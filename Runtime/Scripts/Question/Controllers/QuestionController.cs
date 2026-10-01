using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Training.Core;
using Training.Question.Data;

namespace Training.Question.Controllers
{
    // Runs one question and returns its result. Does not know about the
    // overall training score or which assessment it belongs to.
    public class QuestionController : MonoBehaviour
    {
        [Tooltip("How long to hold on the answered question (showing feedback) before advancing.")]
        [SerializeField] private float _feedbackDelaySeconds = 0.2f;

        // Fired the instant an answer is validated, before the feedback
        // delay � lets the UI colour the selected option immediately.
        public event Action<QuestionResultData> OnAnswerValidated;

        // Fired when the feedback delay actually finishes (right as
        // IsProcessingAnswer goes back to false). Lets the UI re-enable
        // Free Navigation controls at the correct moment instead of
        // guessing a duration — clicking Next/Previous/Finish during the
        // delay used to fail silently (AssessmentController logs a warning
        // and ignores it); this is what makes them visibly unclickable
        // for that window instead.
        public event Action OnFeedbackDelayElapsed;

        private QuestionData _currentQuestion;
        private Action<QuestionResultData> _onCompleted;
        private int _attemptCount;
        private bool _isProcessingAnswer;

        // Exposed so callers that can navigate away mid-feedback (e.g.
        // AssessmentController's FreeNavigation Previous/Next/Finish) can
        // check before acting instead of racing the delayed callback.
        public bool IsProcessingAnswer => _isProcessingAnswer;

        public void RunQuestion(QuestionData question, Action<QuestionResultData> onCompleted)
        {
            _currentQuestion = question;
            _onCompleted = onCompleted;
            _attemptCount = 0;
            _isProcessingAnswer = false;
        }

        // Called by QuestionPanel / choice controls when the user submits an answer.
        // rawAnswer conventions: QuestionAnswer -> free text; YesNo/TrueFalse/SingleChoice
        // -> selected option index as string; MultipleChoice -> comma-separated indices.
        public void ReceiveAnswer(string rawAnswer)
        {
            // Ignore extra taps while feedback for a previous answer is still showing.
            if (_isProcessingAnswer) return;
            _isProcessingAnswer = true;

            _attemptCount++;
            bool isCorrect = ValidateAnswer(rawAnswer);
            var result = BuildResult(rawAnswer, isCorrect);

            OnAnswerValidated?.Invoke(result);
            StartCoroutine(AdvanceAfterDelay(result));
        }

        private IEnumerator AdvanceAfterDelay(QuestionResultData result)
        {
            yield return new WaitForSeconds(_feedbackDelaySeconds);
            _isProcessingAnswer = false;
            OnFeedbackDelayElapsed?.Invoke();
            _onCompleted?.Invoke(result);
        }

        private bool ValidateAnswer(string rawAnswer)
        {
            switch (_currentQuestion.QuestionType)
            {
                case QuestionType.QuestionAnswer:
                    return string.Equals(rawAnswer?.Trim(), _currentQuestion.CorrectAnswerText?.Trim(),
                        StringComparison.OrdinalIgnoreCase);

                // FIX: YesNo/TrueFalse options are submitted as the selected
                // option's index ("0" or "1"), same as SingleChoice � the
                // previous version tried bool.TryParse on that index string,
                // which always failed, so these two types were always marked
                // incorrect regardless of the chosen answer.
                case QuestionType.YesNo:
                    return int.TryParse(rawAnswer, out var yesNoIndex) &&
                           (yesNoIndex == 0) == _currentQuestion.CorrectYesNoAnswer; // index 0 = "Yes"

                case QuestionType.TrueFalse:
                    return int.TryParse(rawAnswer, out var trueFalseIndex) &&
                           (trueFalseIndex == 0) == _currentQuestion.CorrectTrueFalseAnswer; // index 0 = "True"

                case QuestionType.SingleChoice:
                    return int.TryParse(rawAnswer, out var singleIndex) &&
                           _currentQuestion.CorrectOptionIndices.Contains(singleIndex);

                case QuestionType.MultipleChoice:
                    if (string.IsNullOrEmpty(rawAnswer)) return false;
                    var selected = rawAnswer.Split(',')
                        .Where(s => int.TryParse(s, out _))
                        .Select(int.Parse)
                        .OrderBy(i => i);
                    var correct = _currentQuestion.CorrectOptionIndices.OrderBy(i => i);
                    return selected.SequenceEqual(correct);

                default:
                    return false;
            }
        }

        private QuestionResultData BuildResult(string rawAnswer, bool isCorrect)
        {
            return new QuestionResultData
            {
                QuestionId = _currentQuestion.Id,
                QuestionType = _currentQuestion.QuestionType,
                IsCorrect = isCorrect,
                PointsEarned = isCorrect ? _currentQuestion.Points : 0f,
                PointsAvailable = _currentQuestion.Points,
                AttemptCount = _attemptCount,
                SubmittedAnswer = rawAnswer
            };
        }
    }
}