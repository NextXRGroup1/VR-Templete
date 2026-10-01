using UnityEngine;
using Training.Core;
using Training.UI.Panels;

namespace Training.UI.Components
{
    // Creates/selects the appropriate UI panel (Section 24). Reused for
    // panel creation instead of duplicating instantiate logic elsewhere.
    public class UIPanelFactory : MonoBehaviour
    {
        [SerializeField] private TextPanel _textPanelPrefab;
        [SerializeField] private TextImagePanel _textImagePanelPrefab;
        [SerializeField] private AssessmentPanel _assessmentPanelPrefab;

        [Tooltip("Optional. Only required if any AssessmentConfig has Show Results Screen enabled.")]
        [SerializeField] private AssessmentResultPanel _assessmentResultPanelPrefab;

        [Tooltip("Required for multi-session/kiosk use — shown when the whole training session finishes, with the Restart control.")]
        [SerializeField] private TrainingCompletePanel _trainingCompletePanelPrefab;

        [Tooltip("Required only if you have more than one TrainingFlowConfig for a trainee to choose between.")]
        [SerializeField] private TrainingMenuPanel _trainingMenuPanelPrefab;

        [SerializeField] private Transform _panelParent;

        public TextPanel GetTextPanel() => Instantiate(_textPanelPrefab, _panelParent);
        public TextImagePanel GetTextImagePanel() => Instantiate(_textImagePanelPrefab, _panelParent);
        public AssessmentPanel GetAssessmentPanel() => Instantiate(_assessmentPanelPrefab, _panelParent);

        public AssessmentResultPanel GetAssessmentResultPanel()
        {
            if (_assessmentResultPanelPrefab == null)
            {
                Debug.LogError("UIPanelFactory: an assessment wants to show its results screen, but no AssessmentResultPanel prefab is assigned.");
                return null;
            }
            return Instantiate(_assessmentResultPanelPrefab, _panelParent);
        }

        public TrainingCompletePanel GetTrainingCompletePanel()
        {
            if (_trainingCompletePanelPrefab == null)
            {
                Debug.LogError("UIPanelFactory: the session finished, but no TrainingCompletePanel prefab is assigned — nothing will be shown and Restart won't be reachable.");
                return null;
            }
            return Instantiate(_trainingCompletePanelPrefab, _panelParent);
        }

        public TrainingMenuPanel GetTrainingMenuPanel()
        {
            if (_trainingMenuPanelPrefab == null)
            {
                Debug.LogError("UIPanelFactory: a training-selection menu was requested, but no TrainingMenuPanel prefab is assigned.");
                return null;
            }
            return Instantiate(_trainingMenuPanelPrefab, _panelParent);
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