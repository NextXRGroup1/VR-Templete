using UnityEngine;
using Training.Core;
using Training.Trigger.Controllers;
using Training.UI.Checklist;
using Training.UI.Progress;

namespace Training.Integration
{
    /// <summary>
    /// Phase 1: all checklist done → Raise(TaskCompleted).
    /// Phase 2: progress at max → Raise(Continue).
    /// </summary>
    public class ChecklistThenProgressBridge : MonoBehaviour
    {
        [SerializeField] private ChecklistController checklist;
        [SerializeField] private ProgressController progress;

        private bool _checklistDone;

        private void OnEnable()
        {
            if (checklist != null)
                checklist.OnAllCompleted += OnChecklist;
            if (progress != null)
                progress.OnCompleted += OnProgress;
        }

        private void OnDisable()
        {
            if (checklist != null)
                checklist.OnAllCompleted -= OnChecklist;
            if (progress != null)
                progress.OnCompleted -= OnProgress;
        }

        private void OnChecklist()
        {
            _checklistDone = true;
            TriggerFlowController.Raise(TrainingTrigger.TaskCompleted);
        }

        private void OnProgress()
        {
            TriggerFlowController.Raise(TrainingTrigger.Continue);
        }
    }
}
