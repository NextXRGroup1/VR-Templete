using System;
using Training.Core;

namespace Training.Question.Data
{
    // Plain runtime result. Not a MonoBehaviour/ScriptableObject.
    [Serializable]
    public class QuestionResultData
    {
        public string QuestionId;
        public QuestionType QuestionType;
        public bool IsCorrect;
        public float PointsEarned;
        public float PointsAvailable;
        public int AttemptCount;
        public string SubmittedAnswer;
    }
}
