namespace Training.Sequence
{
    public enum GuidanceMode
    {
        /// <summary>
        /// Full guidance: UI (title, description, hint) + narration enabled.
        /// </summary>
        Guided,

        /// <summary>
        /// Minimal guidance: UI and narration disabled. Only the task text is shown.
        /// </summary>
        NonGuided
    }
}
