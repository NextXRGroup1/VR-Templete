using System.Collections.Generic;
using UnityEngine;

namespace Training.Flow.Data
{
    // Designer-editable flow definition (Section 19).
    [CreateAssetMenu(fileName = "TrainingFlowConfig", menuName = "Training/Flow/Training Flow Config")]
    public class TrainingFlowConfig : ScriptableObject
    {
        [Tooltip("Shown on the training-selection menu. Falls back to the asset's file name if left blank.")]
        public string Title;

        [Tooltip("If off, no completion screen is shown at all when this training finishes - score is still computed and available via TrainingSessionController.OnSessionCompleted, just nothing is displayed automatically.")]
        public bool ShowCompletionScreen = true;

        // RENAMED from ShowScoreDetails -> CalculateScore. Same meaning as
        // before (gates whether the completion screen shows title/score/
        // detail/badges), just a clearer name. Ignored if ShowCompletionScreen
        // is off, and ignored (treated as off) whenever ShowFinalQuizPanel is
        // on, since that layout never shows score text at all.
        [Tooltip("If off, the completion screen shows only its image and buttons - no title/score/detail text. Ignored if ShowCompletionScreen is off, and ignored (always treated as off) whenever ShowFinalQuizPanel is on.")]
        public bool CalculateScore = true;

        // NEW. A second, simpler completion layout: just the completion
        // image plus Retry/Home/Quiz buttons, no score text at all. Takes
        // priority over CalculateScore when both are on - TrainingCompletePanel
        // treats them as two alternate layouts, not two independent toggles.
        [Tooltip("If on, the completion screen shows the simpler Final Quiz layout instead of the full score details: just the completion image plus Retry/Home/Quiz buttons, no title/score/detail text at all. Takes priority over CalculateScore when both are on. Ignored if ShowCompletionScreen is off.")]
        public bool ShowFinalQuizPanel = false;

        [Tooltip("Optional. Only used by the Final Quiz Panel layout (ShowFinalQuizPanel on) - e.g. a congratulations graphic. Ignored (and hidden in the Inspector) when ShowFinalQuizPanel is off. Leave empty to show no image.")]
        public Sprite CompletionImage;

        public string StartNodeId;
        public List<FlowNodeData> Nodes = new List<FlowNodeData>();

        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? name : Title;

        public FlowNodeData GetNodeById(string id)
        {
            return Nodes.Find(n => n.Id == id);
        }

        // Section 29 validation rules, checked in-editor or at session start.
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(StartNodeId) || GetNodeById(StartNodeId) == null)
            {
                error = "Flow must have a valid start node.";
                return false;
            }

            foreach (var node in Nodes)
            {
                if (!string.IsNullOrEmpty(node.NextNodeId) && GetNodeById(node.NextNodeId) == null)
                {
                    error = $"Node '{node.Id}' references a NextNodeId '{node.NextNodeId}' that does not exist.";
                    return false;
                }

                if (node.NodeType == Training.Core.FlowNodeType.Trigger &&
                    node.Trigger == Training.Core.TrainingTrigger.None)
                {
                    error = $"Trigger node '{node.Id}' must have a TrainingTrigger other than None.";
                    return false;
                }

                if (node.NodeType == Training.Core.FlowNodeType.Assessment && node.AssessmentReference == null)
                {
                    error = $"Assessment node '{node.Id}' has no AssessmentConfig assigned.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}