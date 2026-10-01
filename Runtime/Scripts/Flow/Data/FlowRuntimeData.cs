using System;
using System.Collections.Generic;

namespace Training.Flow.Data
{
    // Plain runtime state for the flow. Not a MonoBehaviour/ScriptableObject.
    [Serializable]
    public class FlowRuntimeData
    {
        public string CurrentNodeId;
        public List<string> VisitedNodeIds = new List<string>();
        public bool IsCompleted;
    }
}
