#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Training.Core;
using Training.Question.Data;

namespace Training.EditorTools
{
    // Custom Inspector for QuestionData (drawn wherever a List<QuestionData>
    // shows up, e.g. AssessmentConfig.Questions). Only shows the fields that
    // the current QuestionType actually reads, so you can't be misled by
    // fields like "Correct Option Indices" sitting there unused on a YesNo
    // question. Also hides "Audio Clip" unless "Play Audio" is ticked on.
    //
    // Fields shown per QuestionType:
    //   QuestionAnswer  -> Question Text, Play Audio, [Audio Clip], Correct Answer Text, Points
    //   YesNo           -> Question Text, Play Audio, [Audio Clip], Correct Yes No Answer, Points
    //   TrueFalse       -> Question Text, Play Audio, [Audio Clip], Correct True False Answer, Points
    //   SingleChoice    -> Question Text, Play Audio, [Audio Clip], Options, Correct Option Indices (pick ONE), Points
    //   MultipleChoice  -> Question Text, Play Audio, [Audio Clip], Options, Correct Option Indices (pick ANY), Points
    //
    // Drop this file anywhere under an "Editor" folder (or leave the
    // UNITY_EDITOR guard as-is and put it alongside your other editor
    // scripts) — it must NOT ship in a build, which the #if already ensures.
    [CustomPropertyDrawer(typeof(QuestionData))]
    public class QuestionDataDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var idProp = property.FindPropertyRelative("Id");
            var typeProp = property.FindPropertyRelative("QuestionType");
            var textProp = property.FindPropertyRelative("QuestionText");
            var playAudioProp = property.FindPropertyRelative("PlayAudio");
            var audioClipProp = property.FindPropertyRelative("AudioClip");
            var optionsProp = property.FindPropertyRelative("Options");
            var correctIndicesProp = property.FindPropertyRelative("CorrectOptionIndices");
            var correctAnswerTextProp = property.FindPropertyRelative("CorrectAnswerText");
            var correctYesNoProp = property.FindPropertyRelative("CorrectYesNoAnswer");
            var correctTrueFalseProp = property.FindPropertyRelative("CorrectTrueFalseAnswer");
            var pointsProp = property.FindPropertyRelative("Points");

            var questionType = (QuestionType)typeProp.enumValueIndex;

            float y = position.y;

            // Foldout header shows Id + type + a text preview so the list is scannable when collapsed.
            var headerRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            string preview = string.IsNullOrEmpty(textProp.stringValue) ? "(no question text)" : Truncate(textProp.stringValue, 40);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded,
                $"{(string.IsNullOrEmpty(idProp.stringValue) ? "(no id)" : idProp.stringValue)}  —  {questionType}  —  {preview}", true);
            y += EditorGUIUtility.singleLineHeight + Spacing;

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;

            y = DrawField(position, y, idProp);
            y = DrawField(position, y, typeProp);
            y = DrawField(position, y, textProp);

            y = DrawField(position, y, playAudioProp, "Play Audio");
            if (playAudioProp.boolValue)
                y = DrawField(position, y, audioClipProp, "Audio Clip");

            switch (questionType)
            {
                case QuestionType.QuestionAnswer:
                    y = DrawField(position, y, correctAnswerTextProp);
                    break;

                case QuestionType.YesNo:
                    y = DrawField(position, y, correctYesNoProp, "Correct Answer Is Yes");
                    break;

                case QuestionType.TrueFalse:
                    y = DrawField(position, y, correctTrueFalseProp, "Correct Answer Is True");
                    break;

                case QuestionType.SingleChoice:
                    y = DrawField(position, y, optionsProp, "Options");
                    y = DrawHelpBox(position, y, "Correct Option Indices should contain exactly ONE index (0-based).");
                    y = DrawField(position, y, correctIndicesProp, "Correct Option Index");
                    break;

                case QuestionType.MultipleChoice:
                    y = DrawField(position, y, optionsProp, "Options");
                    y = DrawHelpBox(position, y, "Correct Option Indices may contain multiple indices (0-based). ALL must be selected, and nothing else, to be marked correct.");
                    y = DrawField(position, y, correctIndicesProp, "Correct Option Indices");
                    break;
            }

            y = DrawField(position, y, pointsProp);

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private float DrawField(Rect position, float y, SerializedProperty prop, string labelOverride = null)
        {
            float height = EditorGUI.GetPropertyHeight(prop, true);
            var rect = new Rect(position.x, y, position.width, height);

            if (labelOverride != null)
                EditorGUI.PropertyField(rect, prop, new GUIContent(labelOverride), true);
            else
                EditorGUI.PropertyField(rect, prop, true);

            return y + height + Spacing;
        }

        private float DrawHelpBox(Rect position, float y, string message)
        {
            float height = EditorGUIUtility.singleLineHeight * 1.6f;
            var rect = new Rect(position.x, y, position.width, height);
            EditorGUI.HelpBox(rect, message, MessageType.Info);
            return y + height + Spacing;
        }

        private static string Truncate(string s, int max)
        {
            if (s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight + Spacing; // foldout header

            if (!property.isExpanded) return height;

            var typeProp = property.FindPropertyRelative("QuestionType");
            var playAudioProp = property.FindPropertyRelative("PlayAudio");
            var questionType = (QuestionType)typeProp.enumValueIndex;

            height += FieldHeight(property, "Id");
            height += FieldHeight(property, "QuestionType");
            height += FieldHeight(property, "QuestionText");

            height += FieldHeight(property, "PlayAudio");
            if (playAudioProp.boolValue)
                height += FieldHeight(property, "AudioClip");

            switch (questionType)
            {
                case QuestionType.QuestionAnswer:
                    height += FieldHeight(property, "CorrectAnswerText");
                    break;

                case QuestionType.YesNo:
                    height += FieldHeight(property, "CorrectYesNoAnswer");
                    break;

                case QuestionType.TrueFalse:
                    height += FieldHeight(property, "CorrectTrueFalseAnswer");
                    break;

                case QuestionType.SingleChoice:
                    height += FieldHeight(property, "Options");
                    height += EditorGUIUtility.singleLineHeight * 1.6f + Spacing;
                    height += FieldHeight(property, "CorrectOptionIndices");
                    break;

                case QuestionType.MultipleChoice:
                    height += FieldHeight(property, "Options");
                    height += EditorGUIUtility.singleLineHeight * 1.6f + Spacing;
                    height += FieldHeight(property, "CorrectOptionIndices");
                    break;
            }

            height += FieldHeight(property, "Points");

            return height;
        }

        private float FieldHeight(SerializedProperty parent, string relativeName)
        {
            var prop = parent.FindPropertyRelative(relativeName);
            return EditorGUI.GetPropertyHeight(prop, true) + Spacing;
        }
    }
}
#endif