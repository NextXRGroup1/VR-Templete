#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Training.Assessment.Data;

namespace Training.EditorTools
{
    // Custom Inspector for the AssessmentConfig ScriptableObject asset.
    // - "Show Results Screen" controls whether TrainingFlowController pauses
    //   after this assessment to show a score/pass-fail screen before
    //   continuing the flow — no extra fields needed for it, it's a
    //   standalone on/off switch.
    //
    // CHANGE: the "Audio" section (Play Audio / Audio Clip) was removed —
    // those fields no longer exist on AssessmentConfig. Audio is now
    // per-question (QuestionData.PlayAudio/AudioClip, visible per-question
    // in the Questions list below), since a single clip for the whole
    // section didn't fit alongside per-question narration. This is what
    // was throwing the NullReferenceException: FindProperty("PlayAudio")
    // and FindProperty("AudioClip") returned null once those fields were
    // removed, and PropertyField on a null property crashes the whole
    // Inspector, not just that one field.
    [CustomEditor(typeof(AssessmentConfig))]
    public class AssessmentConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var idProp = serializedObject.FindProperty("AssessmentId");
            var showResultsProp = serializedObject.FindProperty("ShowResultsScreen");
            var questionsProp = serializedObject.FindProperty("Questions");
            var navigationModeProp = serializedObject.FindProperty("NavigationMode");
            var passThresholdProp = serializedObject.FindProperty("PassThresholdPercent");

            EditorGUILayout.PropertyField(idProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showResultsProp, new GUIContent("Show Results Screen"));
            EditorGUILayout.HelpBox(
                showResultsProp.boolValue
                    ? "The flow will pause after this assessment and show a score/pass-fail screen with a Continue button."
                    : "The flow will move straight to the next node after this assessment — no results screen.",
                MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Questions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Play Audio / Audio Clip now live on each individual question below, not here.",
                MessageType.None);
            EditorGUILayout.PropertyField(navigationModeProp);
            EditorGUILayout.PropertyField(passThresholdProp);
            EditorGUILayout.PropertyField(questionsProp, true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif