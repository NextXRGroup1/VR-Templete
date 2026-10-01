using System;
using UnityEngine;
using Training.Core;
using Training.Assessment.Data;

namespace Training.Flow.Data
{
    // Configuration describing what should happen at one point in the training.
    // Kept intentionally focused per Section 11 — content fields, trigger fields
    // and the assessment reference sit side by side rather than in separate
    // node subclasses, since ScriptableObject-friendly lists need a single
    // serializable type. If this grows further, split into per-type data
    // (e.g. FlowNodeContentData) and reference it from here instead.
    [Serializable]
    public class FlowNodeData
    {
        public string Id;
        public FlowNodeType NodeType;
        public string NextNodeId;

        // Applies to Text / TextImage / Trigger nodes. Assessment nodes get
        // their own PlayAudio + AudioClip on AssessmentConfig instead, since
        // an assessment can outlive a single node and is configured once on
        // the asset rather than per flow node.
        // PlayAudio == false -> never plays, AudioClip is ignored/hidden.
        // PlayAudio == true with no AudioClip assigned -> simply doesn't
        // play anything; this is never a validation error.
        public bool PlayAudio;
        public AudioClip AudioClip;

        public TrainingTrigger Trigger = TrainingTrigger.None;
        public NavigationMode Navigation = NavigationMode.None;

        // Only used when Navigation == None. Content shows with no Back/Next
        // buttons and the flow advances to NextNodeId on its own after this
        // many seconds. 0 = advance immediately (old behaviour).
        [Min(0f)]
        public float AutoAdvanceDelaySeconds = 4f;

        [TextArea]
        public string TextContent;
        public Sprite Image;

        public AssessmentConfig AssessmentReference;
    }
}