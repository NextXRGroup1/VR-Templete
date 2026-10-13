using System.Collections.Generic;
using UnityEngine;

namespace Training.Sequence
{
    /// <summary>
    /// Basic highlighter that enables/disables a child named "Highlight"
    /// (or any component you assign) on the target object.
    /// Replace with outline/emission later if needed.
    /// </summary>
    public class SimpleHighlighter : MonoBehaviour, IHighlighter
    {
        [Tooltip("Optional: if set, this specific object is toggled instead of searching for a child named Highlight.")]
        public GameObject FallbackHighlightObject;

        private readonly HashSet<GameObject> _active = new HashSet<GameObject>();

        public void SetHighlight(GameObject target, bool enabled)
        {
            if (target == null) return;

            var highlight = FindHighlightObject(target);
            if (highlight == null) return;

            highlight.SetActive(enabled);

            if (enabled) _active.Add(target);
            else _active.Remove(target);
        }

        public void ClearAll()
        {
            foreach (var go in _active)
            {
                if (go == null) continue;
                var highlight = FindHighlightObject(go);
                if (highlight != null) highlight.SetActive(false);
            }
            _active.Clear();
        }

        private GameObject FindHighlightObject(GameObject target)
        {
            // Prefer a child named "Highlight"
            var child = target.transform.Find("Highlight");
            if (child != null) return child.gameObject;

            // Fallback to the assigned object if any
            return FallbackHighlightObject;
        }
    }
}
