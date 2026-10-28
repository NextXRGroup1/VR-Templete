using UnityEngine;
using Training.Core;
using Training.Trigger.Controllers;
using Training.UI.Checklist;
using Training.UI.Progress;

namespace Training.Integration
{
    /// <summary>
    /// Full demo: checklist → InteractionCompleted, progress → ConditionMet.
    /// (Sequence uses TaskCompleted, zone uses Continue — separate.)
    /// </summary>
    public class FullModuleDemoBridge : MonoBehaviour
    {
        [SerializeField] private ChecklistController checklist;
        [SerializeField] private ProgressController progress;

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

        private void OnChecklist() =>
            TriggerFlowController.Raise(TrainingTrigger.InteractionCompleted);

        private void OnProgress() =>
            TriggerFlowController.Raise(TrainingTrigger.ConditionMet);
    }
}
