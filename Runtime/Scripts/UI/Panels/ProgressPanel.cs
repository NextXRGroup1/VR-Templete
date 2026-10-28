using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Training.UI.Progress;

namespace Training.UI.Panels
{
    public class ProgressPanel : UIPanelBase
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI valueText;
        [SerializeField] private Image fillImage;

        private ProgressController _controller;

        public void Wire(TextMeshProUGUI title, TextMeshProUGUI value, Image fill)
        {
            titleText = title;
            valueText = value;
            fillImage = fill;
        }

        public void Bind(ProgressController controller)
        {
            Unbind();
            _controller = controller;
            if (_controller == null) return;
            _controller.OnChanged += OnChanged;
            _controller.OnReset += HandleReset;
            if (titleText != null) titleText.text = _controller.Label;
            OnChanged(_controller.Value, _controller.Normalized);
        }

        public void Unbind()
        {
            if (_controller == null) return;
            _controller.OnChanged -= OnChanged;
            _controller.OnReset -= HandleReset;
            _controller = null;
        }

        private void OnDestroy() => Unbind();

        private void HandleReset()
        {
            if (_controller != null)
                OnChanged(_controller.Value, _controller.Normalized);
        }

        private void OnChanged(float value, float normalized)
        {
            if (fillImage != null) fillImage.fillAmount = normalized;
            if (valueText != null && _controller != null)
                valueText.text = $"{value:0.#} / {_controller.Max:0.#}";
        }
    }
}
