namespace Training.Sequence
{
    /// <summary>
    /// Contract for any action that can be performed on a sequence step
    /// (select PPE, equip item, rotate valve, pour liquid, etc.).
    /// </summary>
    public interface ISequenceAction
    {
        /// <summary>
        /// Called by the sequence when this step becomes active.
        /// </summary>
        void StartAction();

        /// <summary>
        /// Called when the sequence moves away from this step or is stopped.
        /// </summary>
        void CancelAction();

        /// <summary>
        /// Whether the action has already been completed successfully.
        /// </summary>
        bool IsCompleted { get; }
    }
}
