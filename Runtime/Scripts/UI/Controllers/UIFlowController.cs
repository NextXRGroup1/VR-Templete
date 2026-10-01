using UnityEngine;
using Training.Core;
using Training.Flow.Data;
using Training.Flow.Controllers;
using Training.Assessment.Controllers;
using Training.Assessment.Data;
using Training.Question.Data;
using Training.Question.Controllers;
using Training.UI.Components;
using Training.UI.Panels;

namespace Training.UI.Controllers
{
    // Bridges flow/assessment events to UI panels. Displays content and
    // forwards user actions — it never calculates score or decides the next node.
    public class UIFlowController : MonoBehaviour
    {
        [SerializeField] private UIPanelFactory _panelFactory;
        [SerializeField] private TrainingFlowController _flowController;
        [SerializeField] private AssessmentController _assessmentController;
        [SerializeField] private QuestionController _questionController;

        [Tooltip("Required for multi-session/kiosk use — RestartSession() is called on this when the trainee presses Restart.")]
        [SerializeField] private TrainingSessionController _sessionController;

        private TextPanel _textPanel;
        private TextImagePanel _textImagePanel;
        private AssessmentPanel _assessmentPanel;
        private AssessmentResultPanel _resultPanel;
        private TrainingCompletePanel _completePanel;
        private TrainingMenuPanel _menuPanel;

        private void OnEnable()
        {
            _flowController.OnNodeEntered += HandleNodeEntered;
            _flowController.OnShowContentNode += HandleShowContentNode;
            _flowController.OnShowAssessmentResult += HandleShowAssessmentResult;
            _flowController.OnFlowCompleted += HandleFlowCompleted;
            _assessmentController.OnShowQuestion += HandleShowQuestion;
            _questionController.OnAnswerValidated += HandleAnswerValidated;
            _questionController.OnFeedbackDelayElapsed += HandleFeedbackDelayElapsed;

            // Static event on TrainingSessionController — fires once the
            // WHOLE session finishes (as opposed to OnFlowCompleted above,
            // which TrainingFlowController also raises and which this
            // class already uses just to tear down leftover panels).
            TrainingSessionController.OnSessionCompleted += HandleSessionCompleted;
        }

        private void OnDisable()
        {
            _flowController.OnNodeEntered -= HandleNodeEntered;
            _flowController.OnShowContentNode -= HandleShowContentNode;
            _flowController.OnShowAssessmentResult -= HandleShowAssessmentResult;
            _flowController.OnFlowCompleted -= HandleFlowCompleted;
            _assessmentController.OnShowQuestion -= HandleShowQuestion;
            _questionController.OnAnswerValidated -= HandleAnswerValidated;
            _questionController.OnFeedbackDelayElapsed -= HandleFeedbackDelayElapsed;
            TrainingSessionController.OnSessionCompleted -= HandleSessionCompleted;
        }

        // Fires for EVERY node, before the node executes.
        private void HandleNodeEntered(FlowNodeData node)
        {
            switch (node.NodeType)
            {
                case FlowNodeType.Text:
                case FlowNodeType.TextImage:
                    DestroyAssessmentPanel();
                    DestroyResultPanel();
                    DestroyCompletePanel();
                    DestroyMenuPanel();
                    break;

                case FlowNodeType.Assessment:
                    DestroyContentPanels();
                    DestroyResultPanel();
                    DestroyCompletePanel();
                    DestroyMenuPanel();
                    break;

                case FlowNodeType.Trigger:
                    // A trigger node has no UI of its own: clear the screen so
                    // the trainee isn't staring at the last answered question.
                    DestroyContentPanels();
                    DestroyAssessmentPanel();
                    DestroyResultPanel();
                    DestroyCompletePanel();
                    DestroyMenuPanel();
                    break;
            }
        }

        private void HandleShowContentNode(FlowNodeData node)
        {
            DestroyContentPanels();

            switch (node.NodeType)
            {
                case FlowNodeType.Text:
                    {
                        _textPanel = _panelFactory.GetTextPanel();
                        _textPanel.SetContent(node.TextContent, node.Navigation);
                        _textPanel.OnNextPressed += _flowController.ReportNext;
                        _textPanel.OnBackPressed += _flowController.ReportBack;
                        _textPanel.Show();
                        break;
                    }
                case FlowNodeType.TextImage:
                    {
                        _textImagePanel = _panelFactory.GetTextImagePanel();
                        _textImagePanel.SetContent(node.TextContent, node.Image, node.Navigation);
                        _textImagePanel.OnNextPressed += _flowController.ReportNext;
                        _textImagePanel.OnBackPressed += _flowController.ReportBack;
                        _textImagePanel.Show();
                        break;
                    }
            }
        }

        private void HandleShowQuestion(QuestionData question, int index, int total)
        {
            bool freeNav = _assessmentController.IsFreeNavigation;

            if (_assessmentPanel == null)
            {
                DestroyContentPanels();
                _assessmentPanel = _panelFactory.GetAssessmentPanel();
                _assessmentPanel.QuestionPanel.OnAnswerSubmitted += _questionController.ReceiveAnswer;

                _assessmentPanel.SetFreeNavigationControlsVisible(freeNav);
                if (freeNav)
                {
                    _assessmentPanel.OnPreviousQuestionPressed += _assessmentController.GoToPrevious;
                    _assessmentPanel.OnNextQuestionPressed += _assessmentController.GoToNext;
                    _assessmentPanel.OnFinishAssessmentPressed += _assessmentController.FinishAssessment;
                }

                _assessmentPanel.Show();
            }

            _assessmentPanel.QuestionPanel.SetQuestion(question);
            _assessmentPanel.SetProgress(index, total);

            // On the last question there's nothing for Next to advance to —
            // Finish is the way forward from there. Back/Finish are untouched.
            if (freeNav)
                _assessmentPanel.SetNextButtonVisible(index < total - 1);
        }

        private void HandleAnswerValidated(QuestionResultData result)
        {
            if (_assessmentPanel == null) return;

            _assessmentPanel.QuestionPanel.ShowFeedback(result);

            // Brief, visible-but-grayed disable — NOT a hide. If this is
            // the last question, Next is already hidden (see
            // HandleShowQuestion) and this has no visible effect either way.
            if (_assessmentController.IsFreeNavigation)
                _assessmentPanel.SetNextButtonInteractable(false);
        }

        // Fires once the (short) feedback delay actually finishes.
        // Re-enables Next's interactivity — its visibility (shown/hidden
        // based on last-question) is set separately in HandleShowQuestion
        // and is untouched here.
        private void HandleFeedbackDelayElapsed()
        {
            if (_assessmentPanel == null || !_assessmentController.IsFreeNavigation) return;

            _assessmentPanel.SetNextButtonInteractable(true);
        }

        // Only fires when the finished assessment's AssessmentConfig has
        // ShowResultsScreen == true — TrainingFlowController skips straight
        // to the next node otherwise, and this handler is simply never called.
        private void HandleShowAssessmentResult(AssessmentSectionResultData result)
        {
            DestroyAssessmentPanel();

            _resultPanel = _panelFactory.GetAssessmentResultPanel();
            if (_resultPanel == null) return; // factory already logged why

            _resultPanel.SetResult(result);
            _resultPanel.OnContinuePressed += HandleResultContinuePressed;
            _resultPanel.OnRetryPressed += HandleResultRetryPressed;
            _resultPanel.Show();
        }

        private void HandleResultContinuePressed()
        {
            DestroyResultPanel();
            _flowController.ReportResultsContinue();
        }

        private void HandleResultRetryPressed()
        {
            DestroyResultPanel();
            _flowController.ReportResultsRetry();
        }

        // Fires once the WHOLE session is done (TrainingSessionController's
        // static event) — not per-assessment. Shows the final screen with
        // the Restart control, unless the training that just finished has
        // ShowCompletionScreen off.
        private void HandleSessionCompleted(TrainingScoreData data)
        {
            DestroyContentPanels();
            DestroyAssessmentPanel();
            DestroyResultPanel();

            var config = _sessionController.CurrentFlowConfig;
            if (config != null && !config.ShowCompletionScreen)
            {
                // Score is still fully computed and was already delivered
                // via the OnSessionCompleted event above — this just skips
                // showing anything automatically for this particular training.
                return;
            }

            _completePanel = _panelFactory.GetTrainingCompletePanel();
            if (_completePanel == null) return; // factory already logged why

            bool calculateScore = config == null || config.CalculateScore;
            bool showFinalQuizPanel = config != null && config.ShowFinalQuizPanel;
            var completionImage = config != null ? config.CompletionImage : null;

            // Home/Quiz buttons in the Final Quiz Panel layout deliberately
            // have nothing wired here - see TrainingCompletePanel for why.
            _completePanel.SetResult(data, calculateScore, showFinalQuizPanel, completionImage);
            _completePanel.SetChooseAnotherVisible(_sessionController.HasMultipleFlowConfigs);
            _completePanel.OnRestartPressed += HandleRestartPressed;
            _completePanel.OnChooseAnotherPressed += HandleChooseAnotherPressed;
            _completePanel.Show();
        }

        private void HandleRestartPressed()
        {
            DestroyCompletePanel();
            _sessionController.RestartSession();
        }

        private void HandleChooseAnotherPressed()
        {
            DestroyCompletePanel();

            _menuPanel = _panelFactory.GetTrainingMenuPanel();
            if (_menuPanel == null) return; // factory already logged why

            _menuPanel.PopulateMenu(_sessionController.AvailableFlowConfigs);
            _menuPanel.OnConfigSelected += HandleConfigSelected;
            _menuPanel.Show();
        }

        private void HandleConfigSelected(TrainingFlowConfig config)
        {
            DestroyMenuPanel();
            _sessionController.StartSession(config);
        }

        private void HandleFlowCompleted()
        {
            DestroyContentPanels();
            DestroyAssessmentPanel();
            DestroyResultPanel();
        }

        private void DestroyContentPanels()
        {
            if (_textPanel != null)
            {
                _textPanel.OnNextPressed -= _flowController.ReportNext;
                _textPanel.OnBackPressed -= _flowController.ReportBack;
                Destroy(_textPanel.gameObject);
                _textPanel = null;
            }

            if (_textImagePanel != null)
            {
                _textImagePanel.OnNextPressed -= _flowController.ReportNext;
                _textImagePanel.OnBackPressed -= _flowController.ReportBack;
                Destroy(_textImagePanel.gameObject);
                _textImagePanel = null;
            }
        }

        private void DestroyAssessmentPanel()
        {
            if (_assessmentPanel == null) return;

            _assessmentPanel.QuestionPanel.OnAnswerSubmitted -= _questionController.ReceiveAnswer;

            // Unsubscribing an event that was never subscribed (Sequential
            // assessments never wire these) is a safe no-op in C#, so this
            // doesn't need to track whether FreeNavigation was actually on.
            _assessmentPanel.OnPreviousQuestionPressed -= _assessmentController.GoToPrevious;
            _assessmentPanel.OnNextQuestionPressed -= _assessmentController.GoToNext;
            _assessmentPanel.OnFinishAssessmentPressed -= _assessmentController.FinishAssessment;

            Destroy(_assessmentPanel.gameObject);
            _assessmentPanel = null;
        }

        private void DestroyResultPanel()
        {
            if (_resultPanel == null) return;

            _resultPanel.OnContinuePressed -= HandleResultContinuePressed;
            _resultPanel.OnRetryPressed -= HandleResultRetryPressed;
            Destroy(_resultPanel.gameObject);
            _resultPanel = null;
        }

        private void DestroyCompletePanel()
        {
            if (_completePanel == null) return;

            _completePanel.OnRestartPressed -= HandleRestartPressed;
            _completePanel.OnChooseAnotherPressed -= HandleChooseAnotherPressed;
            Destroy(_completePanel.gameObject);
            _completePanel = null;
        }

        private void DestroyMenuPanel()
        {
            if (_menuPanel == null) return;

            _menuPanel.OnConfigSelected -= HandleConfigSelected;
            Destroy(_menuPanel.gameObject);
            _menuPanel = null;
        }
    }
}