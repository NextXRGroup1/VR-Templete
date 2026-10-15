using System;
using System.Collections;
using UnityEngine;
using Training.Core;
using Training.Flow.Data;
using Training.Flow.State;
using Training.Trigger.Controllers;
using Training.Assessment.Controllers;
using Training.Assessment.Data;

namespace Training.Flow.Controllers
{
    // Resolves and advances flow nodes. Does not run individual assessment
    // questions and does not calculate the overall training score.
    public class TrainingFlowController : MonoBehaviour, IFlowController
    {
        [SerializeField] private TriggerFlowController _triggerFlowController;
        [SerializeField] private AssessmentController _assessmentController;
        [SerializeField] private TrainingScoreController _scoreController;

        [Header("Audio")]
        [Tooltip("Plays a node's/assessment's AudioClip when PlayAudio is enabled on it. Optional — leave unassigned if you don't want node audio at all.")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Log every node transition to the Console.")]
        [SerializeField] private bool _verboseLogging = true;

        public event Action OnFlowCompleted;

        // Fires for every node the flow enters (Text, TextImage, Assessment, Trigger).
        public event Action<FlowNodeData> OnNodeEntered;

        // Fires only for Text/TextImage nodes so panels can be built.
        public event Action<FlowNodeData> OnShowContentNode;

        // Fires when a Trigger node starts waiting. UI can show "waiting for X".
        public event Action<TrainingTrigger> OnWaitingForTrigger;

        // Fires when a Navigation.None content node starts its auto-advance
        // countdown. Payload is the delay in seconds. UI can use this to show
        // a countdown/progress ring, or just ignore it — auto-advance works
        // either way.
        public event Action<float> OnAutoAdvanceStarted;

        // Fires once an assessment finishes IF its AssessmentConfig has
        // ShowResultsScreen == true. The flow will not move to the node's
        // NextNodeId until ReportResultsContinue() is called. If
        // ShowResultsScreen is false, this never fires and the flow moves
        // on immediately, same as before this feature existed.
        public event Action<AssessmentSectionResultData> OnShowAssessmentResult;

        public event Action OnBackRequested;

        // Fires whenever CurrentState changes. One place to answer
        // "what is the flow doing right now?" instead of piecing it
        // together from OnShowContentNode / OnWaitingForTrigger / etc.
        public event Action<FlowState> OnStateChanged;
        
        /// <summary>
        /// Animation node: (node, onComplete). Call onComplete when anim should release the flow.
        /// </summary>
        public event Action<FlowNodeData, Action> OnAnimationNode;
        
        public event Action<FlowNodeData> OnActiveNode;

        private TrainingFlowConfig _config;
        private FlowNodeData _currentNode;
        private FlowState _currentState = FlowState.NotStarted;
        private Coroutine _autoAdvanceCoroutine;

        // Set while waiting on a results screen the trainee hasn't
        // dismissed yet. Holds the node whose NextNodeId we resume to.
        private FlowNodeData _pendingResultsNode;

        public string CurrentNodeId => _currentNode?.Id;
        public FlowState CurrentState => _currentState;

        public void Initialize(TrainingFlowConfig config)
        {
            _config = config;
            SetState(FlowState.NotStarted);
        }

        public void StartFlow()
        {
            if (_config == null)
            {
                Debug.LogError("TrainingFlowController: cannot start flow — no config assigned.");
                return;
            }

            if (!_config.Validate(out var error))
            {
                Debug.LogError($"TrainingFlowController: cannot start flow — {error}");
                return;
            }

            Log($"Starting flow at node '{_config.StartNodeId}'.");
            MoveToNode(_config.StartNodeId);
        }

        public void StopFlow()
        {
            CancelAutoAdvance();
            _currentNode = null;
            _pendingResultsNode = null;
            SetState(FlowState.NotStarted);
        }

        private void MoveToNode(string nodeId)
        {
            // Any pending auto-advance belongs to the node we're leaving —
            // always cancel it before moving, whether the move came from the
            // timer itself, a Next click, RaiseTrigger, or JumpToNode.
            CancelAutoAdvance();

            if (string.IsNullOrEmpty(nodeId))
            {
                Log("No NextNodeId — flow complete.");
                CompleteFlow();
                return;
            }

            var node = _config.GetNodeById(nodeId);
            if (node == null)
            {
                Debug.LogError($"TrainingFlowController: referenced node '{nodeId}' does not exist.");
                return;
            }

            _currentNode = node;
            Log($"Entering node '{node.Id}' ({node.NodeType}).");

            // Let the UI react to the node BEFORE it executes, so e.g. the
            // assessment panel is torn down before a Trigger node starts waiting.
            OnNodeEntered?.Invoke(node);

            PlayNodeAudio(node);

            ExecuteNode(node);
        }

        // Text/TextImage/Trigger nodes carry their own PlayAudio + AudioClip.
        // Assessment nodes are skipped here entirely — each question inside
        // plays its own audio via AssessmentController instead, since a
        // single clip for the whole section doesn't fit alongside
        // per-question narration.
        //
        // PlayAudio == false -> never plays. PlayAudio == true with no clip
        // assigned, or no _audioSource wired up -> silently does nothing.
        // Neither case is ever treated as an error.
        private void PlayNodeAudio(FlowNodeData node)
        {
            if (node.NodeType == FlowNodeType.Assessment) return;

            if (!node.PlayAudio) return;

            if (node.AudioClip == null)
            {
                Log($"Node '{node.Id}' has Play Audio enabled but no clip assigned — nothing to play.");
                return;
            }

            if (_audioSource == null)
            {
                Debug.LogWarning($"TrainingFlowController: node '{node.Id}' wants to play audio but no AudioSource is assigned in the Inspector.");
                return;
            }

            _audioSource.Stop();
            _audioSource.clip = node.AudioClip;
            _audioSource.Play();
        }

        private void ExecuteNode(FlowNodeData node)
        {
            switch (node.NodeType)
            {
                case FlowNodeType.Text:
                case FlowNodeType.TextImage:
                    SetState(FlowState.ShowingContent);
                    OnShowContentNode?.Invoke(node);
                    if (node.Navigation == NavigationMode.None)
                        BeginAutoAdvance(node);
                    break;

                case FlowNodeType.Trigger:
                    ExecuteTriggerNode(node);
                    break;

                case FlowNodeType.Assessment:
                    ExecuteAssessmentNode(node);
                    break;
                
                case FlowNodeType.Active:
                    ExecuteActiveNode(node);
                    break;
                
                case FlowNodeType.Animation:
                    ExecuteAnimationNode(node);
                    break;
            }
        }

        // Navigation.None nodes have no Back/Next control, so they either
        // advance instantly (delay == 0, the old behaviour) or sit on screen
        // for AutoAdvanceDelaySeconds and then advance themselves.
        private void BeginAutoAdvance(FlowNodeData node)
        {
            if (node.AutoAdvanceDelaySeconds <= 0f)
            {
                MoveToNode(node.NextNodeId);
                return;
            }

            Log($"Node '{node.Id}' auto-advancing in {node.AutoAdvanceDelaySeconds:0.#}s.");
            OnAutoAdvanceStarted?.Invoke(node.AutoAdvanceDelaySeconds);
            _autoAdvanceCoroutine = StartCoroutine(AutoAdvanceRoutine(node));
        }

        private IEnumerator AutoAdvanceRoutine(FlowNodeData node)
        {
            yield return new WaitForSeconds(node.AutoAdvanceDelaySeconds);
            _autoAdvanceCoroutine = null;
            MoveToNode(node.NextNodeId);
        }

        private void CancelAutoAdvance()
        {
            if (_autoAdvanceCoroutine == null) return;
            StopCoroutine(_autoAdvanceCoroutine);
            _autoAdvanceCoroutine = null;
        }

        private void ExecuteTriggerNode(FlowNodeData node)
        {
            if (node.Trigger == TrainingTrigger.None)
            {
                Debug.LogError($"TrainingFlowController: trigger node '{node.Id}' requires a TrainingTrigger other than None.");
                return;
            }

            if (_triggerFlowController == null)
            {
                Debug.LogError($"TrainingFlowController: node '{node.Id}' is a Trigger node but no TriggerFlowController is assigned in the Inspector. Flow will stop here.");
                return;
            }

            SetState(FlowState.WaitingForTrigger);

            Log($"Node '{node.Id}' is WAITING for TrainingTrigger.{node.Trigger}. " +
                $"Nothing will happen until something calls TriggerFlowController.RaiseTrigger(TrainingTrigger.{node.Trigger}).");

            OnWaitingForTrigger?.Invoke(node.Trigger);

            _triggerFlowController.WaitForTrigger(node.Trigger, () =>
            {
                Log($"Trigger {node.Trigger} received — leaving node '{node.Id}'.");
                MoveToNode(node.NextNodeId);
            });
        }
        
        private void ExecuteAnimationNode(FlowNodeData node)
        {
            Log($"Animation node '{node.Id}'.");

            bool advanced = false;

            void Done()
            {
                if (advanced) return;
                advanced = true;
                MoveToNode(node.NextNodeId);
            }

            if (OnAnimationNode != null)
            {
                OnAnimationNode.Invoke(node, Done);
            }
            else
            {
                Debug.LogWarning(
                    $"TrainingFlowController: Animation node '{node.Id}' but no AnimationNodeController is subscribed.");
                Done();
            }
        }

        private void ExecuteAssessmentNode(FlowNodeData node)
        {
            if (node.AssessmentReference == null)
            {
                Debug.LogError($"TrainingFlowController: assessment node '{node.Id}' has no AssessmentConfig.");
                return;
            }

            if (_assessmentController == null)
            {
                Debug.LogError($"TrainingFlowController: node '{node.Id}' is an Assessment node but no AssessmentController is assigned in the Inspector.");
                return;
            }

            SetState(FlowState.RunningAssessment);
            _assessmentController.StartAssessment(node.AssessmentReference, result => OnAssessmentCompleted(result, node));
        }

        private void ExecuteActiveNode(FlowNodeData node)
        {
            Log($"Active node '{node.Id}' — applying enable/disable bindings.");
            OnActiveNode?.Invoke(node);

            if (node.AutoAdvanceDelaySeconds <= 0f)
                MoveToNode(node.NextNodeId);
            else
                BeginAutoAdvance(node);
        }
        
        private void OnAssessmentCompleted(AssessmentSectionResultData result, FlowNodeData node)
        {
            Log($"Assessment '{result.AssessmentId}' finished: {result.CorrectAnswers}/{result.TotalQuestions} correct, score {result.Score:0.#}%, passed={result.Passed}. Next node = '{node.NextNodeId}'.");

            if (_scoreController != null)
                _scoreController.RecordResult(result);
            else
                Debug.LogWarning("TrainingFlowController: no TrainingScoreController assigned — result not recorded.");

            // Flexible per-assessment: only pause for a results screen if
            // this assessment's config asks for one. Everything else about
            // the flow is unaffected either way.
            if (node.AssessmentReference.ShowResultsScreen)
            {
                _pendingResultsNode = node;
                SetState(FlowState.ShowingResults);
                Log($"Showing results screen for '{result.AssessmentId}' — waiting for ReportResultsContinue().");
                OnShowAssessmentResult?.Invoke(result);
                return;
            }

            MoveToNode(node.NextNodeId);
        }

        // Called by the UI layer (e.g. a "Continue" button on the results
        // screen) once the trainee has seen the assessment result. No-op if
        // nothing is currently waiting on it.
        public void ReportResultsContinue()
        {
            if (_pendingResultsNode == null) return;

            var nextNodeId = _pendingResultsNode.NextNodeId;
            _pendingResultsNode = null;
            MoveToNode(nextNodeId);
        }

        // Called by the UI layer (e.g. a "Retry" button on the results
        // screen) to re-run the SAME assessment node from question 1,
        // instead of moving on to NextNodeId. Re-entering the node via
        // MoveToNode gives it a fresh AssessmentController.StartAssessment
        // call (clean _results, _callbackInvoked reset) exactly as if the
        // trainee had arrived at it for the first time.
        public void ReportResultsRetry()
        {
            if (_pendingResultsNode == null) return;

            var nodeId = _pendingResultsNode.Id;
            _pendingResultsNode = null;
            MoveToNode(nodeId);
        }

        // Called by the UI layer when the user presses Next on a content panel.
        public void ReportNext()
        {
            if (_currentNode == null) return;
            MoveToNode(_currentNode.NextNodeId);
        }

        // Called by the UI layer when the user presses Back on a content panel.
        public void ReportBack()
        {
            OnBackRequested?.Invoke();
        }

        // Used by TrainingSessionController once it resolves a previous node.
        public void JumpToNode(string nodeId)
        {
            MoveToNode(nodeId);
        }

        private void CompleteFlow()
        {
            _currentNode = null;
            SetState(FlowState.Completed);
            OnFlowCompleted?.Invoke();
        }

        private void SetState(FlowState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            Log($"State -> {newState}");
            OnStateChanged?.Invoke(newState);
        }

        private void Log(string message)
        {
            if (_verboseLogging) Debug.Log($"[Flow] {message}");
        }
    }
}