#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Training.Flow.Data;

namespace Training.EditorTools
{
    // Custom Inspector for the TrainingFlowConfig ScriptableObject asset.
    // Without this, ShowCompletionScreen / CalculateScore / ShowFinalQuizPanel /
    // CompletionImage all just fall through to the default Inspector and
    // show unconditionally - including CalculateScore/ShowFinalQuizPanel/
    // CompletionImage when ShowCompletionScreen is off, where they mean
    // nothing at all. This hides what doesn't currently apply, same pattern
    // as AssessmentConfigEditor.
    [CustomEditor(typeof(TrainingFlowConfig))]
    public class TrainingFlowConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var titleProp = serializedObject.FindProperty("Title");
            var showCompletionProp = serializedObject.FindProperty("ShowCompletionScreen");
            var calculateScoreProp = serializedObject.FindProperty("CalculateScore");
            var showFinalQuizProp = serializedObject.FindProperty("ShowFinalQuizPanel");
            var completionImageProp = serializedObject.FindProperty("CompletionImage");
            var startNodeIdProp = serializedObject.FindProperty("StartNodeId");
            var nodesProp = serializedObject.FindProperty("Nodes");

            EditorGUILayout.PropertyField(titleProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Completion Screen", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showCompletionProp, new GUIContent("Show Completion Screen"));

            if (showCompletionProp.boolValue)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(showFinalQuizProp, new GUIContent("Show Final Quiz Panel"));

                // CalculateScore is meaningless once ShowFinalQuizPanel is on
                // (that layout never shows score text regardless) - shown
                // greyed out rather than hidden, so it's clear it still HAS
                // a value, just isn't being used right now.
                using (new EditorGUI.DisabledScope(showFinalQuizProp.boolValue))
                {
                    EditorGUILayout.PropertyField(calculateScoreProp, new GUIContent("Calculate Score"));
                }

                // Completion Image is exclusive to the Final Quiz Panel
                // layout - only shown (and therefore only settable) once
                // that's turned on, same reasoning as hiding it entirely
                // when ShowCompletionScreen itself is off.
                if (showFinalQuizProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(completionImageProp, new GUIContent("Completion Image"));
                    EditorGUI.indentLevel--;
                }

                if (showFinalQuizProp.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        "Final Quiz Panel layout: completion image + Retry/Home/Quiz buttons only. " +
                        "No title/score/detail text is shown, and Calculate Score is ignored while this is on.",
                        MessageType.None);
                }
                else if (calculateScoreProp.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        "Full layout: title, score %, detail text, pass/fail badge, Retry + optional Choose Another. " +
                        "No completion image in this layout - turn on Show Final Quiz Panel to use one.",
                        MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Minimal layout: Retry only - no score text, no badges, no completion image.",
                        MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Flow", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(startNodeIdProp);
            EditorGUILayout.PropertyField(nodesProp, true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif