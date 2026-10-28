using System;
using UnityEngine;
using Training.Core;
using Training.Trigger.Controllers;
using Training.UI.Checklist;
using Training.UI.Progress;

namespace Training.Integration
{
    public class ChecklistProgressFlowBridge : MonoBehaviour
    {
        [SerializeField] private ChecklistController checklist;
        [SerializeField] private ProgressController progress;
        [SerializeField] private TrainingTrigger completionTrigger = TrainingTrigger.TaskCompleted;

        public event Action OnReadyForNext;

        private void OnEnable()
        {
            if (checklist != null)
                checklist.OnAllCompleted += HandleChecklistDone;
            if (progress != null)
                progress.OnCompleted += HandleProgressDone;
        }

        private void OnDisable()
        {
            if (checklist != null)
                checklist.OnAllCompleted -= HandleChecklistDone;
            if (progress != null)
                progress.OnCompleted -= HandleProgressDone;
        }

        private void HandleChecklistDone()
        {
            TriggerFlowController.Raise(completionTrigger);
        }

        private void HandleProgressDone()
        {
            if (progress != null && progress.ManualAdvanceOnly)
            {
                OnReadyForNext?.Invoke();
                return;
            }
            TriggerFlowController.Raise(completionTrigger);
        }
    }
}
