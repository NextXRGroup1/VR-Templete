using System;
using System.Collections.Generic;
using UnityEngine;
using Training.Assessment.Data;

namespace Training.Assessment.Controllers
{
    // Aggregates section results and produces the final score (Section 17).
    // Final score is generated only from recorded assessment results,
    // never directly from UI state (Section 29).
    //
    // CHANGE: keyed by AssessmentId instead of a plain append-only list.
    // This is what makes the Retry button behave sensibly — retrying and
    // passing REPLACES the earlier failed attempt for that same
    // assessment in the final score, rather than both attempts being
    // added together and double-counted.
    public class TrainingScoreController : MonoBehaviour
    {
        public event Action<TrainingScoreData> OnScoreFinalized;

        [Range(0f, 100f)]
        [SerializeField] private float _passThresholdPercent = 60f;

        private readonly Dictionary<string, AssessmentSectionResultData> _sectionResults =
            new Dictionary<string, AssessmentSectionResultData>();

        public void RecordResult(AssessmentSectionResultData result)
        {
            // Last attempt for a given AssessmentId wins — this is the
            // retry-replaces-not-stacks behavior. If two different
            // Assessment nodes intentionally share one AssessmentConfig,
            // their results will also collapse to one entry here; give
            // each reused AssessmentConfig a distinct AssessmentId if that
            // is not what you want.
            _sectionResults[result.AssessmentId] = result;
        }

        public TrainingScoreData FinalizeScore()
        {
            int totalQuestions = 0;
            int correct = 0;
            float earnedTotal = 0f;
            float availableTotal = 0f;

            foreach (var section in _sectionResults.Values)
            {
                totalQuestions += section.TotalQuestions;
                correct += section.CorrectAnswers;

                foreach (var q in section.QuestionResults)
                {
                    earnedTotal += q.PointsEarned;
                    availableTotal += q.PointsAvailable;
                }
            }

            float score = availableTotal > 0f ? (earnedTotal / availableTotal) * 100f : 0f;

            var scoreData = new TrainingScoreData
            {
                TotalQuestions = totalQuestions,
                CorrectAnswers = correct,
                IncorrectAnswers = totalQuestions - correct,
                Score = score,
                Passed = score >= _passThresholdPercent,
                SectionResults = new List<AssessmentSectionResultData>(_sectionResults.Values)
            };

            OnScoreFinalized?.Invoke(scoreData);
            return scoreData;
        }

        public void Reset()
        {
            _sectionResults.Clear();
        }
    }
}