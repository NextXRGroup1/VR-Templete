using UnityEngine;
using Training.Core;
using Training.Trigger.Controllers;

namespace Training.Integration
{
    /// <summary>
    /// Collider Is Trigger = ON. Raises a TrainingTrigger (enum) — not the flow title.
    /// Must match the Trigger node currently waiting on the active flow.
    /// </summary>
    public class SimpleFlowTriggerZone : MonoBehaviour
    {
        [SerializeField] private TrainingTrigger triggerToRaise = TrainingTrigger.Continue;
        [SerializeField] private string requiredTag = "Player";
        [SerializeField] private bool fireOnlyOnce = true;

        private bool _fired;

        private void OnTriggerEnter(Collider other)
        {
            if (_fired && fireOnlyOnce) return;
            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            TriggerFlowController.Raise(triggerToRaise);
            _fired = true;
            Debug.Log($"[Zone] Raised {triggerToRaise}");
        }

        /// <summary>Allow the same zone to fire again on a later session.</summary>
        public void ResetZone() => _fired = false;
    }
}
