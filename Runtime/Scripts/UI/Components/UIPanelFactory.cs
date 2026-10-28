using UnityEngine;
using Training.Core;
using Training.UI.Panels;

namespace Training.UI.Components
{
    public class UIPanelFactory : MonoBehaviour
    {
        [SerializeField] private TextPanel _textPanelPrefab;
        [SerializeField] private TextImagePanel _textImagePanelPrefab;
        [SerializeField] private AssessmentPanel _assessmentPanelPrefab;

        [Tooltip("Optional. Only required if any AssessmentConfig has Show Results Screen enabled.")]
        [SerializeField] private AssessmentResultPanel _assessmentResultPanelPrefab;

        [Tooltip("Required for multi-session/kiosk use.")]
        [SerializeField] private TrainingCompletePanel _trainingCompletePanelPrefab;

        [Tooltip("Required only if you have more than one TrainingFlowConfig.")]
        [SerializeField] private TrainingMenuPanel _trainingMenuPanelPrefab;

        [Tooltip("Optional. Checklist status panel.")]
        [SerializeField] private ChecklistPanel _checklistPanelPrefab;

        [Tooltip("Optional. Progress / fill panel.")]
        [SerializeField] private ProgressPanel _progressPanelPrefab;

        [SerializeField] private Transform _panelParent;

        public TextPanel GetTextPanel() => Instantiate(_textPanelPrefab, _panelParent);
        public TextImagePanel GetTextImagePanel() => Instantiate(_textImagePanelPrefab, _panelParent);
        public AssessmentPanel GetAssessmentPanel() => Instantiate(_assessmentPanelPrefab, _panelParent);

        public AssessmentResultPanel GetAssessmentResultPanel()
        {
            if (_assessmentResultPanelPrefab == null)
            {
                Debug.LogError("UIPanelFactory: AssessmentResultPanel prefab not assigned.");
                return null;
            }
            return Instantiate(_assessmentResultPanelPrefab, _panelParent);
        }

        public TrainingCompletePanel GetTrainingCompletePanel()
        {
            if (_trainingCompletePanelPrefab == null)
            {
                Debug.LogError("UIPanelFactory: TrainingCompletePanel prefab not assigned.");
                return null;
            }
            return Instantiate(_trainingCompletePanelPrefab, _panelParent);
        }

        public TrainingMenuPanel GetTrainingMenuPanel()
        {
            if (_trainingMenuPanelPrefab == null)
            {
                Debug.LogError("UIPanelFactory: TrainingMenuPanel prefab not assigned.");
                return null;
            }
            return Instantiate(_trainingMenuPanelPrefab, _panelParent);
        }

        public ChecklistPanel GetChecklistPanel()
        {
            if (_checklistPanelPrefab == null)
            {
                Debug.LogError("UIPanelFactory: ChecklistPanel prefab not assigned.");
                return null;
            }
            return Instantiate(_checklistPanelPrefab, _panelParent);
        }

        public ProgressPanel GetProgressPanel()
        {
            if (_progressPanelPrefab == null)
            {
                Debug.LogError("UIPanelFactory: ProgressPanel prefab not assigned.");
                return null;
            }
            return Instantiate(_progressPanelPrefab, _panelParent);
        }

        public IUIPanel GetPanelForNode(FlowNodeType nodeType)
        {
            switch (nodeType)
            {
                case FlowNodeType.Text: return GetTextPanel();
                case FlowNodeType.TextImage: return GetTextImagePanel();
                case FlowNodeType.Assessment: return GetAssessmentPanel();
                default: return null;
            }
        }
    }
}
