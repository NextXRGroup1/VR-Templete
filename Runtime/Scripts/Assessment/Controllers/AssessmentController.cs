using System;
using System.Collections.Generic;
using UnityEngine;
using Training.Core;
using Training.Assessment.Data;
using Training.Question.Data;
using Training.Question.Controllers;

namespace Training.Assessment.Controllers
{
    // Runs a complete assessment section and returns a result through a
    // callback. Owns the full lifecycle of one section (Section 33) —
    // it does not decide overall training progression.
    //
    // NavigationMode.Sequential: unchanged — each answer immediately
    // advances to the next question, then completes automatically.
    //
    // NavigationMode.FreeNavigation: answering a question records it and
    // stays put (no auto-advance). The UI drives movement between
    // questions via GoToNext/GoToPrevious/GoToQuestion, and calls
    // FinishAssessment() when the trainee is done. Any question never
    // visited/answered is scored as 0 points, not skipped from totals.
    public class AssessmentController : MonoBehaviour
    {
        [SerializeField] private QuestionController _questionController;

        [Tooltip("Plays each question's AudioClip when that question has Play Audio enabled. Optional — leave unassigned if you don't want question audio.")]
        [SerializeField] private AudioSource _audioSource;

        // (question, index, total) — used by the UI layer to render the current question and progress.
        public event Action<QuestionData, int, int> OnShowQuestion;

        private AssessmentConfig _config;
        private Action<AssessmentSectionResultData> _onCompleted;

        // Keyed by question index rather than append-order, since
        // FreeNavigation can answer questions out of order or re-answer one
        // already visited (last answer for that index wins).
        private readonly Dictionary<int, QuestionResultData> _results = new Dictionary<int, QuestionResultData>();

        private int _currentIndex;
        private bool _callbackInvoked;

        public bool IsFreeNavigation => _config != null && _config.NavigationMode == AssessmentNavigationMode.FreeNavigation;
        public int QuestionCount => _config != null ? _config.Questions.Count : 0;
        public int CurrentIndex => _currentIndex;

        public void StartAssessment(AssessmentConfig config, Action<AssessmentSectionResultData> onCompleted)
        {
            if (!config.Validate(out var error))
            {
                Debug.LogError($"AssessmentController: {error}");
                return;
            }

            _config = config;
            _onCompleted = onCompleted;
            _results.Clear();
            _currentIndex = 0;
            _callbackInvoked = false;

            ShowQuestion(_currentIndex);
        }

        private void ShowQuestion(int index)
        {
            _currentIndex = index;
            var question = _config.Questions[index];
            PlayQuestionAudio(question);
            OnShowQuestion?.Invoke(question, index, _config.Questions.Count);

            // FIX: capture "index" in the closure and pass it straight into
            // OnQuestionAnswered, instead of OnQuestionAnswered reading the
            // mutable _currentIndex field later. QuestionController delays
            // its callback by _feedbackDelaySeconds (the feedback-colour
            // pause) — if the trainee navigates to a different question
            // during that window, _currentIndex changes before the delayed
            // callback fires, and the old code would file the answer under
            // the NEW index instead of the question that was actually
            // answered. Binding the index here makes that impossible.
            _questionController.RunQuestion(question, result => OnQuestionAnswered(index, result));
        }

        // PlayAudio == false -> never plays. PlayAudio == true with no clip
        // assigned, or no _audioSource wired up -> silently does nothing.
        // Neither case is ever treated as an error — same convention
        // TrainingFlowController uses for Text/TextImage/Trigger node audio.
        private void PlayQuestionAudio(QuestionData question)
        {
            if (!question.PlayAudio) return;

            if (question.AudioClip == null)
            {
                Debug.Log($"AssessmentController: question '{question.Id}' has Play Audio enabled but no clip assigned — nothing to play.");
                return;
            }

            if (_audioSource == null)
            {
                Debug.LogWarning($"AssessmentController: question '{question.Id}' wants to play audio but no AudioSource is assigned in the Inspector.");
                return;
            }

            _audioSource.Stop();
            _audioSource.clip = question.AudioClip;
            _audioSource.Play();
        }

        // Called by the UI's Next/Previous/jump controls — FreeNavigation only.
        // Sequential mode ignores this; its order is fixed by design.
        public void GoToQuestion(int index)
        {
            if (!IsFreeNavigation)
            {
                Debug.LogWarning("AssessmentController: GoToQuestion called on a Sequential assessment — ignored.");
                return;
            }

            if (index < 0 || index >= _config.Questions.Count)
            {
                Debug.LogWarning($"AssessmentController: GoToQuestion index {index} out of range.");
                return;
            }

            // FIX: refuse to navigate while the just-submitted answer on the
            // current question is still in its feedback delay. Without this,
            // navigating mid-delay is exactly the scenario the index-capture
            // fix above has to defend against — better to not allow it than
            // to only patch its symptom.
            if (_questionController.IsProcessingAnswer)
            {
                Debug.LogWarning("AssessmentController: GoToQuestion ignored — still showing feedback for the current answer.");
                return;
            }

            ShowQuestion(index);
        }

        public void GoToNext() => GoToQuestion(_currentIndex + 1);
        public void GoToPrevious() => GoToQuestion(_currentIndex - 1);

        // Called by a "Finish"/"Submit Assessment" UI control — FreeNavigation
        // only. Any question not yet answered is scored as 0 points; it is
        // not excluded from TotalQuestions.
        public void FinishAssessment()
        {
            if (!IsFreeNavigation)
            {
                Debug.LogWarning("AssessmentController: FinishAssessment called on a Sequential assessment — ignored (it completes automatically).");
                return;
            }

            // FIX: same reasoning as GoToQuestion above — if the last answer
            // is still mid feedback-delay, its OnQuestionAnswered callback
            // hasn't recorded it into _results yet. Finishing right now
            // would score that question as unanswered even though the
            // trainee did answer it.
            if (_questionController.IsProcessingAnswer)
            {
                Debug.LogWarning("AssessmentController: FinishAssessment ignored — still showing feedback for the current answer.");
                return;
            }

            CompleteAssessment();
        }

        private void OnQuestionAnswered(int answeredIndex, QuestionResultData result)
        {
            _results[answeredIndex] = result;

            if (_config.NavigationMode == AssessmentNavigationMode.Sequential)
            {
                int nextIndex = answeredIndex + 1;
                if (nextIndex < _config.Questions.Count)
                    ShowQuestion(nextIndex);
                else
                    CompleteAssessment();
            }

            // FreeNavigation: stay on the current question after feedback.
            // The trainee moves on via GoToNext/GoToPrevious/GoToQuestion,
            // or finishes early via FinishAssessment().
        }

        private AssessmentSectionResultData CalculateSectionResult()
        {
            int total = _config.Questions.Count;
            int correct = 0;
            float earned = 0f;
            float available = 0f;
            int attempts = 0;
            var allResults = new List<QuestionResultData>(total);

            for (int i = 0; i < total; i++)
            {
                QuestionResultData r;
                if (!_results.TryGetValue(i, out r))
                {
                    // Unanswered (FreeNavigation only — Sequential always
                    // fills every index before it can complete). Counts
                    // toward TotalQuestions and IncorrectAnswers, 0 points.
                    var q = _config.Questions[i];
                    r = new QuestionResultData
                    {
                        QuestionId = q.Id,
                        QuestionType = q.QuestionType,
                        IsCorrect = false,
                        PointsEarned = 0f,
                        PointsAvailable = q.Points,
                        AttemptCount = 0,
                        SubmittedAnswer = null
                    };
                }

                if (r.IsCorrect) correct++;
                earned += r.PointsEarned;
                available += r.PointsAvailable;
                attempts += r.AttemptCount;
                allResults.Add(r);
            }

            // Section 17: weighted points (earned / available), not raw question count.
            float score = available > 0f ? (earned / available) * 100f : 0f;

            return new AssessmentSectionResultData
            {
                AssessmentId = _config.AssessmentId,
                TotalQuestions = total,
                CorrectAnswers = correct,
                IncorrectAnswers = total - correct,
                AttemptCount = attempts,
                Score = score,
                Passed = score >= _config.PassThresholdPercent,
                QuestionResults = allResults
            };
        }

        private void CompleteAssessment()
        {
            // Section 29: an assessment callback must be invoked exactly once.
            if (_callbackInvoked) return;
            _callbackInvoked = true;

            var sectionResult = CalculateSectionResult();
            _onCompleted?.Invoke(sectionResult);
        }
    }
}