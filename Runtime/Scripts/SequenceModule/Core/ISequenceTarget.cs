using UnityEngine;

namespace Training.Sequence
{
    /// <summary>
    /// Optional interface for actions that have a physical target object.
    /// Used by the sequence for highlighting. Actions without a target
    /// simply do not implement this.
    /// </summary>
    public interface ISequenceTarget
    {
        GameObject Target { get; }
    }
}
