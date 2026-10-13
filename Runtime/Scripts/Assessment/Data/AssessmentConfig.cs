using System.Collections.Generic;
using UnityEngine;
using Training.Core;
using Training.Question.Data;

namespace Training.Assessment.Data
{
    [CreateAssetMenu(fileName = "AssessmentConfig", menuName = "Training/Assessment/Assessment Config")]
    public class AssessmentConfig : ScriptableObject
    {
        public string AssessmentId;

        public bool ShowResultsScreen = true;

        public List<QuestionData> Questions = new List<QuestionData>();
        public AssessmentNavigationMode NavigationMode = AssessmentNavigationMode.Sequential;

        [Range(0f, 100f)]
        public float PassThresholdPercent = 60f;

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(AssessmentId))
            {
                error = "AssessmentId cannot be empty.";
                return false;
            }

            if (Questions == null || Questions.Count == 0)
            {
                error = $"AssessmentConfig '{AssessmentId}' must contain at least one question.";
                return false;
            }

            var questionIds = new HashSet<string>();

            foreach (var q in Questions)
            {
                if (q == null)
                {
                    error = $"AssessmentConfig '{AssessmentId}' contains a null question.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(q.Id))
                {
                    error = $"AssessmentConfig '{AssessmentId}' has a question with empty Id.";
                    return false;
                }

                if (!questionIds.Add(q.Id))
                {
                    error = $"Duplicate question Id '{q.Id}' in AssessmentConfig '{AssessmentId}'.";
                    return false;
                }

                if (q.Points < 0f)
                {
                    error = $"Question '{q.Id}' has negative Points.";
                    return false;
                }

                switch (q.QuestionType)
                {
                    case QuestionType.QuestionAnswer:
                        if (string.IsNullOrWhiteSpace(q.CorrectAnswerText))
                        {
                            error = $"Question '{q.Id}' (QuestionAnswer) has empty CorrectAnswerText.";
                            return false;
                        }
                        break;

                    case QuestionType.SingleChoice:
                    case QuestionType.MultipleChoice:
                        if (q.Options == null || q.Options.Count == 0)
                        {
                            error = $"Question '{q.Id}' is a choice question but has no options.";
                            return false;
                        }
                        if (q.CorrectOptionIndices == null || q.CorrectOptionIndices.Count == 0)
                        {
                            error = $"Question '{q.Id}' has no CorrectOptionIndices.";
                            return false;
                        }
                        foreach (var idx in q.CorrectOptionIndices)
                        {
                            if (idx < 0 || idx >= q.Options.Count)
                            {
                                error = $"Question '{q.Id}' has CorrectOptionIndex {idx} out of range (options: {q.Options.Count}).";
                                return false;
                            }
                        }
                        break;
                }
            }

            error = null;
            return true;
        }
    }
}
