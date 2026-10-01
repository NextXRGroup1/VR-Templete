using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Training.Assessment.Data;

namespace Training.UI.Panels
{
    // Shown once the ENTIRE training session finishes (TrainingSessionController.
    // OnSessionCompleted) - distinct from AssessmentResultPanel, which shows
    // after one assessment SECTION. This is the final screen, and is what
    // makes repeat/kiosk-style sessions possible: its Retry button is what
    // actually calls TrainingSessionController.RestartSession() again.
    //
    // Two layouts, driven by TrainingFlowConfig.CalculateScore / ShowFinalQuizPanel:
    //   - Full detail (CalculateScore, ShowFinalQuizPanel off): title, score %,
    //     detail text, pass/fail badge, Retry + optional Choose Another.
    //   - Final Quiz Panel (ShowFinalQuizPanel on - takes priority over
    //     CalculateScore): completion image + Retry/Home/Quiz buttons only,
    //     no score text at all. Completion image is EXCLUSIVE to this layout
    //     - the full-detail layout never shows it. Home/Quiz only ever fire
    //     their events here - no behaviour is wired to them; that's the
    //     consuming project's job.
    public class TrainingCompletePanel : UIPanelBase
    {
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private TextMeshProUGUI _detailText;
        [SerializeField] private GameObject _passBadge;
        [SerializeField] private GameObject _failBadge;


        [Tooltip("Shared by both layouts - always visible whenever the panel is shown.")]
        [SerializeField] private Button _restartButton; // labelled "Retry" in the UI

        [Tooltip("Optional. Leave unassigned if this app only ever runs one training with no picker - TrainingSessionController.HasMultipleFlowConfigs decides whether this shows. Never shown in the Final Quiz Panel layout regardless.")]
        [SerializeField] private Button _chooseAnotherButton;

        [Header("Final Quiz Panel layout (optional - leave unassigned if you never use ShowFinalQuizPanel)")]
        [Tooltip("Parent object grouping the Home and Quiz buttons - shown/hidden as a set.")]
        [SerializeField] private GameObject _finalQuizControls;
        [SerializeField] private Image _completionImage;
        [SerializeField] private Button _homeButton;
        [SerializeField] private Button _quizButton;

        [Header("Colors")]
        [SerializeField] private Color _passColor = new Color(0.2f, 0.7f, 0.2f);
        [SerializeField] private Color _failColor = new Color(0.7f, 0.2f, 0.2f);

        public event Action OnRestartPressed;
        public event Action OnChooseAnotherPressed;

        // Fire only - deliberately no behaviour wired to these anywhere in
        // this package. Subscribe to them yourself wherever Home/Quiz
        // actually need to go in your project.
        public event Action OnHomePressed;
        public event Action OnQuizPressed;

        private bool _isFinalQuizPanelMode;

        private void Awake()
        {
            if (_restartButton != null)
                _restartButton.onClick.AddListener(() => OnRestartPressed?.Invoke());

            if (_chooseAnotherButton != null)
                _chooseAnotherButton.onClick.AddListener(() => OnChooseAnotherPressed?.Invoke());

            if (_homeButton != null)
                _homeButton.onClick.AddListener(() => OnHomePressed?.Invoke());

            if (_quizButton != null)
                _quizButton.onClick.AddListener(() => OnQuizPressed?.Invoke());
        }

        // Called once by UIFlowController right after this panel is shown,
        // based on TrainingSessionController.HasMultipleFlowConfigs - only
        // worth offering "choose another" if there's actually more than one.
        // Suppressed entirely while in Final Quiz Panel mode (call SetResult
        // first, then this, so the mode is already known).
        public void SetChooseAnotherVisible(bool visible)
        {
            if (_chooseAnotherButton != null)
                _chooseAnotherButton.gameObject.SetActive(visible && !_isFinalQuizPanelMode);
        }

        public void SetResult(TrainingScoreData data, bool calculateScore, bool showFinalQuizPanel, Sprite completionImage)
        {
            _isFinalQuizPanelMode = showFinalQuizPanel;

            // showFinalQuizPanel takes priority - it's a different layout,
            // not a modifier on top of the score-details one.
            bool showFullDetails = calculateScore && !showFinalQuizPanel;

            // Simple mode: image + buttons only. All the score/title/detail
            // elements are fully hidden, not just emptied, so no leftover
            // layout space or text flicker from a previous session.
            SetActiveIfAssigned(_titleText, showFullDetails);
            SetActiveIfAssigned(_scoreText, showFullDetails);
            SetActiveIfAssigned(_detailText, showFullDetails);
            if (_passBadge != null) _passBadge.SetActive(showFullDetails && data.Passed);
            if (_failBadge != null) _failBadge.SetActive(showFullDetails && !data.Passed);

            if (showFullDetails)
            {
                if (_titleText != null)
                    _titleText.text = data.Passed ? "Training Complete" : "Training Not Passed";

                if (_scoreText != null)
                {
                    _scoreText.text = $"{data.Score:0.#}%";
                    _scoreText.color = data.Passed ? _passColor : _failColor;
                }

                if (_detailText != null)
                {
                    _detailText.text = $"{data.CorrectAnswers} / {data.TotalQuestions} correct across " +
                                        $"{data.SectionResults.Count} assessment section(s).";
                }
            }

            // Home/Quiz only ever appear together, only in Final Quiz Panel
            // mode. Retry (_restartButton) is shared by both layouts and is
            // never touched here - it stays visible either way.
            if (_finalQuizControls != null)
                _finalQuizControls.SetActive(showFinalQuizPanel);

            // Choose Another was possibly already set by a prior
            // SetChooseAnotherVisible call this session - re-apply the
            // suppression now that _isFinalQuizPanelMode is known, in case
            // SetResult is called after it rather than before.
            if (_chooseAnotherButton != null && showFinalQuizPanel)
                _chooseAnotherButton.gameObject.SetActive(false);

            // Completion image is Final Quiz Panel-only now — the full
            // detail layout never shows it, regardless of whether a sprite
            // is assigned on the config.
            if (_completionImage != null)
            {
                bool hasImage = showFinalQuizPanel && completionImage != null;
                _completionImage.gameObject.SetActive(hasImage);
                if (hasImage) _completionImage.sprite = completionImage;
            }
        }

        private static void SetActiveIfAssigned(TextMeshProUGUI text, bool active)
        {
            if (text != null) text.gameObject.SetActive(active);
        }
    }
}