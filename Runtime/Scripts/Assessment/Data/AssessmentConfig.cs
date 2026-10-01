using System.Collections.Generic;
using UnityEngine;
using Training.Core;
using Training.Question.Data;

namespace Training.Assessment.Data
{
    // Designer-editable assessment and question configuration (Section 19).
    // A collection of heterogeneous questions — each question owns its own
    // QuestionType (Section 13).
    [CreateAssetMenu(fileName = "AssessmentConfig", menuName = "Training/Assessment/Assessment Config")]
    public class AssessmentConfig : ScriptableObject
    {
        public string AssessmentId;

        // NOTE: assessment-level PlayAudio/AudioClip removed — audio is now
        // per-question (QuestionData.PlayAudio/AudioClip, played by
        // AssessmentController when each question is shown), since a
        // single clip for the whole section didn't fit alongside per-
        // question narration.

        // When true, TrainingFlowController pauses right after this
        // assessment finishes and shows an AssessmentResultPanel (score,
        // correct/incorrect, pass/fail) with a Continue button, before
        // moving on to the flow node's NextNodeId. When false, the flow
        // behaves exactly as before — straight through to the next node,
        // no results screen at all. Per-assessment, so some sections can
        // show results and others skip it.
        public bool ShowResultsScreen = true;

        public List<QuestionData> Questions = new List<QuestionData>();
        public AssessmentNavigationMode NavigationMode = AssessmentNavigationMode.Sequential;

        [Range(0f, 100f)]
        public float PassThresholdPercent = 60f;

        // Section 29 validation rules.
        public bool Validate(out string error)
        {
            if (Questions == null || Questions.Count == 0)
            {
                error = $"AssessmentConfig '{AssessmentId}' must contain at least one question.";
                return false;
            }

            foreach (var q in Questions)
            {
                if ((q.QuestionType == QuestionType.SingleChoice || q.QuestionType == QuestionType.MultipleChoice)
                    && (q.Options == null || q.Options.Count == 0))
                {
                    error = $"Question '{q.Id}' is a choice question but has no options.";
                    return false;
                }
            }

            // Deliberately NOT validating PlayAudio/AudioClip here — a
            // PlayAudio == true with no clip assigned is a valid, silent
            // configuration, not an error. Same for ShowResultsScreen — it
            // needs nothing else assigned to be valid.

            error = null;
            return true;
        }
    }
}