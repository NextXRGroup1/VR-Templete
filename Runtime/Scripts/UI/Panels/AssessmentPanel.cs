using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Training.UI.Panels
{
    // Presents assessment state (progress) and hosts the QuestionPanel
    // (Section 23). Assessment rules/flow rules are not owned here � the
    // Previous/Next/Finish buttons below only report clicks; UIFlowController
    // decides whether they're relevant and wires them to AssessmentController.
    public class AssessmentPanel : UIPanelBase
    {
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private QuestionPanel _questionPanel;

        [Header("Free Navigation controls (optional � leave unassigned for Sequential-only prefabs)")]
        [SerializeField] private GameObject _freeNavigationControls; // parent object to show/hide as a group
        [SerializeField] private Button _previousQuestionButton;
        [SerializeField] private Button _nextQuestionButton;
        [SerializeField] private Button _finishAssessmentButton;

        public QuestionPanel QuestionPanel => _questionPanel;

        public event Action OnPreviousQuestionPressed;
        public event Action OnNextQuestionPressed;
        public event Action OnFinishAssessmentPressed;

        private void Awake()
        {
            if (_previousQuestionButton != null)
                _previousQuestionButton.onClick.AddListener(() => OnPreviousQuestionPressed?.Invoke());

            if (_nextQuestionButton != null)
                _nextQuestionButton.onClick.AddListener(() => OnNextQuestionPressed?.Invoke());

            if (_finishAssessmentButton != null)
                _finishAssessmentButton.onClick.AddListener(() => OnFinishAssessmentPressed?.Invoke());
        }

        public void SetProgress(int currentIndex, int totalQuestions)
        {
            if (_progressText != null)
                _progressText.text = $"Question {currentIndex + 1} / {totalQuestions}";
        }

        // Called once by UIFlowController right after this panel is created,
        // based on AssessmentController.IsFreeNavigation. Sequential
        // assessments never show these; nothing else needs to know why.
        public void SetFreeNavigationControlsVisible(bool visible)
        {
            if (_freeNavigationControls != null)
                _freeNavigationControls.SetActive(visible);
        }

        // Called every time a new question is shown, so Next hides itself
        // on the last question — Finish is the way forward from there
        // instead. Back/Finish are left alone here on purpose. Hidden via
        // SetActive rather than interactable=false, so it's fully gone
        // rather than visible-but-grayed-out.
        public void SetNextButtonVisible(bool visible)
        {
            if (_nextQuestionButton != null)
                _nextQuestionButton.gameObject.SetActive(visible);
        }

        // Called around the (short) feedback-color window only. Stays
        // visible the whole time — just briefly non-clickable so a click
        // during that window doesn't silently do nothing, without the
        // button disappearing on every single answer.
        public void SetNextButtonInteractable(bool interactable)
        {
            if (_nextQuestionButton != null)
                _nextQuestionButton.interactable = interactable;
        }
    }
}