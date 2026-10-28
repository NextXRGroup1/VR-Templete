using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Training.UI.Checklist;

namespace Training.UI.Panels
{
    public class ChecklistPanel : UIPanelBase
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Transform listRoot;
        [SerializeField] private ChecklistRowView rowPrefab;

        private readonly List<ChecklistRowView> _rows = new List<ChecklistRowView>();
        private ChecklistController _controller;

        public void Wire(TextMeshProUGUI title, Transform list, ChecklistRowView row)
        {
            titleText = title;
            listRoot = list;
            rowPrefab = row;
        }

        public void Bind(ChecklistController controller, string title = "Checklist")
        {
            Unbind();
            _controller = controller;
            if (titleText != null) titleText.text = title;
            if (_controller != null)
            {
                _controller.OnItemChanged += OnItemChanged;
                _controller.OnReset += Rebuild;
            }
            Rebuild();
        }

        public void Unbind()
        {
            if (_controller == null) return;
            _controller.OnItemChanged -= OnItemChanged;
            _controller.OnReset -= Rebuild;
            _controller = null;
        }

        private void OnDestroy() => Unbind();

        private void Rebuild()
        {
            ClearRows();
            if (_controller == null || rowPrefab == null || listRoot == null) return;
            foreach (var item in _controller.Items)
            {
                if (item == null) continue;
                var row = Instantiate(rowPrefab, listRoot);
                row.gameObject.SetActive(true);
                row.Setup(item.Id, item.Label, _controller.IsChecked(item.Id));
                _rows.Add(row);
            }
        }

        private void OnItemChanged(string id, bool isChecked)
        {
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i] != null && _rows[i].Id == id)
                    _rows[i].SetChecked(isChecked);
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i] != null) Destroy(_rows[i].gameObject);
            _rows.Clear();
        }
    }
}
