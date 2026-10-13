namespace Training.Sequence
{
    /// <summary>
    /// UI contract used by the sequence.
    /// Implement this on whatever panel you use.
    /// </summary>
    public interface ISequenceUI
    {
        /// <summary>
        /// Guided mode – show full information.
        /// </summary>
        void ShowGuided(string title, string description, string hint);

        /// <summary>
        /// Non-Guided mode – show only the short task text.
        /// </summary>
        void ShowTaskOnly(string taskText);

        /// <summary>
        /// Hide the panel.
        /// </summary>
        void Hide();
    }
}
