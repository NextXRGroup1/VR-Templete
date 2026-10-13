using UnityEngine;
using Training.Flow.Controllers;
using Training.Flow.Components;
using Training.Flow.Data;
using Training.UI.Components;
using Training.UI.Panels;

namespace Training.Integration
{
    /// <summary>
    /// When multiple TrainingFlowConfigs are assigned:
    /// - showMenuOnStart = true  → training picker first (FlowRunner should not auto-start)
    /// - false → index 0 starts via FlowRunner
    /// After a session, use completion panel Retry / Choose Another (already in UIFlowController).
    /// </summary>
    public class MultiFlowSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private TrainingSessionController session;
        [SerializeField] private UIPanelFactory panelFactory;
        [SerializeField] private FlowRunner flowRunner;

        [Tooltip("If true and more than one flow is available, show the menu instead of auto-starting index 0.")]
        [SerializeField] private bool showMenuOnStart = true;

        private TrainingMenuPanel _menu;

        private void Start()
        {
            if (session == null || panelFactory == null) return;

            if (showMenuOnStart && session.HasMultipleFlowConfigs)
            {
                if (flowRunner != null)
                {
                    // Ensure runner does not also StartSession in same frame
                    var so = flowRunner;
                    // start already ran if Start order is wrong — disable component before play preferred
                }
                ShowMenu();
            }
        }

        public void ShowMenu()
        {
            DestroyMenu();
            _menu = panelFactory.GetTrainingMenuPanel();
            if (_menu == null) return;

            _menu.PopulateMenu(session.AvailableFlowConfigs);
            _menu.OnConfigSelected += OnPicked;
            _menu.Show();
        }

        private void OnPicked(TrainingFlowConfig config)
        {
            if (_menu != null)
                _menu.OnConfigSelected -= OnPicked;
            DestroyMenu();
            session.StartSession(config);
        }

        private void DestroyMenu()
        {
            if (_menu == null) return;
            _menu.Hide();
            Destroy(_menu.gameObject);
            _menu = null;
        }
    }
}
