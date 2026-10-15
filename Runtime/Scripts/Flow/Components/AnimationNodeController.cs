using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Training.Core;
using Training.Flow.Controllers;
using Training.Flow.Data;

namespace Training.Flow.Components
{
    [Serializable]
    public class AnimationNodeBinding
    {
        [Tooltip("Must match FlowNodeData.Id for an Animation node.")]
        public string NodeId;

        [Tooltip("Object that has an Animator.")]
        public GameObject Target;

        [Tooltip("Animator Trigger parameter name.")]
        public string TriggerKey;

        [Tooltip("Optional state to wait on. If empty, uses current state after trigger.")]
        public string StateName;

        public TrainingAnimationPlayMode PlayMode = TrainingAnimationPlayMode.Once;

        [Range(1, 100)]
        [Tooltip("For OnceStopAtPercent (and early end). 100 = full clip.")]
        public int StopAtPercent = 100;

        [Tooltip("Animator layer index.")]
        public int Layer = 0;

        [Min(0f)]
        [Tooltip("Safety timeout in seconds. 0 = no timeout.")]
        public float TimeoutSeconds = 30f;

        [Tooltip("If true, flow advances immediately without waiting for the animation.")]
        public bool FireAndContinue = false;
    }

    public class AnimationNodeController : MonoBehaviour
    {
        [SerializeField] private TrainingFlowController flowController;
        [SerializeField] private List<AnimationNodeBinding> bindings = new List<AnimationNodeBinding>();

        private Coroutine _running;

        private void OnEnable()
        {
            if (flowController != null)
                flowController.OnAnimationNode += HandleAnimationNode;
        }

        private void OnDisable()
        {
            if (flowController != null)
                flowController.OnAnimationNode -= HandleAnimationNode;
            StopRunning();
        }

        private void HandleAnimationNode(FlowNodeData node, Action onComplete)
        {
            if (node == null || string.IsNullOrEmpty(node.Id))
            {
                onComplete?.Invoke();
                return;
            }

            var binding = FindBinding(node.Id);
            if (binding == null)
            {
                Debug.LogWarning($"[AnimationNode] No binding for node '{node.Id}'.");
                onComplete?.Invoke();
                return;
            }

            StopRunning();
            _running = StartCoroutine(PlayRoutine(binding, onComplete));
        }

        private AnimationNodeBinding FindBinding(string nodeId)
        {
            if (bindings == null) return null;

            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i] != null && bindings[i].NodeId == nodeId)
                    return bindings[i];
            }

            return null;
        }

        private IEnumerator PlayRoutine(AnimationNodeBinding b, Action onComplete)
        {
            Animator animator = b.Target != null ? b.Target.GetComponent<Animator>() : null;
            if (animator == null)
            {
                Debug.LogError($"[AnimationNode] No Animator on target for node '{b.NodeId}'.");
                onComplete?.Invoke();
                yield break;
            }

            if (!string.IsNullOrEmpty(b.TriggerKey))
                animator.SetTrigger(b.TriggerKey);

            if (b.FireAndContinue)
            {
                onComplete?.Invoke();

                if (b.PlayMode != TrainingAnimationPlayMode.Loop)
                    yield return WaitForEnd(animator, b);

                _running = null;
                yield break;
            }

            yield return WaitForEnd(animator, b);
            onComplete?.Invoke();
            _running = null;
        }

        private IEnumerator WaitForEnd(Animator animator, AnimationNodeBinding b)
        {
            yield return null;

            float timeout = b.TimeoutSeconds > 0f ? b.TimeoutSeconds : float.MaxValue;
            float elapsed = 0f;
            float targetNorm = Mathf.Clamp01(b.StopAtPercent / 100f);

            if (!string.IsNullOrEmpty(b.StateName))
            {
                while (elapsed < timeout)
                {
                    var info = animator.GetCurrentAnimatorStateInfo(b.Layer);
                    if (info.IsName(b.StateName))
                        break;

                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            if (b.PlayMode == TrainingAnimationPlayMode.Loop)
            {
                while (elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                yield break;
            }

            while (elapsed < timeout)
            {
                var info = animator.GetCurrentAnimatorStateInfo(b.Layer);

                if (!string.IsNullOrEmpty(b.StateName) && !info.IsName(b.StateName))
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                    continue;
                }

                float norm = info.normalizedTime;

                if (b.PlayMode == TrainingAnimationPlayMode.Once ||
                    b.PlayMode == TrainingAnimationPlayMode.OnceStopAtPercent)
                {
                    if (norm >= targetNorm)
                        yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Debug.LogWarning($"[AnimationNode] Timeout on node '{b.NodeId}'.");
        }

        private void StopRunning()
        {
            if (_running == null) return;
            StopCoroutine(_running);
            _running = null;
        }
    }
}