namespace Training.Flow.State
{
    public enum FlowState
    {
        NotStarted,
        ShowingContent,
        WaitingForTrigger,
        RunningAssessment,
        ShowingResults,
        Completed
    }
}