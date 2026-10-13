using System;
using System.Collections.Generic;
using UnityEngine;

namespace Training.Sequence
{
    /// <summary>
    /// Drives a linear sequence of steps:
    /// highlight → wait for action → show UI (according to mode) → next step.
    /// When all steps finish it fires OnSequenceCompleted.
    /// Integration with a training framework is done by subscribing to that event.
    /// </summary>
    public class GuidedSequenceController : MonoBehaviour
    {
        [Header("Configuration")]
        public GuidanceMode Mode = GuidanceMode.Guided;
        public List<GuidedStep> Steps = new List<GuidedStep>();

        [Header("References")]
        [SerializeField] private MonoBehaviour highlighterComponent; // must implement IHighlighter
        [SerializeField] private MonoBehaviour uiComponent;          // must implement ISequenceUI

        /// <summary>
        /// Fired when the entire sequence completes successfully.
        /// Subscribe from your training framework to continue the flow.
        /// </summary>
        public event Action OnSequenceCompleted;

        public event Action<int, GuidedStep> OnStepStarted;
        public event Action<int, GuidedStep> OnStepCompleted;

        private IHighlighter _highlighter;
        private ISequenceUI _ui;
        private int _currentIndex = -1;
        private bool _isRunning;

        private void Awake()
        {
            _highlighter = highlighterComponent as IHighlighter;
            _ui = uiComponent as ISequenceUI;

            if (_highlighter == null)
                Debug.LogWarning("GuidedSequenceController: No IHighlighter assigned.");
            if (_ui == null)
                Debug.LogWarning("GuidedSequenceController: No ISequenceUI assigned.");
        }

        /// <summary>
        /// Start the sequence from the beginning.
        /// </summary>
        public void StartSequence()
        {
            if (!ValidateSequence())
                return;

            StopSequence();
            _isRunning = true;
            _currentIndex = 0;
            EnterStep(_currentIndex);
        }

        /// <summary>
        /// Stop and clear everything.
        /// </summary>
        public void StopSequence()
        {
            if (_currentIndex >= 0 && _currentIndex < Steps.Count)
            {
                var step = Steps[_currentIndex];
                if (step.Action != null)
                {
                    step.Action.OnCompleted -= HandleActionCompleted;
                    step.Action.CancelAction();
                }
            }

            _highlighter?.ClearAll();
            _ui?.Hide();
            _currentIndex = -1;
            _isRunning = false;
        }

        private bool ValidateSequence()
        {
            if (Steps == null || Steps.Count == 0)
            {
                Debug.LogError("GuidedSequenceController: Sequence has no steps.");
                return false;
            }

            for (int i = 0; i < Steps.Count; i++)
            {
                var step = Steps[i];

                if (step == null)
                {
                    Debug.LogError($"GuidedSequenceController: Step {i} is null.");
                    return false;
                }

                if (step.Action == null)
                {
                    Debug.LogError($"GuidedSequenceController: Step {i} has no Action.");
                    return false;
                }

                if (step.Action is ISequenceValidatable validatable &&
                    !validatable.Validate(out var error))
                {
                    Debug.LogError($"GuidedSequenceController: Step {i} invalid – {error}");
                    return false;
                }
            }

            return true;
        }

        private void EnterStep(int index)
        {
            if (index < 0 || index >= Steps.Count) return;

            var step = Steps[index];

            // Highlight – target comes from the action when it implements ISequenceTarget
            _highlighter?.ClearAll();
            var target = GetActionTarget(step.Action);
            if (target != null)
                _highlighter?.SetHighlight(target, true);

            // UI according to mode
            if (Mode == GuidanceMode.Guided)
            {
                _ui?.ShowGuided(step.Title, step.Description, step.Hint);
            }
            else
            {
                _ui?.ShowTaskOnly(step.TaskText);
            }

            // Start action
            if (step.Action != null)
            {
                step.Action.OnCompleted += HandleActionCompleted;
                step.Action.StartAction();
            }
            else
            {
                Debug.LogWarning($"GuidedSequenceController: Step {index} has no Action assigned.");
            }

            OnStepStarted?.Invoke(index, step);
        }

        private void HandleActionCompleted()
        {
            if (!_isRunning) return;

            var step = Steps[_currentIndex];
            if (step.Action != null)
                step.Action.OnCompleted -= HandleActionCompleted;

            OnStepCompleted?.Invoke(_currentIndex, step);

            _currentIndex++;

            if (_currentIndex >= Steps.Count)
            {
                FinishSequence();
            }
            else
            {
                EnterStep(_currentIndex);
            }
        }

        private void FinishSequence()
        {
            _highlighter?.ClearAll();
            _ui?.Hide();
            _isRunning = false;
            _currentIndex = -1;

            OnSequenceCompleted?.Invoke();
        }

        private static GameObject GetActionTarget(SequenceActionBase action)
        {
            if (action is ISequenceTarget sequenceTarget)
                return sequenceTarget.Target;
            return null;
        }
    }
}
