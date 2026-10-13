using UnityEngine;

namespace Training.Sequence
{
    /// <summary>
    /// Select / confirm action.
    /// Completes only when the correct target object is selected.
    /// Wire XR ray, mouse, or button input to NotifySelected(selectedObject).
    /// </summary>
    public class SelectAction : SequenceActionBase, ISequenceTarget, ISequenceValidatable
    {
        [Tooltip("Object that must be selected.")]
        [SerializeField] private GameObject target;

        public GameObject Target => target;

        public bool Validate(out string error)
        {
            if (target == null)
            {
                error = "SelectAction requires a Target.";
                return false;
            }

            error = null;
            return true;
        }

        protected override void OnStartAction()
        {
            // Waiting for external selection notification.
        }

        protected override void OnCancelAction()
        {
            // Nothing to clean up by default.
        }

        /// <summary>
        /// Call this when the trainee selects an object.
        /// Only completes if the selected object matches the required Target.
        /// </summary>
        public void NotifySelected(GameObject selected)
        {
            if (IsCompleted) return;
            if (target == null || selected == null) return;
            if (selected != target) return;

            Complete();
        }
    }
}
