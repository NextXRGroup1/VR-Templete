using System;
using System.Collections;
using UnityEngine;
using Training.Flow.Controllers;
using Training.Flow.Data;

namespace Training.Integration
{
    /// <summary>
    /// Demo animation: on Animation node, spin a target then complete the node.
    /// Avoids needing an AnimatorController asset in the sample.
    /// </summary>
    public class FullDemoSpinHandler : MonoBehaviour
    {
        [SerializeField] private TrainingFlowController flowController;
        [SerializeField] private Transform spinTarget;
        [SerializeField] private string animationNodeId = "N_ANIM";
        [SerializeField] private float duration = 1.5f;
        [SerializeField] private Vector3 eulerSpeed = new Vector3(0f, 180f, 0f);

        private void OnEnable()
        {
            if (flowController != null)
                flowController.OnAnimationNode += Handle;
        }

        private void OnDisable()
        {
            if (flowController != null)
                flowController.OnAnimationNode -= Handle;
        }

        private void Handle(FlowNodeData node, Action onComplete)
        {
            if (node == null || node.Id != animationNodeId)
            {
                onComplete?.Invoke();
                return;
            }
            StartCoroutine(SpinThen(onComplete));
        }

        private IEnumerator SpinThen(Action onComplete)
        {
            if (spinTarget == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                spinTarget.Rotate(eulerSpeed * Time.deltaTime, Space.Self);
                t += Time.deltaTime;
                yield return null;
            }
            onComplete?.Invoke();
        }
    }
}
