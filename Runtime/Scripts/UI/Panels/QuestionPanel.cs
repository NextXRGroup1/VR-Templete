using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Training.Core;
using Training.Question.Data;
using TMPro;

namespace Training.UI.Panels
{
    // Generic question presentation shell.
    //
    // CHANGES vs the old version:
    //  - No Toggle / radio-button / checkmark anywhere. The green square you
    //    were seeing was the Toggle's checkmark Image (it was literally
    //    created with Color.green) and the green-on-hover was Unity's default
    //    ColorTint transition on Button/Toggle. Both are now killed at spawn
    //    time (transition = None), so even an OLD prefab behaves correctly
    //    without regenerating it.
    //  - Each option now shows an index number (1. 2. 3. 4.).
    //  - MultipleChoice selection is tracked in this script (a HashSet)
    //    instead of reading Toggle.isOn. The old code read
    //    _optionsContainer.childCount at submit time, but Destroy() is
    //    deferred to end-of-frame, so old options were still counted as
    //    children -> wrong indices. Fixed by using _spawnedOptions.
    //  - Feedback for YesNo / TrueFalse now uses CorrectYesNoAnswer /
    //    CorrectTrueFalseAnswer. The old code used CorrectOptionIndices,
    //    which is empty for those types, so it never revealed the answer.
    public class QuestionPanel : UIPanelBase
    {
        [SerializeField] private TextMeshProUGUI _questionText;
        [SerializeField] private Transform _optionsContainer;
        [SerializeField] private GameObject _choiceOptionPrefab;
        [SerializeField] private TMP_InputField _freeTextInput;
        [SerializeField] private Button _submitButton;

        [Header("Option numbering")]
        [Tooltip("Show 1. 2. 3. 4. in front of each option.")]
        [SerializeField] private bool _showOptionIndex = true;
        [Tooltip("{0} = 1-based number. e.g. \"{0}.\"  \"{0})\"  \"[{0}]\"")]
        [SerializeField] private string _indexFormat = "{0}.";

        [Header("Option colors")]
        [SerializeField] private Color _normalColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color _selectedColor = new Color(0.22f, 0.35f, 0.55f, 1f);

        [Header("Answer feedback")]
        [Tooltip("Turn OFF if you don't want options tinted green/red after answering.")]
        [SerializeField] private bool _useFeedbackColors = true;
        [Tooltip("Also colour the correct option when the user got it wrong.")]
        [SerializeField] private bool _revealCorrectAnswer = true;
        [SerializeField] private Color _correctColor = new Color(0.20f, 0.60f, 0.25f, 1f);
        [SerializeField] private Color _incorrectColor = new Color(0.70f, 0.20f, 0.20f, 1f);

        public event Action<string> OnAnswerSubmitted;

        private QuestionData _currentQuestion;
        private readonly List<OptionView> _spawnedOptions = new List<OptionView>();
        private readonly HashSet<int> _selectedIndices = new HashSet<int>();

        private class OptionView
        {
            public GameObject Root;
            public Image Background;
            public Button Button;
        }

        private void Awake()
        {
            if (_submitButton != null)
                _submitButton.onClick.AddListener(SubmitCurrentAnswer);
        }

        public void SetQuestion(QuestionData question)
        {
            _currentQuestion = question;
            if (_questionText != null) _questionText.text = question.QuestionText;

            ClearOptions();
            _selectedIndices.Clear();
            ResetFreeTextColor();

            switch (question.QuestionType)
            {
                case QuestionType.QuestionAnswer:
                    if (_freeTextInput != null)
                    {
                        _freeTextInput.text = string.Empty;
                        _freeTextInput.gameObject.SetActive(true);
                    }
                    if (_submitButton != null) _submitButton.gameObject.SetActive(true);
                    break;

                case QuestionType.YesNo:
                    BuildOptions(new[] { "Yes", "No" }, multiSelect: false);
                    break;

                case QuestionType.TrueFalse:
                    BuildOptions(new[] { "True", "False" }, multiSelect: false);
                    break;

                case QuestionType.SingleChoice:
                    BuildOptions(question.Options.ToArray(), multiSelect: false);
                    break;

                case QuestionType.MultipleChoice:
                    BuildOptions(question.Options.ToArray(), multiSelect: true);
                    break;
            }

            SetInteractable(true);
        }

        private void BuildOptions(string[] labels, bool multiSelect)
        {
            if (_freeTextInput != null) _freeTextInput.gameObject.SetActive(false);
            // Single-answer types submit on tap, so no Submit button needed.
            if (_submitButton != null) _submitButton.gameObject.SetActive(multiSelect);

            if (_choiceOptionPrefab == null || _optionsContainer == null)
            {
                Debug.LogError("QuestionPanel: _choiceOptionPrefab or _optionsContainer is not assigned.");
                return;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                int index = i; // capture for the closure
                var go = Instantiate(_choiceOptionPrefab, _optionsContainer);
                go.SetActive(true);

                var view = new OptionView
                {
                    Root = go,
                    Background = go.GetComponent<Image>(),
                    Button = go.GetComponentInChildren<Button>(true)
                };

                StripLegacyToggle(go);
                SetOptionLabel(go, labels[index], index);

                if (view.Background != null) view.Background.color = _normalColor;

                if (view.Button != null)
                {
                    // Kills the green highlight/pressed tint on hover.
                    view.Button.transition = Selectable.Transition.None;
                    view.Button.onClick.RemoveAllListeners();

                    if (multiSelect)
                        view.Button.onClick.AddListener(() => ToggleSelection(index));
                    else
                        view.Button.onClick.AddListener(() => SelectSingle(index));
                }

                _spawnedOptions.Add(view);
            }
        }

        // Old prefabs shipped a Toggle on a bright-green "Checkmark" child.
        // Hide it so existing prefabs stop showing the green box.
        private void StripLegacyToggle(GameObject optionGO)
        {
            var toggle = optionGO.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
                toggle.gameObject.SetActive(false);
        }

        private void SelectSingle(int index)
        {
            _selectedIndices.Clear();
            _selectedIndices.Add(index);
            RefreshSelectionColors();
            OnAnswerSubmitted?.Invoke(index.ToString());
        }

        private void ToggleSelection(int index)
        {
            if (!_selectedIndices.Remove(index))
                _selectedIndices.Add(index);

            RefreshSelectionColors();
        }

        private void RefreshSelectionColors()
        {
            for (int i = 0; i < _spawnedOptions.Count; i++)
            {
                var bg = _spawnedOptions[i].Background;
                if (bg == null) continue;
                bg.color = _selectedIndices.Contains(i) ? _selectedColor : _normalColor;
            }
        }

        // Works with TextMeshProUGUI or legacy Text.
        // If the prefab has a child named "Index", the number goes there;
        // otherwise it is prefixed onto the label.
        private void SetOptionLabel(GameObject optionGO, string label, int index)
        {
            string number = _showOptionIndex ? string.Format(_indexFormat, index + 1) : string.Empty;

            var indexTransform = optionGO.transform.Find("Index");
            if (indexTransform != null && _showOptionIndex)
            {
                WriteText(indexTransform.gameObject, number, searchChildren: true);
                WriteLabelText(optionGO, label, skip: indexTransform.gameObject);
                return;
            }

            string composed = _showOptionIndex ? $"{number} {label}" : label;
            WriteLabelText(optionGO, composed, skip: null);
        }

        private void WriteLabelText(GameObject optionGO, string value, GameObject skip)
        {
            var labelTransform = optionGO.transform.Find("Label");
            if (labelTransform != null)
            {
                WriteText(labelTransform.gameObject, value, searchChildren: true);
                return;
            }

            foreach (var tmp in optionGO.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (skip != null && tmp.gameObject == skip) continue;
                tmp.text = value;
                return;
            }

            foreach (var txt in optionGO.GetComponentsInChildren<Text>(true))
            {
                if (skip != null && txt.gameObject == skip) continue;
                txt.text = value;
                return;
            }
        }

        private void WriteText(GameObject target, string value, bool searchChildren)
        {
            var tmp = searchChildren
                ? target.GetComponentInChildren<TextMeshProUGUI>(true)
                : target.GetComponent<TextMeshProUGUI>();
            if (tmp != null) { tmp.text = value; return; }

            var legacy = searchChildren
                ? target.GetComponentInChildren<Text>(true)
                : target.GetComponent<Text>();
            if (legacy != null) legacy.text = value;
        }

        private void SubmitCurrentAnswer()
        {
            if (_currentQuestion == null) return;

            if (_currentQuestion.QuestionType == QuestionType.QuestionAnswer)
            {
                OnAnswerSubmitted?.Invoke(_freeTextInput != null ? _freeTextInput.text : string.Empty);
                return;
            }

            if (_currentQuestion.QuestionType == QuestionType.MultipleChoice)
            {
                var ordered = _selectedIndices.OrderBy(i => i).Select(i => i.ToString());
                OnAnswerSubmitted?.Invoke(string.Join(",", ordered));
            }
        }

        // Called by UIFlowController right after QuestionController.OnAnswerValidated.
        public void ShowFeedback(QuestionResultData result)
        {
            SetInteractable(false);

            if (!_useFeedbackColors) return;

            switch (_currentQuestion.QuestionType)
            {
                case QuestionType.YesNo:
                case QuestionType.TrueFalse:
                case QuestionType.SingleChoice:
                    ShowSingleSelectFeedback(result);
                    break;

                case QuestionType.MultipleChoice:
                    ShowMultiSelectFeedback(result);
                    break;

                case QuestionType.QuestionAnswer:
                    ShowFreeTextFeedback(result.IsCorrect);
                    break;
            }
        }

        private void ShowSingleSelectFeedback(QuestionResultData result)
        {
            if (!int.TryParse(result.SubmittedAnswer, out var selectedIndex)) return;

            for (int i = 0; i < _spawnedOptions.Count; i++)
            {
                var image = _spawnedOptions[i].Background;
                if (image == null) continue;

                bool isSelected = i == selectedIndex;
                bool isCorrectOption = IsCorrectOptionIndex(i);

                if (isSelected)
                    image.color = result.IsCorrect ? _correctColor : _incorrectColor;
                else if (_revealCorrectAnswer && isCorrectOption && !result.IsCorrect)
                    image.color = _correctColor;
                // everything else stays _normalColor — nothing else turns green
            }
        }

        private void ShowMultiSelectFeedback(QuestionResultData result)
        {
            for (int i = 0; i < _spawnedOptions.Count; i++)
            {
                var image = _spawnedOptions[i].Background;
                if (image == null) continue;

                bool isCorrectOption = IsCorrectOptionIndex(i);
                bool wasSelected = _selectedIndices.Contains(i);

                if (wasSelected)
                    image.color = isCorrectOption ? _correctColor : _incorrectColor;
                else if (_revealCorrectAnswer && isCorrectOption && !result.IsCorrect)
                    image.color = _correctColor;
            }
        }

        // YesNo / TrueFalse store their answer in a bool, not CorrectOptionIndices.
        // Option 0 = "Yes" / "True", option 1 = "No" / "False".
        private bool IsCorrectOptionIndex(int index)
        {
            switch (_currentQuestion.QuestionType)
            {
                case QuestionType.YesNo:
                    return (index == 0) == _currentQuestion.CorrectYesNoAnswer;
                case QuestionType.TrueFalse:
                    return (index == 0) == _currentQuestion.CorrectTrueFalseAnswer;
                default:
                    return _currentQuestion.CorrectOptionIndices != null &&
                           _currentQuestion.CorrectOptionIndices.Contains(index);
            }
        }

        private void ShowFreeTextFeedback(bool isCorrect)
        {
            var inputImage = _freeTextInput != null ? _freeTextInput.GetComponent<Image>() : null;
            if (inputImage != null)
                inputImage.color = isCorrect ? _correctColor : _incorrectColor;
        }

        private void ResetFreeTextColor()
        {
            var inputImage = _freeTextInput != null ? _freeTextInput.GetComponent<Image>() : null;
            if (inputImage != null)
                inputImage.color = Color.white;
        }

        private void SetInteractable(bool interactable)
        {
            foreach (var view in _spawnedOptions)
            {
                if (view.Button != null) view.Button.interactable = interactable;
            }

            if (_submitButton != null) _submitButton.interactable = interactable;
            if (_freeTextInput != null) _freeTextInput.interactable = interactable;
        }

        private void ClearOptions()
        {
            // Detach BEFORE Destroy — Destroy is deferred to end of frame,
            // so the old objects would otherwise still be children this frame.
            foreach (var view in _spawnedOptions)
            {
                if (view.Root == null) continue;
                view.Root.transform.SetParent(null, false);
                Destroy(view.Root);
            }
            _spawnedOptions.Clear();

            if (_optionsContainer == null) return;
            for (int i = _optionsContainer.childCount - 1; i >= 0; i--)
            {
                var child = _optionsContainer.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                Destroy(child);
            }
        }
    }
}