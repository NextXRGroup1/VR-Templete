using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Training.Flow.Data;

namespace Training.UI.Panels
{
    // Lists the available trainings (TrainingSessionController.
    // AvailableFlowConfigs) as a set of buttons and reports which one the
    // trainee picked. Purely presentational — does not start anything
    // itself; UIFlowController calls TrainingSessionController.
    // StartSession(config) when OnConfigSelected fires.
    public class TrainingMenuPanel : UIPanelBase
    {
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Transform _listContainer;
        [SerializeField] private GameObject _menuItemPrefab; // needs a Button + a TextMeshProUGUI (or Text) label somewhere on/under it

        public event Action<TrainingFlowConfig> OnConfigSelected;

        private readonly List<GameObject> _spawnedItems = new List<GameObject>();

        public void PopulateMenu(IReadOnlyList<TrainingFlowConfig> configs)
        {
            ClearMenu();

            if (_titleText != null)
                _titleText.text = "Choose a Training";

            if (configs == null) return;

            foreach (var config in configs)
            {
                if (config == null) continue;

                var itemGO = Instantiate(_menuItemPrefab, _listContainer);
                _spawnedItems.Add(itemGO);

                var label = itemGO.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = config.DisplayTitle;

                var button = itemGO.GetComponentInChildren<Button>();
                if (button != null)
                {
                    var capturedConfig = config; // avoid the classic foreach-closure capture bug
                    button.onClick.AddListener(() => OnConfigSelected?.Invoke(capturedConfig));
                }
            }
        }

        private void ClearMenu()
        {
            foreach (var item in _spawnedItems)
                Destroy(item);
            _spawnedItems.Clear();
        }
    }
}
