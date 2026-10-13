#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Training.Flow.Data;

namespace Training.EditorTools
{
    [CustomEditor(typeof(TrainingFlowConfig))]
    public class TrainingFlowConfigEditor : UnityEditor.Editor
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

            EditorGUILayout.LabelField("Training", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(titleProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Completion Screen", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showCompletionProp, new GUIContent("Show Completion Screen"));

            if (showCompletionProp.boolValue)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(showFinalQuizProp, new GUIContent("Show Final Quiz Panel"));

                using (new EditorGUI.DisabledScope(showFinalQuizProp.boolValue))
                {
                    EditorGUILayout.PropertyField(calculateScoreProp, new GUIContent("Calculate Score"));
                }

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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Flow", GUILayout.Height(28)))
            {
                var config = (TrainingFlowConfig)target;
                if (config.Validate(out var error))
                {
                    var nodeCount = config.Nodes != null ? config.Nodes.Count : 0;
                    EditorUtility.DisplayDialog(
                        "Validation",
                        $"Flow is valid.\n\n{nodeCount} nodes\nStart: {config.StartNodeId}",
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Validation Failed", error, "OK");
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
