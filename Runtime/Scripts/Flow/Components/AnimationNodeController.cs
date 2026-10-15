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

        [Tooltip("Optional: state to wait on. If empty, waits on current state after trigger.")]
        public string StateName;

        public TrainingAnimationPlayMode PlayMode = AnimationPlayMode.Once;

        [Range(1, 100)]
        [Tooltip("Used by OnceStopAtPercent (and optional early end for Once).")]
        public int StopAtPercent = 100;

        [Tooltip("Animator layer index.")]
        public int Layer = 0;

        [Min(0f)]
        [Tooltip("Failsafe seconds. 0 = no timeout.")]
        public float TimeoutSeconds = 30f;

        [Tooltip("If true, flow advances without waiting for the animation.")]
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
                Debug.LogWarning($"[AnimationNode] No binding for '{node.Id}'.");
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
            var animator = b.Target != null ? b.Target.GetComponent<Animator>() : null;
            if (animator == null)
            {
                Debug.LogError($"[AnimationNode] No Animator on binding '{b.NodeId}'.");
                onComplete?.Invoke();
                yield break;
            }

            if (!string.IsNullOrEmpty(b.TriggerKey))
                animator.SetTrigger(b.TriggerKey);

            if (b.FireAndContinue)
            {
                onComplete?.Invoke();
                // still play; flow already moved
                if (b.PlayMode != AnimationPlayMode.Loop)
                    yield return WaitForEnd(animator, b);
                yield break;
            }

            yield return WaitForEnd(animator, b);
            onComplete?.Invoke();
        }

        private IEnumerator WaitForEnd(Animator animator, AnimationNodeBinding b)
        {
            // Let the trigger take effect
            yield return null;

            float timeout = b.TimeoutSeconds > 0f ? b.TimeoutSeconds : float.MaxValue;
            float elapsed = 0f;
            float targetNorm = Mathf.Clamp01(b.StopAtPercent / 100f);

            // Wait until we are in the target state (if named)
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

            if (b.PlayMode == AnimationPlayMode.Loop)
            {
                // Loop until timeout only (training: prefer Once or StopAtPercent)
                while (elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                yield break;
            }

            // Once / OnceStopAtPercent: wait normalized time
            while (elapsed < timeout)
            {
                var info = animator.GetCurrentAnimatorStateInfo(b.Layer);

                if (!string.IsNullOrEmpty(b.StateName) && !info.IsName(b.StateName))
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                    continue;
                }

                // normalizedTime can be > 1 when looping; for Once use modulo logic carefully
                float norm = info.normalizedTime;
                if (b.PlayMode == AnimationPlayMode.Once ||
                    b.PlayMode == AnimationPlayMode.OnceStopAtPercent)
                {
                    // First cycle only
                    if (norm >= targetNorm)
                        yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Debug.LogWarning($"[AnimationNode] Timeout on '{b.NodeId}'.");
        }

        private void StopRunning()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }
        }
    }
}