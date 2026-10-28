using UnityEngine;
using TMPro;

namespace Training.UI.Panels
{
    public class ChecklistRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private GameObject checkMark;
        [SerializeField] private GameObject emptyMark;

        public string Id { get; private set; }

        public void Setup(string id, string label, bool isChecked)
        {
            Id = id;
            if (labelText != null) labelText.text = label;
            SetChecked(isChecked);
        }

        public void SetChecked(bool on)
        {
            if (checkMark != null) checkMark.SetActive(on);
            if (emptyMark != null) emptyMark.SetActive(!on);
        }

        public void Wire(TextMeshProUGUI label, GameObject check, GameObject empty)
        {
            labelText = label;
            checkMark = check;
            emptyMark = empty;
        }
    }
}
