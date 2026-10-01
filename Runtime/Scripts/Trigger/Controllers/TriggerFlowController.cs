using System;
using UnityEngine;
using Training.Core;

namespace Training.Trigger.Controllers
{
    // Waits for configured enum triggers and resolves the matching flow step.
    // Knows nothing about UI.
    //
    // CHANGES vs the old version:
    //  - Remembers a trigger that arrived slightly BEFORE the flow started
    //    waiting for it (_pendingTrigger). Previously an early RaiseTrigger
    //    was thrown away and the flow waited forever.
    //  - OnWaitStarted / verbose logging so you can see what it is waiting on.
    //  - DebugRaise() + a debug key so you can test a Trigger node without
    //    having wired up any VR interaction yet.
    public class TriggerFlowController : MonoBehaviour
    {
        [Tooltip("Log waits and raised triggers to the Console.")]
        [SerializeField] private bool _verboseLogging = true;

        [Header("Editor testing")]
        [Tooltip("In the Editor, press this key to fire the trigger the flow is currently waiting for.")]
        [SerializeField] private bool _enableDebugKey = true;
        [SerializeField] private KeyCode _debugKey = KeyCode.T;

        public event Action<TrainingTrigger> OnTriggerReceived;
        public event Action<TrainingTrigger> OnWaitStarted;

        private TrainingTrigger _expectedTrigger = TrainingTrigger.None;
        private TrainingTrigger _pendingTrigger = TrainingTrigger.None;
        private Action _onMatched;

        public TrainingTrigger ExpectedTrigger => _expectedTrigger;
        public bool IsWaiting => _expectedTrigger != TrainingTrigger.None;

        public void WaitForTrigger(TrainingTrigger expectedTrigger, Action onMatched)
        {
            _expectedTrigger = expectedTrigger;
            _onMatched = onMatched;

            Log($"Waiting for TrainingTrigger.{expectedTrigger}");
            OnWaitStarted?.Invoke(expectedTrigger);

            // If it already arrived a frame or two early, consume it now.
            if (_pendingTrigger == expectedTrigger)
            {
                Log($"TrainingTrigger.{expectedTrigger} had already been raised — resolving immediately.");
                _pendingTrigger = TrainingTrigger.None;
                Resolve();
            }
        }

        public void CancelWait()
        {
            _expectedTrigger = TrainingTrigger.None;
            _onMatched = null;
        }

        // Call this from ANY script: a grab event, a collider, a task check,
        // a UI button, an XR interactable, an animation event, etc.
        //   triggerFlowController.RaiseTrigger(TrainingTrigger.TaskCompleted);
        public void RaiseTrigger(TrainingTrigger trigger)
        {
            if (trigger == TrainingTrigger.None) return;

            Log($"RaiseTrigger({trigger}) — currently expecting {_expectedTrigger}");
            OnTriggerReceived?.Invoke(trigger);

            if (_expectedTrigger != TrainingTrigger.None && trigger == _expectedTrigger)
            {
                Resolve();
            }
            else
            {
                // Buffer it in case the flow starts waiting for it this frame.
                _pendingTrigger = trigger;
            }
        }

        // Inspector/UnityEvent-friendly overload (UnityEvents can't pass enums).
        public void RaiseTriggerByName(string triggerName)
        {
            if (Enum.TryParse(triggerName, true, out TrainingTrigger parsed))
                RaiseTrigger(parsed);
            else
                Debug.LogError($"TriggerFlowController: '{triggerName}' is not a valid TrainingTrigger.");
        }

        // Fires whatever the flow is currently waiting on. For testing only.
        public void DebugRaiseExpected()
        {
            if (_expectedTrigger == TrainingTrigger.None)
            {
                Debug.LogWarning("TriggerFlowController: not waiting for any trigger right now.");
                return;
            }
            RaiseTrigger(_expectedTrigger);
        }

        private void Resolve()
        {
            var callback = _onMatched;
            _expectedTrigger = TrainingTrigger.None;
            _onMatched = null;
            callback?.Invoke();
        }

        private void Update()
        {
            if (!_enableDebugKey) return;
            if (!Application.isEditor) return;
            if (Input.GetKeyDown(_debugKey)) DebugRaiseExpected();
        }

        private void Log(string message)
        {
            if (_verboseLogging) Debug.Log($"[Trigger] {message}");
        }
    }
}