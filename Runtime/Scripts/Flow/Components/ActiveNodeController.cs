using System;
using System.Collections.Generic;
using UnityEngine;
using Training.Flow.Controllers;
using Training.Flow.Data;

namespace Training.Flow.Components
{
    [Serializable]
    public class ActiveNodeBinding
    {
        [Tooltip("Must match FlowNodeData.Id for an Active node.")]
        public string NodeId;

        [Tooltip("Set active true when this node is entered.")]
        public GameObject[] ObjectsToEnable;

        [Tooltip("Set active false when this node is entered.")]
        public GameObject[] ObjectsToDisable;
    }

    /// <summary>
    /// Scene bindings for FlowNodeType.Active.
    /// GameObject refs must live here (not on the ScriptableObject flow asset).
    /// </summary>
    public class ActiveNodeController : MonoBehaviour
    {
        [SerializeField] private TrainingFlowController flowController;
        [SerializeField] private List<ActiveNodeBinding> bindings = new List<ActiveNodeBinding>();

        private void OnEnable()
        {
            if (flowController != null)
                flowController.OnActiveNode += HandleActiveNode;
        }

        private void OnDisable()
        {
            if (flowController != null)
                flowController.OnActiveNode -= HandleActiveNode;
        }

        private void HandleActiveNode(FlowNodeData node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id)) return;
            Apply(node.Id);
        }

        public void Apply(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || bindings == null) return;

            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b == null || b.NodeId != nodeId) continue;

                if (b.ObjectsToEnable != null)
                {
                    for (int e = 0; e < b.ObjectsToEnable.Length; e++)
                    {
                        if (b.ObjectsToEnable[e] != null)
                            b.ObjectsToEnable[e].SetActive(true);
                    }
                }

                if (b.ObjectsToDisable != null)
                {
                    for (int d = 0; d < b.ObjectsToDisable.Length; d++)
                    {
                        if (b.ObjectsToDisable[d] != null)
                            b.ObjectsToDisable[d].SetActive(false);
                    }
                }

                Debug.Log($"[ActiveNode] Applied binding for node '{nodeId}'.");
                return;
            }

            Debug.LogWarning(
                $"[ActiveNode] No binding for node '{nodeId}'. " +
                "Add an entry on ActiveNodeController with matching Node Id.");
        }
    }
}