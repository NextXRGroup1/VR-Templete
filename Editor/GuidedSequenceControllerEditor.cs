#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace Training.Sequence.Editor
{
    [CustomEditor(typeof(GuidedSequenceController))]
    public class GuidedSequenceControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty _modeProp;
        private SerializedProperty _stepsProp;
        private SerializedProperty _highlighterProp;
        private SerializedProperty _uiProp;

        private void OnEnable()
        {
            _modeProp = serializedObject.FindProperty("Mode");
            _stepsProp = serializedObject.FindProperty("Steps");
            _highlighterProp = serializedObject.FindProperty("highlighterComponent");
            _uiProp = serializedObject.FindProperty("uiComponent");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var controller = (GuidedSequenceController)target;

            EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_modeProp);

            if ((GuidanceMode)_modeProp.enumValueIndex == GuidanceMode.Guided)
            {
                EditorGUILayout.HelpBox(
                    "Guided: Title, Description and Hint are shown.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Non-Guided: Only Task Text is shown. Detailed UI is disabled.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_highlighterProp, new GUIContent("Highlighter"));
            EditorGUILayout.PropertyField(_uiProp, new GUIContent("Sequence UI"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Steps", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_stepsProp, true);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Sequence", GUILayout.Height(28)))
            {
                Validate(controller);
            }

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("Start Sequence (Play Mode)", GUILayout.Height(28)))
            {
                controller.StartSequence();
            }
            if (GUILayout.Button("Stop Sequence (Play Mode)", GUILayout.Height(28)))
            {
                controller.StopSequence();
            }
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }

        private void Validate(GuidedSequenceController controller)
        {
            var errors = new List<string>();

            if (controller.Steps == null || controller.Steps.Count == 0)
            {
                errors.Add("Sequence has no steps.");
            }
            else
            {
                for (int i = 0; i < controller.Steps.Count; i++)
                {
                    var step = controller.Steps[i];

                    if (step == null)
                    {
                        errors.Add($"Step {i}: Step is null.");
                        continue;
                    }

                    if (step.Action == null)
                    {
                        errors.Add($"Step {i}: Action is missing.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(step.TaskText))
                        errors.Add($"Step {i}: Task Text is empty (required for Non-Guided mode).");

                    if (step.Action is ISequenceValidatable validatable)
                    {
                        if (!validatable.Validate(out var actionError))
                            errors.Add($"Step {i}: {actionError}");
                    }
                }
            }

            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation",
                    $"Sequence is valid.\n\n{controller.Steps.Count} steps ready.", "OK");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Sequence Validation Failed");
            sb.AppendLine();
            foreach (var e in errors)
                sb.AppendLine("❌ " + e);
            sb.AppendLine();
            sb.Append($"{errors.Count} error(s)");

            EditorUtility.DisplayDialog("Validation", sb.ToString(), "OK");
        }
    }
}
#endif
