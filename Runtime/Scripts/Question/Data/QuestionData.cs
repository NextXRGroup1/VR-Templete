using System;
using System.Collections.Generic;
using UnityEngine;
using Training.Core;

namespace Training.Question.Data
{
    // Configuration for one question. QuestionType belongs to the question
    // itself (Section 13) so an AssessmentConfig can mix types freely.
    [Serializable]
    public class QuestionData
    {
        public string Id;
        public QuestionType QuestionType;

        [TextArea]
        public string QuestionText;

        // Per-question audio (e.g. narrated question text). Independent of
        // AssessmentConfig's own PlayAudio/AudioClip — that one plays once
        // for the whole assessment, this one plays per question.
        // PlayAudio == false -> never plays, AudioClip is ignored/hidden.
        // PlayAudio == true with no clip assigned -> plays nothing, not an error.
        public bool PlayAudio;
        public AudioClip AudioClip;

        // Used by SingleChoice / MultipleChoice
        public List<string> Options = new List<string>();
        public List<int> CorrectOptionIndices = new List<int>();

        // Used by QuestionAnswer
        public string CorrectAnswerText;

        // Used by YesNo
        public bool CorrectYesNoAnswer;

        // Used by TrueFalse
        public bool CorrectTrueFalseAnswer;

        [Min(0f)]
        public float Points = 1f;
    }
}