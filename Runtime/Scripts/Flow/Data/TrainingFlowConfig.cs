using System.Collections.Generic;
using UnityEngine;
using Training.Core;
using Training.Assessment.Data;

namespace Training.Flow.Data
{
    [CreateAssetMenu(fileName = "TrainingFlowConfig", menuName = "Training/Flow/Training Flow Config")]
    public class TrainingFlowConfig : ScriptableObject
    {
        public string Title;

        [Tooltip("When off, nothing is shown when this training ends (score can still be calculated).")]
        public bool ShowCompletionScreen = true;

        [Tooltip("Full results layout with score. Ignored while ShowFinalQuizPanel is on.")]
        public bool CalculateScore = true;

        [Tooltip("Simpler layout: image + Retry/Home/Quiz. Takes priority over CalculateScore.")]
        public bool ShowFinalQuizPanel = false;

        [Tooltip("Optional. Only used by the Final Quiz Panel layout.")]
        public Sprite CompletionImage;

        public string StartNodeId;
        public List<FlowNodeData> Nodes = new List<FlowNodeData>();

        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? name : Title;

        public FlowNodeData GetNodeById(string id)
        {
            return Nodes.Find(n => n.Id == id);
        }

        public bool Validate(out string error)
        {
            if (Nodes == null || Nodes.Count == 0)
            {
                error = "Flow must contain at least one node.";
                return false;
            }

            var ids = new HashSet<string>();
            foreach (var node in Nodes)
            {
                if (node == null)
                {
                    error = "Flow contains a null node.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(node.Id))
                {
                    error = "Every flow node must have an ID.";
                    return false;
                }

                if (!ids.Add(node.Id))
                {
                    error = $"Duplicate node ID detected: '{node.Id}'.";
                    return false;
                }
            }

            if (string.IsNullOrEmpty(StartNodeId) || GetNodeById(StartNodeId) == null)
            {
                error = "Flow must have a valid start node.";
                return false;
            }

            var assessmentIds = new HashSet<string>();

            foreach (var node in Nodes)
            {
                if (!string.IsNullOrEmpty(node.NextNodeId) && GetNodeById(node.NextNodeId) == null)
                {
                    error = $"Node '{node.Id}' references NextNodeId '{node.NextNodeId}' that does not exist.";
                    return false;
                }

                if (!string.IsNullOrEmpty(node.NextNodeId) && node.NextNodeId == node.Id)
                {
                    error = $"Node '{node.Id}' points to itself (self-loop).";
                    return false;
                }

                if (node.NodeType == FlowNodeType.Trigger && node.Trigger == TrainingTrigger.None)
                {
                    error = $"Trigger node '{node.Id}' must have a TrainingTrigger other than None.";
                    return false;
                }

                if (node.NodeType == FlowNodeType.Assessment)
                {
                    if (node.AssessmentReference == null)
                    {
                        error = $"Assessment node '{node.Id}' has no AssessmentConfig assigned.";
                        return false;
                    }

                    if (!node.AssessmentReference.Validate(out var assessError))
                    {
                        error = $"Assessment node '{node.Id}': {assessError}";
                        return false;
                    }

                    var aid = node.AssessmentReference.AssessmentId;
                    if (!string.IsNullOrWhiteSpace(aid) && !assessmentIds.Add(aid))
                    {
                        error = $"Duplicate AssessmentId '{aid}' used by more than one assessment node in this flow.";
                        return false;
                    }
                }
            }

            if (HasCycle(StartNodeId, out var cycleNode))
            {
                error = $"Flow cycle detected involving node '{cycleNode}'.";
                return false;
            }

            var reachable = GetReachableIds(StartNodeId);
            foreach (var node in Nodes)
            {
                if (!reachable.Contains(node.Id))
                {
                    error = $"Node '{node.Id}' is unreachable from StartNodeId '{StartNodeId}'.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private bool HasCycle(string startId, out string cycleNodeId)
        {
            // Local variable required: C# does not allow assigning to an out
            // parameter from inside a local function (CS1628).
            string foundCycleNode = null;
            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();

            bool Dfs(string id)
            {
                if (string.IsNullOrEmpty(id)) return false;
                if (visiting.Contains(id))
                {
                    foundCycleNode = id;
                    return true;
                }
                if (visited.Contains(id)) return false;

                visiting.Add(id);
                var node = GetNodeById(id);
                if (node != null && !string.IsNullOrEmpty(node.NextNodeId))
                {
                    if (Dfs(node.NextNodeId)) return true;
                }
                visiting.Remove(id);
                visited.Add(id);
                return false;
            }

            var hasCycle = Dfs(startId);
            cycleNodeId = foundCycleNode;
            return hasCycle;
        }

        private HashSet<string> GetReachableIds(string startId)
        {
            var reachable = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(startId);

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!reachable.Add(id)) continue;

                var node = GetNodeById(id);
                if (node != null && !string.IsNullOrEmpty(node.NextNodeId))
                    queue.Enqueue(node.NextNodeId);
            }

            return reachable;
        }
    }
}
