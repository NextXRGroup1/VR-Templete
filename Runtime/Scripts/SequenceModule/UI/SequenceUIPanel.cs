using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Training.Sequence
{
    /// <summary>
    /// Simple UI panel that implements ISequenceUI.
    /// Assign the text fields in the Inspector.
    /// </summary>
    public class SequenceUIPanel : MonoBehaviour, ISequenceUI
    {
        [Header("References")]
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private TextMeshProUGUI taskText;

        public void ShowGuided(string title, string description, string hint)
        {
            if (root != null) root.SetActive(true);

            SetText(titleText, title);
            SetText(descriptionText, description);
            SetText(hintText, hint);
            SetText(taskText, null);          // hide task-only field in guided mode
        }

        public void ShowTaskOnly(string task)
        {
            if (root != null) root.SetActive(true);

            SetText(titleText, null);
            SetText(descriptionText, null);
            SetText(hintText, null);
            SetText(taskText, task);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label == null) return;

            bool hasValue = !string.IsNullOrEmpty(value);
            label.gameObject.SetActive(hasValue);
            if (hasValue) label.text = value;
        }
    }
}
