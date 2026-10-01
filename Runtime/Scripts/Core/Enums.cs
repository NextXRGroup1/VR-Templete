namespace Training.Core
{
    public enum FlowMode
    {
        Sequential,
        Trigger
    }

    // NOTE: "Trigger" was added to this enum. The source spec's Section 9 list
    // (Text, TextImage, Assessment) doesn't include it, but Section 27's example
    // scenario has a NODE_004 of Type: Trigger, so a node type is needed to
    // represent "wait for an enum trigger" as a distinct step in the flow.
    public enum FlowNodeType
    {
        Text,
        TextImage,
        Assessment,
        Trigger
    }

    public enum QuestionType
    {
        QuestionAnswer,
        YesNo,
        TrueFalse,
        SingleChoice,
        MultipleChoice
    }

    public enum TrainingTrigger
    {
        None,
        Start,
        Continue,
        InteractionCompleted,
        TaskCompleted,
        ConditionMet,
        AssessmentStarted,
        AssessmentCompleted
    }

    public enum NavigationMode
    {
        None,
        Next,
        BackNext
    }

    public enum AssessmentNavigationMode
    {
        Sequential,
        FreeNavigation
    }
}
