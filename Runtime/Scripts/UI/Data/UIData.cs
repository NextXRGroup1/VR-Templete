using System;
using UnityEngine;

namespace Training.UI.Data
{
    [Serializable]
    public class TextPanelData
    {
        public string Text;
    }

    [Serializable]
    public class TextImagePanelData
    {
        public string Text;
        public Sprite Image;
    }

    // System-level editable UI settings (Section 19: ScriptableObject + Settings).
    [CreateAssetMenu(fileName = "TrainingUISettings", menuName = "Training/UI/Training UI Settings")]
    public class TrainingUISettings : ScriptableObject
    {
        [Min(0f)] public float PanelFadeDuration = 0.25f;
        public bool AutoAdvanceOnTrigger = true;
    }
}
