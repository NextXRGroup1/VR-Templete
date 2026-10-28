using UnityEngine;
using UnityEngine.InputSystem;
using Training.UI.Checklist;
using Training.UI.Progress;

namespace Training.Integration
{
    /// <summary>
    /// Editor/demo: 1/2/3 mark checklist items, +/- add progress.
    /// </summary>
    public class ChecklistProgressDemoInput : MonoBehaviour
    {
        public ChecklistController Checklist;
        public ProgressController Progress;
        public string[] ChecklistIds = { "Inspect", "Close", "Confirm" };
        public float ProgressStep = 25f;

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Checklist != null && ChecklistIds != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame && ChecklistIds.Length > 0)
                    Checklist.MarkComplete(ChecklistIds[0]);
                if (Keyboard.current.digit2Key.wasPressedThisFrame && ChecklistIds.Length > 1)
                    Checklist.MarkComplete(ChecklistIds[1]);
                if (Keyboard.current.digit3Key.wasPressedThisFrame && ChecklistIds.Length > 2)
                    Checklist.MarkComplete(ChecklistIds[2]);
            }

            if (Progress != null)
            {
                if (Keyboard.current.equalsKey.wasPressedThisFrame || Keyboard.current.numpadPlusKey.wasPressedThisFrame)
                    Progress.Add(ProgressStep);
                if (Keyboard.current.minusKey.wasPressedThisFrame || Keyboard.current.numpadMinusKey.wasPressedThisFrame)
                    Progress.Add(-ProgressStep);
            }
        }
    }
}
