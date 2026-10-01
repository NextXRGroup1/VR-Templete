using System;
using System.Collections.Generic;
using Training.Question.Data;

namespace Training.Assessment.Data
{
    // Plain runtime result returned through the assessment callback (Section 18).
    [Serializable]
    public class AssessmentSectionResultData
    {
        public string AssessmentId;
        public int TotalQuestions;
        public int CorrectAnswers;
        public int IncorrectAnswers;
        public int AttemptCount;
        public float Score;
        public bool Passed;
        public List<QuestionResultData> QuestionResults = new List<QuestionResultData>();
    }
}
