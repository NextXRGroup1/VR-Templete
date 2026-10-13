namespace Training.Sequence
{
    /// <summary>
    /// Optional validation contract for actions.
    /// Editor and runtime call this before starting a sequence.
    /// </summary>
    public interface ISequenceValidatable
    {
        bool Validate(out string error);
    }
}
