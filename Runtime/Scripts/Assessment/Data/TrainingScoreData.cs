using System;
using System.Collections.Generic;

namespace Training.Assessment.Data
{
    // Plain runtime result aggregated across every assessment section (Section 18).
    [Serializable]
    public class TrainingScoreData
    {
        public int TotalQuestions;
        public int CorrectAnswers;
        public int IncorrectAnswers;
        public float Score;
        public bool Passed;
        public List<AssessmentSectionResultData> SectionResults = new List<AssessmentSectionResultData>();
    }
}
