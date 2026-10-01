using System;
using System.Collections.Generic;
using UnityEngine;
using Training.Flow.Data;
using Training.Assessment.Controllers;
using Training.Assessment.Data;

namespace Training.Flow.Controllers
{
    // Owns the complete training session lifecycle (Section 6). Does not
    // validate questions and does not render UI directly.
    //
    // CHANGE: no more single fixed _flowConfig field — _availableFlowConfigs
    // is the only place a training is assigned. Auto-start (FlowRunner ->
    // StartSession()) uses index 0. Call StartSession(config) or
    // StartSession(index) to run a specific one instead.
    public class TrainingSessionController : MonoBehaviour
    {
        [Tooltip("Every training this session can run. Index 0 is what auto-starts on launch. A selection menu offers all of them.")]
        [SerializeField] private List<TrainingFlowConfig> _availableFlowConfigs = new List<TrainingFlowConfig>();

        [SerializeField] private TrainingFlowController _flowController;
        [SerializeField] private TrainingScoreController _scoreController;

        public static event Action<TrainingScoreData> OnSessionCompleted;

        // Visit history, used to resolve Back navigation across node boundaries.
        private readonly List<string> _nodeHistory = new List<string>();

        // Whichever config is currently loaded — set by StartSession(config)/
        // StartSession(index), or defaulted to index 0 the first time the
        // no-arg StartSession() runs with nothing set yet.
        private TrainingFlowConfig _currentFlowConfig;

        public IReadOnlyList<TrainingFlowConfig> AvailableFlowConfigs => _availableFlowConfigs;
        public TrainingFlowConfig CurrentFlowConfig => _currentFlowConfig;

        // A picker only makes sense with more than one option — UI can
        // check this to decide whether to ever show a menu at all.
        public bool HasMultipleFlowConfigs => _availableFlowConfigs != null && _availableFlowConfigs.Count > 1;

        private void OnEnable()
        {
            _flowController.OnFlowCompleted += HandleFlowCompleted;
            _flowController.OnBackRequested += HandleBackRequested;
            _flowController.OnShowContentNode += HandleNodeShown;
        }

        private void OnDisable()
        {
            _flowController.OnFlowCompleted -= HandleFlowCompleted;
            _flowController.OnBackRequested -= HandleBackRequested;
            _flowController.OnShowContentNode -= HandleNodeShown;
        }

        // Starts (or restarts) whatever _currentFlowConfig currently is.
        // First call ever, with nothing set yet: defaults to
        // _availableFlowConfigs[0] — this is the auto-start path FlowRunner
        // uses on launch. Later calls (after a Restart or a menu pick)
        // reuse/rerun whatever was set most recently.
        public void StartSession()
        {
            if (_currentFlowConfig == null)
            {
                if (_availableFlowConfigs == null || _availableFlowConfigs.Count == 0)
                {
                    Debug.LogError("TrainingSessionController: no training configured — Available Flow Configs is empty.");
                    return;
                }

                _currentFlowConfig = _availableFlowConfigs[0];
            }

            RunCurrentConfig();
        }

        // Starts a SPECIFIC training by reference — this is what a
        // selection menu calls (or any script that already holds a
        // TrainingFlowConfig reference).
        public void StartSession(TrainingFlowConfig config)
        {
            if (config == null)
            {
                Debug.LogError("TrainingSessionController: StartSession(config) called with a null config.");
                return;
            }

            _currentFlowConfig = config;
            RunCurrentConfig();
        }

        // Starts a SPECIFIC training by its position in Available Flow
        // Configs — convenient for UnityEvents/Inspector-driven calls or a
        // menu keyed by index rather than holding an asset reference.
        public void StartSession(int index)
        {
            if (_availableFlowConfigs == null || index < 0 || index >= _availableFlowConfigs.Count)
            {
                Debug.LogError($"TrainingSessionController: StartSession(index) called with {index}, which is out of range (0-{(_availableFlowConfigs?.Count ?? 0) - 1}).");
                return;
            }

            StartSession(_availableFlowConfigs[index]);
        }

        // Reruns the CURRENT config from the start — the "Restart" button.
        // To run a DIFFERENT training instead, use StartSession(config) or
        // StartSession(index), not this.
        public void RestartSession()
        {
            StartSession();
        }

        private void RunCurrentConfig()
        {
            _scoreController.Reset();
            _nodeHistory.Clear();
            _flowController.Initialize(_currentFlowConfig);
            _flowController.StartFlow();
        }

        private void HandleNodeShown(FlowNodeData node)
        {
            // Only Text/TextImage nodes fire this, which is also the only
            // case Back navigation applies to (Section 12: Assessment
            // "normally None" for its own navigation, Trigger has none).
            _nodeHistory.Add(node.Id);
        }

        private void HandleBackRequested()
        {
            // Drop the current node, then jump to whatever is now on top.
            if (_nodeHistory.Count > 1)
            {
                _nodeHistory.RemoveAt(_nodeHistory.Count - 1);
                var previousNodeId = _nodeHistory[_nodeHistory.Count - 1];
                _flowController.JumpToNode(previousNodeId);
            }
        }

        private void HandleFlowCompleted()
        {
            var finalScore = _scoreController.FinalizeScore();
            RaiseOnSessionCompleted(finalScore);
        }

        public static void RaiseOnSessionCompleted(TrainingScoreData data)
        {
            OnSessionCompleted?.Invoke(data);
        }
    }
}