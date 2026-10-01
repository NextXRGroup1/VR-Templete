#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Training.Core;
using Training.Flow.Data;

namespace Training.EditorTools
{
    // Custom drawer for FlowNodeData — collapses each node into a foldout
    // (Id — NodeType) and, when expanded, shows only the fields that
    // NodeType actually uses. Mirrors QuestionDataDrawer's approach for
    // QuestionData, applied to flow nodes instead of questions.
    //
    // Field visibility by NodeType:
    //   Text        -> Id, NodeType, NextNodeId, Navigation, [Auto Advance Delay if Navigation == None],
    //                  Play Audio, [Audio Clip if Play Audio], TextContent
    //   TextImage   -> same as Text, plus Image
    //   Trigger     -> Id, NodeType, NextNodeId, Trigger, Play Audio, [Audio Clip if Play Audio]
    //   Assessment  -> Id, NodeType, NextNodeId, AssessmentReference
    //                  (its own Play Audio / Audio Clip live on the AssessmentConfig asset, not here)
    [CustomPropertyDrawer(typeof(FlowNodeData))]
    public class FlowNodeDataDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;
        private const float HelpBoxHeight = 32f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var idProp = property.FindPropertyRelative("Id");
            var nodeTypeProp = property.FindPropertyRelative("NodeType");
            var nextNodeIdProp = property.FindPropertyRelative("NextNodeId");
            var triggerProp = property.FindPropertyRelative("Trigger");
            var navigationProp = property.FindPropertyRelative("Navigation");
            var delayProp = property.FindPropertyRelative("AutoAdvanceDelaySeconds");
            var playAudioProp = property.FindPropertyRelative("PlayAudio");
            var audioClipProp = property.FindPropertyRelative("AudioClip");
            var textContentProp = property.FindPropertyRelative("TextContent");
            var imageProp = property.FindPropertyRelative("Image");
            var assessmentRefProp = property.FindPropertyRelative("AssessmentReference");

            var nodeType = (FlowNodeType)nodeTypeProp.enumValueIndex;
            var navigation = (NavigationMode)navigationProp.enumValueIndex;
            string preview = string.IsNullOrEmpty(idProp.stringValue) ? "(no id)" : idProp.stringValue;

            var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, $"{preview}  —  {nodeType}", true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            float y = position.y + EditorGUIUtility.singleLineHeight + Spacing;

            y = DrawField(position, y, idProp);
            y = DrawField(position, y, nodeTypeProp);
            y = DrawField(position, y, nextNodeIdProp);

            switch (nodeType)
            {
                case FlowNodeType.Text:
                    y = DrawField(position, y, navigationProp);
                    if (navigation == NavigationMode.None)
                        y = DrawField(position, y, delayProp, "Auto Advance Delay Seconds");
                    y = DrawAudioFields(position, y, playAudioProp, audioClipProp);
                    y = DrawField(position, y, textContentProp);
                    break;

                case FlowNodeType.TextImage:
                    y = DrawField(position, y, navigationProp);
                    if (navigation == NavigationMode.None)
                        y = DrawField(position, y, delayProp, "Auto Advance Delay Seconds");
                    y = DrawAudioFields(position, y, playAudioProp, audioClipProp);
                    y = DrawField(position, y, textContentProp);
                    y = DrawField(position, y, imageProp);
                    break;

                case FlowNodeType.Assessment:
                    y = DrawField(position, y, assessmentRefProp);
                    y = DrawHelpBox(position, y,
                        "Assessment nodes control their own question navigation — Navigation is ignored here. " +
                        "Play Audio / Audio Clip for an assessment are set on the Assessment Config asset itself.",
                        MessageType.None);
                    break;

                case FlowNodeType.Trigger:
                    y = DrawField(position, y, triggerProp);
                    if ((TrainingTrigger)triggerProp.enumValueIndex == TrainingTrigger.None)
                    {
                        y = DrawHelpBox(position, y,
                            "Trigger must not be None — the flow will log an error at runtime for this node.",
                            MessageType.Warning);
                    }
                    y = DrawAudioFields(position, y, playAudioProp, audioClipProp);
                    break;
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        // Play Audio checkbox, and Audio Clip only when it's ticked on.
        // Leaving Audio Clip unassigned while Play Audio is true is valid —
        // it just means nothing plays. No warning needed for that case.
        private float DrawAudioFields(Rect position, float y, SerializedProperty playAudioProp, SerializedProperty audioClipProp)
        {
            y = DrawField(position, y, playAudioProp, "Play Audio");

            if (playAudioProp.boolValue)
                y = DrawField(position, y, audioClipProp, "Audio Clip");

            return y;
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

        private float DrawHelpBox(Rect position, float y, string message, MessageType type)
        {
            var rect = new Rect(position.x, y, position.width, HelpBoxHeight);
            EditorGUI.HelpBox(rect, message, type);
            return y + HelpBoxHeight + Spacing;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            var nodeTypeProp = property.FindPropertyRelative("NodeType");
            var triggerProp = property.FindPropertyRelative("Trigger");
            var navigationProp = property.FindPropertyRelative("Navigation");
            var playAudioProp = property.FindPropertyRelative("PlayAudio");
            var nodeType = (FlowNodeType)nodeTypeProp.enumValueIndex;
            var navigation = (NavigationMode)navigationProp.enumValueIndex;

            float height = EditorGUIUtility.singleLineHeight + Spacing; // foldout
            height += FieldHeight(property, "Id");
            height += FieldHeight(property, "NodeType");
            height += FieldHeight(property, "NextNodeId");

            switch (nodeType)
            {
                case FlowNodeType.Text:
                    height += FieldHeight(property, "Navigation");
                    if (navigation == NavigationMode.None)
                        height += FieldHeight(property, "AutoAdvanceDelaySeconds");
                    height += AudioFieldsHeight(property, playAudioProp);
                    height += FieldHeight(property, "TextContent");
                    break;

                case FlowNodeType.TextImage:
                    height += FieldHeight(property, "Navigation");
                    if (navigation == NavigationMode.None)
                        height += FieldHeight(property, "AutoAdvanceDelaySeconds");
                    height += AudioFieldsHeight(property, playAudioProp);
                    height += FieldHeight(property, "TextContent");
                    height += FieldHeight(property, "Image");
                    break;

                case FlowNodeType.Assessment:
                    height += FieldHeight(property, "AssessmentReference");
                    height += HelpBoxHeight + Spacing;
                    break;

                case FlowNodeType.Trigger:
                    height += FieldHeight(property, "Trigger");
                    if ((TrainingTrigger)triggerProp.enumValueIndex == TrainingTrigger.None)
                        height += HelpBoxHeight + Spacing;
                    height += AudioFieldsHeight(property, playAudioProp);
                    break;
            }

            return height;
        }

        private float AudioFieldsHeight(SerializedProperty property, SerializedProperty playAudioProp)
        {
            float height = FieldHeight(property, "PlayAudio");
            if (playAudioProp.boolValue)
                height += FieldHeight(property, "AudioClip");
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