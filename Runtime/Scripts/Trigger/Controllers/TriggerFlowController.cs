using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Training.Core;

namespace Training.Trigger.Controllers
{
    public class TriggerFlowController : MonoBehaviour
    {
        [SerializeField] private bool _verboseLogging = true;

        [Header("Editor testing")]
        [SerializeField] private bool _enableDebugKey = true;
        [SerializeField] private Key _debugKey = Key.T;

        /// <summary>No hard reference needed: TriggerFlowController.Raise(...)</summary>
        public static event Action<TrainingTrigger> OnGlobalTriggerRaised;

        public event Action<TrainingTrigger> OnTriggerReceived;
        public event Action<TrainingTrigger> OnWaitStarted;

        private TrainingTrigger _expectedTrigger = TrainingTrigger.None;
        private TrainingTrigger _pendingTrigger = TrainingTrigger.None;
        private Action _onMatched;

        public TrainingTrigger ExpectedTrigger => _expectedTrigger;
        public bool IsWaiting => _expectedTrigger != TrainingTrigger.None;

        private void OnEnable()
        {
            OnGlobalTriggerRaised += RaiseTrigger;
        }

        private void OnDisable()
        {
            OnGlobalTriggerRaised -= RaiseTrigger;
        }

        public static void Raise(TrainingTrigger trigger)
        {
            if (trigger == TrainingTrigger.None) return;
            OnGlobalTriggerRaised?.Invoke(trigger);
        }

        public static void RaiseByName(string triggerName)
        {
            if (Enum.TryParse(triggerName, true, out TrainingTrigger parsed))
                Raise(parsed);
            else
                Debug.LogError(
                    $"TriggerFlowController: '{triggerName}' is not a valid TrainingTrigger. " +
                    "Use enum names only (TaskCompleted, Continue, ConditionMet, …) — not the flow Title.");
        }

        public void WaitForTrigger(TrainingTrigger expectedTrigger, Action onMatched)
        {
            _expectedTrigger = expectedTrigger;
            _onMatched = onMatched;

            Log($"Waiting for TrainingTrigger.{expectedTrigger}");
            OnWaitStarted?.Invoke(expectedTrigger);

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

        public void RaiseTrigger(TrainingTrigger trigger)
        {
            if (trigger == TrainingTrigger.None) return;

            Log($"RaiseTrigger({trigger}) — currently expecting {_expectedTrigger}");
            OnTriggerReceived?.Invoke(trigger);

            if (_expectedTrigger != TrainingTrigger.None && trigger == _expectedTrigger)
                Resolve();
            else
                _pendingTrigger = trigger;
        }

        public void RaiseTriggerByName(string triggerName) => RaiseByName(triggerName);

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
            if (!_enableDebugKey || !Application.isEditor) return;
            if (Keyboard.current != null && Keyboard.current[_debugKey].wasPressedThisFrame)
                DebugRaiseExpected();
        }

        private void Log(string message)
        {
            if (_verboseLogging) Debug.Log($"[Trigger] {message}");
        }
    }
}
