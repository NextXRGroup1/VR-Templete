using UnityEngine;
using Training.Core;
using Training.Sequence;
using Training.Trigger.Controllers;
using Training.Flow.Controllers;

namespace Training.Integration
{
    /// <summary>
    /// Sequence activity ↔ flow Trigger node.
    /// When flow waits for completionTrigger → optional StartSequence.
    /// When sequence completes → TriggerFlowController.Raise (static).
    /// </summary>
    public class SequenceToFlowBridge : MonoBehaviour
    {
        [SerializeField] private GuidedSequenceController sequence;
        [SerializeField] private TrainingFlowController flowController;

        [SerializeField] private TrainingTrigger completionTrigger = TrainingTrigger.TaskCompleted;
        [SerializeField] private bool startSequenceWhenFlowWaits = true;
        [SerializeField] private bool bindFlowWaitEvent = true;

        private void OnEnable()
        {
            if (sequence != null)
                sequence.OnSequenceCompleted += HandleSequenceCompleted;
            if (bindFlowWaitEvent && flowController != null)
                flowController.OnWaitingForTrigger += HandleFlowWaiting;
        }

        private void OnDisable()
        {
            if (sequence != null)
                sequence.OnSequenceCompleted -= HandleSequenceCompleted;
            if (flowController != null)
                flowController.OnWaitingForTrigger -= HandleFlowWaiting;
        }

        private void HandleSequenceCompleted()
        {
            TriggerFlowController.Raise(completionTrigger);
        }

        private void HandleFlowWaiting(TrainingTrigger expected)
        {
            if (!startSequenceWhenFlowWaits) return;
            if (expected != completionTrigger) return;
            sequence?.StartSequence();
        }

        public void StartActivity() => sequence?.StartSequence();
    }
}
