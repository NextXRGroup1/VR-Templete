using System;
using UnityEngine;
using UnityEngine.UI;
using Training.Core;
using TMPro;

namespace Training.UI.Panels
{
    // Display text together with an image (Section 23).
    public class TextImagePanel : UIPanelBase
    {
        [SerializeField] private TextMeshProUGUI _bodyText;
        [SerializeField] private Image _image;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _backButton;

        public event Action OnNextPressed;
        public event Action OnBackPressed;

        private void Awake()
        {
            if (_nextButton != null) _nextButton.onClick.AddListener(() => OnNextPressed?.Invoke());
            if (_backButton != null) _backButton.onClick.AddListener(() => OnBackPressed?.Invoke());
        }

        public void SetContent(string text, Sprite image, NavigationMode navigation)
        {
            _bodyText.text = text;
            _image.sprite = image;

            if (_nextButton != null)
                _nextButton.gameObject.SetActive(navigation == NavigationMode.Next || navigation == NavigationMode.BackNext);

            if (_backButton != null)
                _backButton.gameObject.SetActive(navigation == NavigationMode.BackNext);
        }
    }
}
