using UnityEngine;
using Training.Core;
using Training.Trigger.Controllers;

namespace Training.Trigger.Components
{
    // NEW FILE. Drop this on any GameObject in the scene to fire a
    // TrainingTrigger — this is the piece that was missing, which is why
    // your Trigger node (NODE 4, TaskCompleted) never resolved.
    //
    // Usage options:
    //   1. Assign _triggerFlowController + _trigger, then call Fire() from
    //      a UI Button OnClick, an XR Interactable event, an Animation
    //      Event, or any UnityEvent.
    //   2. Tick "Fire On Trigger Enter" and put this on a collider volume
    //      the trainee walks/reaches into.
    //   3. Tick "Fire On Start" for a node that should pass through
    //      automatically after a delay.
    public class TrainingTriggerSource : MonoBehaviour
    {
        [SerializeField] private TriggerFlowController _triggerFlowController;
        [SerializeField] private TrainingTrigger _trigger = TrainingTrigger.TaskCompleted;

        [Header("Auto-fire options")]
        [SerializeField] private bool _fireOnStart;
        [Min(0f)]
        [SerializeField] private float _startDelaySeconds;

        [SerializeField] private bool _fireOnTriggerEnter;
        [SerializeField] private string _requiredTag = "";

        [Tooltip("Only allow this source to fire once per session.")]
        [SerializeField] private bool _fireOnlyOnce = true;

        private bool _hasFired;

        private void Start()
        {
            if (_fireOnStart)
                Invoke(nameof(Fire), _startDelaySeconds);
        }

        // Hook this to a UnityEvent / Button OnClick.
        public void Fire()
        {
            if (_fireOnlyOnce && _hasFired) return;

            if (_triggerFlowController == null)
            {
                Debug.LogError($"TrainingTriggerSource on '{name}': no TriggerFlowController assigned.");
                return;
            }

            _hasFired = true;
            _triggerFlowController.RaiseTrigger(_trigger);
        }

        public void ResetSource()
        {
            _hasFired = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_fireOnTriggerEnter) return;
            if (!string.IsNullOrEmpty(_requiredTag) && !other.CompareTag(_requiredTag)) return;
            Fire();
        }
    }
}
