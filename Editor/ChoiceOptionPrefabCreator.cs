#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Training.EditorTools
{
    // Generates the "ChoiceOption" prefab used by QuestionPanel.
    //   Tools > Training > Generate ChoiceOption Prefab
    //
    // CHANGES vs the old version:
    //  - The Toggle and its bright-green "Checkmark" Image are GONE. That
    //    green square (and the green-on-hover) was the whole problem.
    //  - Button transition is set to None, so hovering/pressing does not
    //    tint the option at all. Selection colour is driven by QuestionPanel.
    //  - Added an "Index" child that shows 1. 2. 3. 4.
    //  - Labels use TextMeshProUGUI to match the rest of the UI.
    //
    // Resulting hierarchy (QuestionPanel looks for these exact child names):
    //   ChoiceOption   [Image + Button]
    //     Index        [TextMeshProUGUI]
    //     Label        [TextMeshProUGUI]
    public static class ChoiceOptionPrefabCreator
    {
        private const string OutputFolder = "Assets/Prefabs/UI";
        private const string PrefabPath = OutputFolder + "/ChoiceOption.prefab";

        [MenuItem("Tools/Training/Generate ChoiceOption Prefab")]
        public static void Generate()
        {
            EnsureFolderExists();

            var root = new GameObject("ChoiceOption", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(420f, 56f);

            var image = root.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            // No ColorTint -> no green (or any) hover/press highlight.
            button.transition = Selectable.Transition.None;

            // ---- Index ("1.") ----
            var indexGO = new GameObject("Index", typeof(RectTransform));
            indexGO.transform.SetParent(root.transform, false);
            var indexRect = indexGO.GetComponent<RectTransform>();
            indexRect.anchorMin = new Vector2(0f, 0f);
            indexRect.anchorMax = new Vector2(0f, 1f);
            indexRect.pivot = new Vector2(0f, 0.5f);
            indexRect.offsetMin = new Vector2(12f, 0f);
            indexRect.offsetMax = new Vector2(0f, 0f);
            indexRect.sizeDelta = new Vector2(40f, 0f);

            var indexText = indexGO.AddComponent<TextMeshProUGUI>();
            indexText.text = "1.";
            indexText.fontSize = 24f;
            indexText.alignment = TextAlignmentOptions.MidlineLeft;
            indexText.color = new Color(0.65f, 0.70f, 0.80f, 1f);
            indexText.raycastTarget = false;

            // ---- Label ----
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(root.transform, false);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(58f, 4f);
            labelRect.offsetMax = new Vector2(-12f, -4f);

            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "Option";
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = Color.white;
            label.raycastTarget = false;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.Refresh();
            Debug.Log($"ChoiceOption prefab created at {PrefabPath}. Assign it to QuestionPanel's 'Choice Option Prefab' field.");
        }

        private static void EnsureFolderExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }
    }
}
#endif