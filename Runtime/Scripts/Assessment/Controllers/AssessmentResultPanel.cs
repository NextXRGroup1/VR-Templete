using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Training.Assessment.Data;

namespace Training.UI.Panels
{
    // Shown after an assessment finishes, only when that assessment's
    // AssessmentConfig.ShowResultsScreen is true. Purely presentational —
    // it doesn't decide pass/fail or advance the flow itself; it just
    // displays whatever AssessmentSectionResultData it's given and reports
    // when the trainee is ready to continue.
    public class AssessmentResultPanel : UIPanelBase
    {
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private TextMeshProUGUI _detailText;
        [SerializeField] private GameObject _passBadge;
        [SerializeField] private GameObject _failBadge;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _retryButton;

        [Header("Optional pass/fail colouring")]
        [SerializeField] private Color _passColor = new Color(0.20f, 0.60f, 0.25f, 1f);
        [SerializeField] private Color _failColor = new Color(0.70f, 0.20f, 0.20f, 1f);

        public event Action OnContinuePressed;
        public event Action OnRetryPressed;

        private void Awake()
        {
            if (_continueButton != null)
                _continueButton.onClick.AddListener(() => OnContinuePressed?.Invoke());

            if (_retryButton != null)
                _retryButton.onClick.AddListener(() => OnRetryPressed?.Invoke());
        }

        public void SetResult(AssessmentSectionResultData result)
        {
            if (_titleText != null)
                _titleText.text = result.Passed ? "Assessment Passed" : "Assessment Failed";

            if (_scoreText != null)
            {
                _scoreText.text = $"{result.Score:0.#}%";
                _scoreText.color = result.Passed ? _passColor : _failColor;
            }

            if (_detailText != null)
                _detailText.text = $"{result.CorrectAnswers} / {result.TotalQuestions} correct";

            if (_passBadge != null) _passBadge.SetActive(result.Passed);
            if (_failBadge != null) _failBadge.SetActive(!result.Passed);
        }
    }
}