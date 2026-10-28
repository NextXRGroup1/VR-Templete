using System;
using System.Collections.Generic;
using UnityEngine;

namespace Training.UI.Checklist
{
    [Serializable]
    public class ChecklistItemDef
    {
        public string Id;
        public string Label;
    }

    public class ChecklistController : MonoBehaviour
    {
        [SerializeField] private List<ChecklistItemDef> items = new List<ChecklistItemDef>();
        [SerializeField] private bool fireWhenAllComplete = true;

        public event Action OnAllCompleted;
        public event Action<string, bool> OnItemChanged;
        public event Action OnReset;

        private readonly HashSet<string> _completed = new HashSet<string>();
        private bool _allFired;

        public IReadOnlyList<ChecklistItemDef> Items => items;
        public int TotalCount => items != null ? items.Count : 0;
        public int CompletedCount => _completed.Count;
        public float Progress01 => TotalCount <= 0 ? 0f : (float)CompletedCount / TotalCount;
        public bool IsAllComplete => TotalCount > 0 && CompletedCount >= TotalCount;

        public void Setup(List<ChecklistItemDef> defs)
        {
            items = defs ?? new List<ChecklistItemDef>();
            ResetAll();
        }

        public void MarkComplete(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!ContainsId(id))
            {
                Debug.LogWarning($"[Checklist] Unknown id '{id}'.");
                return;
            }
            if (!_completed.Add(id)) return;
            OnItemChanged?.Invoke(id, true);
            TryFireAllCompleted();
        }

        public void MarkIncomplete(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!_completed.Remove(id)) return;
            _allFired = false;
            OnItemChanged?.Invoke(id, false);
        }

        public bool IsChecked(string id) => _completed.Contains(id);

        public void ResetAll()
        {
            _completed.Clear();
            _allFired = false;
            OnReset?.Invoke();
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && !string.IsNullOrEmpty(items[i].Id))
                    OnItemChanged?.Invoke(items[i].Id, false);
            }
        }

        private bool ContainsId(string id)
        {
            if (items == null) return false;
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].Id == id) return true;
            return false;
        }

        private void TryFireAllCompleted()
        {
            if (!fireWhenAllComplete || _allFired || !IsAllComplete) return;
            _allFired = true;
            OnAllCompleted?.Invoke();
        }
    }
}
