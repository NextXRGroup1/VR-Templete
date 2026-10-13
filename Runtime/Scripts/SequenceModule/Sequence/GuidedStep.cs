using System;
using UnityEngine;

namespace Training.Sequence
{
    /// <summary>
    /// One step inside a guided sequence.
    /// The Action owns the interaction target (if any).
    /// </summary>
    [Serializable]
    public class GuidedStep
    {
        [Tooltip("Short task label shown in both Guided and NonGuided modes.")]
        public string TaskText;

        [Tooltip("Title shown only in Guided mode.")]
        public string Title;

        [Tooltip("Longer description shown only in Guided mode.")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("Optional hint shown only in Guided mode.")]
        public string Hint;

        [Tooltip("The concrete action the trainee must perform (Select, Equip, custom experiment action, etc.).")]
        public SequenceActionBase Action;
    }
}
