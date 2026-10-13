using System;
using UnityEngine;

namespace Training.Sequence
{
    /// <summary>
    /// Base class for all sequence actions.
    /// Inherit from this to create Select, Equip, RotateValve, Pour, etc.
    /// </summary>
    public abstract class SequenceActionBase : MonoBehaviour, ISequenceAction
    {
        public bool IsCompleted { get; private set; }

        /// <summary>
        /// Fired when the action is successfully completed.
        /// The GuidedSequenceController listens to this.
        /// </summary>
        public event Action OnCompleted;

        public void StartAction()
        {
            IsCompleted = false;
            OnStartAction();
        }

        public void CancelAction()
        {
            OnCancelAction();
        }

        /// <summary>
        /// Call this from your concrete action when the trainee succeeds.
        /// </summary>
        protected void Complete()
        {
            if (IsCompleted) return;

            IsCompleted = true;
            OnCompleted?.Invoke();
        }

        /// <summary>
        /// Implement the specific interaction logic here.
        /// </summary>
        protected abstract void OnStartAction();

        /// <summary>
        /// Clean up listeners, visuals, etc. Must unsubscribe any external events.
        /// </summary>
        protected abstract void OnCancelAction();
    }
}
