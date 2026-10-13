#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Training.Sequence.Editor
{
    [CustomPropertyDrawer(typeof(GuidedStep))]
    public class GuidedStepDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                float y = position.y + EditorGUIUtility.singleLineHeight + 2f;
                float line = EditorGUIUtility.singleLineHeight;
                float gap = 2f;

                var taskProp = property.FindPropertyRelative("TaskText");
                var titleProp = property.FindPropertyRelative("Title");
                var descProp = property.FindPropertyRelative("Description");
                var hintProp = property.FindPropertyRelative("Hint");
                var actionProp = property.FindPropertyRelative("Action");

                var r = new Rect(position.x, y, position.width, line);
                EditorGUI.PropertyField(r, taskProp, new GUIContent("Task Text"));
                y += line + gap;

                EditorGUI.LabelField(new Rect(position.x, y, position.width, line), "Guided Mode Only", EditorStyles.boldLabel);
                y += line + gap;

                r = new Rect(position.x, y, position.width, line);
                EditorGUI.PropertyField(r, titleProp);
                y += line + gap;

                float descHeight = EditorGUI.GetPropertyHeight(descProp);
                r = new Rect(position.x, y, position.width, descHeight);
                EditorGUI.PropertyField(r, descProp);
                y += descHeight + gap;

                r = new Rect(position.x, y, position.width, line);
                EditorGUI.PropertyField(r, hintProp);
                y += line + gap;

                EditorGUI.LabelField(new Rect(position.x, y, position.width, line), "Action", EditorStyles.boldLabel);
                y += line + gap;

                r = new Rect(position.x, y, position.width, line);
                EditorGUI.PropertyField(r, actionProp);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float line = EditorGUIUtility.singleLineHeight;
            float gap = 2f;
            float height = line + gap;

            height += (line + gap); // Task
            height += (line + gap); // Guided header
            height += (line + gap); // Title

            var descProp = property.FindPropertyRelative("Description");
            height += EditorGUI.GetPropertyHeight(descProp) + gap;

            height += (line + gap); // Hint
            height += (line + gap); // Action header
            height += line;         // Action

            return height;
        }
    }
}
#endif
