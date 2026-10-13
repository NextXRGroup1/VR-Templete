using UnityEngine;
using Training.Core;
using Training.Trigger.Controllers;

namespace Training.Integration
{
    public class ActivityToFlowBridge : MonoBehaviour
    {
        [SerializeField] private TrainingTrigger completionTrigger = TrainingTrigger.TaskCompleted;

        public void CompleteActivity()
        {
            TriggerFlowController.Raise(completionTrigger);
        }

        public void CompleteActivityWith(TrainingTrigger trigger)
        {
            TriggerFlowController.Raise(trigger);
        }
    }
}
