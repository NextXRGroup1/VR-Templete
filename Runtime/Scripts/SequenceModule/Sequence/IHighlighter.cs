using UnityEngine;

namespace Training.Sequence
{
    /// <summary>
    /// Abstraction for highlighting an object.
    /// Implement with outline, emission, or any visual method you prefer.
    /// </summary>
    public interface IHighlighter
    {
        void SetHighlight(GameObject target, bool enabled);
        void ClearAll();
    }
}
