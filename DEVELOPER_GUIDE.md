# Training Framework — Developer Guide

**Package:** `com.training.framework`  
**Unity:** 6000.0+ (Unity 6)  
**Dependencies:** TextMeshPro, uGUI, Input System  

Guide for developers integrating and extending the framework in **VR, AR, and desktop** training projects.

---

## Table of contents

1. [What this package is](#1-what-this-package-is)
2. [Core concepts](#2-core-concepts)
3. [Architecture](#3-architecture)
4. [Quick start](#4-quick-start)
5. [TrainingFlowConfig (designing a course)](#5-trainingflowconfig-designing-a-course)
6. [Node types reference](#6-node-types-reference)
7. [Triggers (unlocking a step)](#7-triggers-unlocking-a-step)
8. [Active node (enable / disable objects)](#8-active-node-enable--disable-objects)
9. [Animation node](#9-animation-node)
10. [Multiple flow configs in one scene](#10-multiple-flow-configs-in-one-scene)
11. [Sequence activities (PPE / Select / Equip)](#11-sequence-activities-ppe--select--equip)
12. [Zone and custom activities](#12-zone-and-custom-activities)
13. [Assessment and scoring](#13-assessment-and-scoring)
14. [UI system](#14-ui-system)
15. [AR usage](#15-ar-usage)
16. [Editor menus](#16-editor-menus)
17. [API reference](#17-api-reference)
18. [Extending the system](#18-extending-the-system)
19. [Troubleshooting](#19-troubleshooting)
20. [Do and don’t](#20-do-and-dont)
21. [File map](#21-file-map)

---

## 1. What this package is

A **flow-driven training runtime** plus **world activities**, in one package:

| Module | Responsibility |
|--------|----------------|
| **Flow** | Ordered steps: text, wait-for-signal, active, animation, assessment, completion |
| **Session** | Which `TrainingFlowConfig` is running; multi-course list |
| **Trigger** | Named signals that resume a waiting Trigger node |
| **Assessment / Question** | Quizzes, results, section scores |
| **UI** | Panels for text, questions, results, menu, completion |
| **Sequence** | Guided multi-step world tasks (select, equip, custom) |
| **Integration** | Bridges sequence / zones / custom code → flow triggers |

It is **XR-agnostic**. Wire VR grab/ray/hand or AR tap/track events to `Raise(...)` or sequence `NotifySelected` / `NotifyEquipped`.

---

## 2. Core concepts

### Flow vs trigger (do not confuse these)

| | **Training flow** | **Trigger** |
|--|-------------------|-------------|
| What | Entire course asset | One signal that unlocks a **Trigger** node |
| Type | `TrainingFlowConfig` ScriptableObject | `TrainingTrigger` enum |
| Example | Title: `"PPE Hands-On Training"` | `TaskCompleted`, `Continue` |
| How you use it | Assign to Session → Available Flow Configs | `TriggerFlowController.Raise(TaskCompleted)` |

**Never** pass the flow **title** into `RaiseByName`. Only **enum names** are valid.

### Why scene controllers for Active / Animation

`TrainingFlowConfig` is a **Project asset**. Scene `GameObject` / `Animator` references do not store reliably on ScriptableObjects.

| Lives on flow asset | Lives on scene controller |
|---------------------|---------------------------|
| Node `Id`, type, next id | `GameObject[]` enable/disable |
| Trigger enum, assessment ref | Animator target, trigger key, play mode |

Match by **Node Id**.

---

## 3. Architecture

```
TrainingSessionController          // which course
        │ StartSession(config)
        ▼
TrainingFlowController             // walks nodes
        │
        ├─ Text / TextImage     → UIFlowController shows panels
        ├─ Active                 → ActiveNodeController (enable/disable)
        ├─ Animation              → AnimationNodeController (play → next)
        ├─ Trigger                → TriggerFlowController.WaitForTrigger
        │                              ▲
        │                              │ Raise(TaskCompleted | Continue | …)
        │                              │
        │                   Sequence / Zone / your activity
        │
        └─ Assessment             → AssessmentController → QuestionController
                                         │
                                         ▼
                                  score + optional results
                                         │
                                         ▼
                                  flow complete → TrainingCompletePanel
                                  (Retry / Choose Another)
```

### Integration bridges

| Component | Role |
|-----------|------|
| `SequenceToFlowBridge` | Flow waits → start sequence; sequence done → `Raise` |
| `SimpleFlowTriggerZone` | `OnTriggerEnter` → `Raise(triggerToRaise)` |
| `ActivityToFlowBridge` | `CompleteActivity()` from any script |
| `ActiveNodeController` | Node Id → objects to enable / disable |
| `AnimationNodeController` | Node Id → Animator + key; then next node |
| `MultiFlowSceneBootstrap` | Optional menu when multiple configs exist |
| `CombinedSequenceInput` | Editor mouse test for Select/Equip |

---

## 4. Quick start

### Recommended

1. Import the package (or copy into the project).
2. Open an empty scene.
3. Menu: **Training → Setup Multi-Flow Connected Scene**.
4. Press **Play**.
5. **Choose a Training** → PPE or Zone.

### What the multi-flow setup creates

| Asset / object | Purpose |
|----------------|---------|
| `Assets/TrainingDemo/FlowA_PPE.asset` | Sequence path (`TaskCompleted`) |
| `Assets/TrainingDemo/FlowB_Zone.asset` | Zone path (`Continue`) |
| `Assets/TrainingDemo/SharedAssessment.asset` | Shared quiz |
| `TrainingSystem` hierarchy | Controllers, UI, sequence, zone, bridges |
| `Player` (tag) | Zone testing |

### Hierarchy (simplified)

```
TrainingSystem
├── Controllers
│   ├── TriggerFlowController
│   ├── QuestionController
│   ├── AssessmentController
│   ├── TrainingScoreController
│   ├── TrainingFlowController
│   ├── TrainingSessionController   ← multiple flows
│   └── FlowRunner                  ← Start On Awake = false if menu first
├── UIPanelFactory
├── UIFlowController
├── MultiFlowSceneBootstrap
├── ActiveNodeController            ← optional, for Active nodes
├── AnimationNodeController         ← optional, for Animation nodes
├── SequenceActivity + bridge
├── ContinueTriggerZone
└── TrainingCanvas
```

---

## 5. TrainingFlowConfig (designing a course)

**Assets → Create → Training → Flow → Training Flow Config** (or setup menu).

### Important fields

| Field | Meaning |
|-------|---------|
| `Title` | Display name (menu / completion) — **not** a trigger id |
| `StartNodeId` | First node id |
| `Nodes` | List of `FlowNodeData` |
| `ShowCompletionScreen` | Final session panel |
| `CalculateScore` | Show score on completion |
| `ShowFinalQuizPanel` | Alternate completion layout |

### FlowNodeData (common)

| Field | Used when |
|-------|-----------|
| `Id` | Unique string (`A01`, `STAGE_PPE`, …) |
| `NodeType` | See [Node types](#6-node-types-reference) |
| `NextNodeId` | Next step; empty = end of flow |
| `TextContent` | Text / Trigger help text |
| `Image` | TextImage |
| `Trigger` | Trigger nodes only |
| `Navigation` | None / Next / BackNext on content |
| `AutoAdvanceDelaySeconds` | Content auto-advance; Active delay; etc. |
| `AssessmentReference` | Assessment nodes only |

### Validation

Inspector **Validate Flow** (or `config.Validate(out string error)`):

- Start node exists  
- No duplicate / empty ids  
- NextNodeId targets exist  
- No self-loops / cycles / unreachable nodes  
- Trigger ≠ None on Trigger nodes  
- Assessment reference present and valid  

---

## 6. Node types reference

```csharp
public enum FlowNodeType
{
    Text,        // 0 — show content
    TextImage,   // 1 — text + image
    Assessment,  // 2 — quiz
    Trigger,     // 3 — wait for TrainingTrigger
    Active,      // 4 — enable/disable scene objects, then advance
    Animation    // 5 — play Animator, then advance
}
```

| Type | Behaviour | Advances when |
|------|-----------|---------------|
| **Text / TextImage** | Show UI | Next / Back / auto-delay |
| **Trigger** | Wait for signal | Matching `Raise(...)` |
| **Active** | Enable/disable objects | Immediately or after delay |
| **Animation** | Play Animator trigger | Anim complete / % / timeout / fire-and-continue |
| **Assessment** | Quiz | Assessment finished (+ optional results Continue) |

### Example graph

```
A01  Text          intro
A01b Active        show PPE props, hide intro props
A01c Animation     door open once → next
A02  Trigger       TaskCompleted (sequence)
A03  Assessment    quiz
A04  Text          end
```

---

## 7. Triggers (unlocking a step)

### Built-in enum

```csharp
public enum TrainingTrigger
{
    None,
    Start,
    Continue,
    InteractionCompleted,
    TaskCompleted,
    ConditionMet,
    AssessmentStarted,
    AssessmentCompleted
}
```

### Static API (preferred — no scene reference)

```csharp
using Training.Core;
using Training.Trigger.Controllers;

TriggerFlowController.Raise(TrainingTrigger.TaskCompleted);
TriggerFlowController.Raise(TrainingTrigger.Continue);

// String form — enum name only, NOT flow title
TriggerFlowController.RaiseByName("TaskCompleted");
```

The active `TriggerFlowController` must be **enabled** so it is subscribed to `OnGlobalTriggerRaised`.

### Instance API

```csharp
triggerFlowController.RaiseTrigger(TrainingTrigger.TaskCompleted);
```

### Behaviour

1. Flow hits Trigger node → `WaitForTrigger(expected, onMatched)`.
2. Something calls `Raise(same value)`.
3. Controller resolves → flow moves to `NextNodeId`.
4. Early raises can be **buffered** until wait starts.

| Node waits for | You must raise |
|----------------|----------------|
| `TaskCompleted` | `TaskCompleted` |
| `Continue` | `Continue` |

---

## 8. Active node (enable / disable objects)

### Why a separate controller

Scene `GameObject` lists cannot live safely on the ScriptableObject flow asset. Bind them on **`ActiveNodeController`** by **Node Id**.

### Flow asset

- `NodeType = Active`
- Set `Id` and `NextNodeId`
- Optional `AutoAdvanceDelaySeconds` (`0` = immediate)

### Scene: ActiveNodeController

| Field | Meaning |
|-------|---------|
| Flow Controller | `TrainingFlowController` |
| Bindings | List of entries |

**Each binding:**

| Field | Meaning |
|-------|---------|
| Node Id | Same as Active node `Id` |
| Objects To Enable | `SetActive(true)` |
| Objects To Disable | `SetActive(false)` |

### Runtime

```
Enter Active node
  → OnActiveNode(node)
  → ActiveNodeController.Apply(node.Id)
  → MoveToNode(NextNodeId)  // or after delay
```

---

## 9. Animation node

### Enum (do not use UnityEngine.AnimationPlayMode)

```csharp
public enum TrainingAnimationPlayMode
{
    Once = 0,
    Loop = 1,
    OnceStopAtPercent = 2
}
```

Unity already defines `UnityEngine.AnimationPlayMode`. Always use **`TrainingAnimationPlayMode`**.

### Flow asset

- `NodeType = Animation`
- Set `Id` and `NextNodeId`

### Scene: AnimationNodeController

| Binding field | Purpose |
|---------------|---------|
| Node Id | Matches flow node |
| Target | GameObject with `Animator` |
| Trigger Key | Animator trigger parameter name |
| State Name | Optional state to wait on |
| Play Mode | Once / Loop / OnceStopAtPercent |
| Stop At Percent | 1–100 |
| Layer | Animator layer index |
| Timeout Seconds | Failsafe (avoid soft-lock) |
| Fire And Continue | Advance flow without waiting |

### Behaviour

| Mode | Behaviour |
|------|-----------|
| **Once** | Trigger → wait until state reaches end (or %) → next node |
| **Loop** | Loop until timeout, then next (prefer Once for training) |
| **OnceStopAtPercent** | Stop when `normalizedTime >= percent/100` → next node |

```
Enter Animation node
  → OnAnimationNode(node, onComplete)
  → SetTrigger(key)
  → wait (unless FireAndContinue)
  → onComplete → MoveToNode(NextNodeId)
```

### Example binding

| Field | Value |
|-------|--------|
| Node Id | `ANIM_OPEN_DOOR` |
| Target | Door (Animator) |
| Trigger Key | `Open` |
| State Name | `DoorOpen` |
| Play Mode | Once |
| Stop At Percent | 100 |

---

## 10. Multiple flow configs in one scene

### Session

```csharp
// TrainingSessionController
List<TrainingFlowConfig> _availableFlowConfigs; // index 0 = default

StartSession();
StartSession(TrainingFlowConfig config);
StartSession(int index);
RestartSession();

bool HasMultipleFlowConfigs; // Count > 1
```

### Menu behaviour

| Situation | Behaviour |
|-----------|-----------|
| Bootstrap `showMenuOnStart` + 2+ configs | Menu first; set `FlowRunner.startOnAwake = false` |
| `FlowRunner.startOnAwake` true | Auto-starts index 0 |
| Completion **Choose Another** | Menu again |
| Completion **Retry** | `RestartSession()` |

Only **one** flow runs at a time. The next config does **not** auto-start after completion.

Same scene can share sequence objects, zone, Active/Animation controllers; the **active** flow’s current node decides what matters.

---

## 11. Sequence activities (PPE / Select / Equip)

| Type | Role |
|------|------|
| `GuidedSequenceController` | Ordered steps |
| `GuidedStep` | Task, title, description, hint, action |
| `SelectAction` | Correct GameObject selected |
| `EquipAction` | Item (+ optional socket) equipped |
| `SequenceActionBase` | Call `Complete()` when done |
| `ISequenceValidatable` | Editor + runtime validation |
| `GuidanceMode` | Guided (UI on) / NonGuided |

### Wire to flow

1. Trigger node with `TaskCompleted`.
2. `SequenceToFlowBridge`: same `completionTrigger`, `startSequenceWhenFlowWaits = true`.
3. Sequence done → `TriggerFlowController.Raise(TaskCompleted)`.

```csharp
selectAction.NotifySelected(clickedGameObject);
equipAction.NotifyEquipped(itemGameObject, socketTransform);
```

Wrong object → ignored. Null target/item → validation fails (no auto-complete).

### Custom action

```csharp
public class RotateValveAction : SequenceActionBase, ISequenceValidatable
{
    public bool Validate(out string error) { /* require refs */ error = null; return true; }

    public void NotifyRotatedEnough()
    {
        if (IsCompleted) return;
        Complete();
    }
}
```

---

## 12. Zone and custom activities

### Zone

```csharp
// SimpleFlowTriggerZone — collider Is Trigger = ON
triggerToRaise = Continue;  // must match flow node
requiredTag = "Player";     // or empty = any
```

Uses static `TriggerFlowController.Raise`.

### Any script

```csharp
TriggerFlowController.Raise(TrainingTrigger.TaskCompleted);
// or
activityToFlowBridge.CompleteActivity();
```

---

## 13. Assessment and scoring

| Field | Meaning |
|-------|---------|
| `AssessmentId` | Unique id |
| `Questions` | List of `QuestionData` |
| `ShowResultsScreen` | Pause for section results |
| `PassThresholdPercent` | Pass / fail |
| `NavigationMode` | Sequential / FreeNavigation |

**Question types:** `QuestionAnswer`, `YesNo`, `TrueFalse`, `SingleChoice`, `MultipleChoice`

```
Assessment node
  → AssessmentController.StartAssessment
  → QuestionController per question
  → section result
  → optional AssessmentResultPanel
  → ReportResultsContinue → next flow node
```

Overall score: `TrainingScoreController` → completion panel.

---

## 14. UI system

| Component | Role |
|-----------|------|
| `UIFlowController` | Flow/assessment events → show/hide panels |
| `UIPanelFactory` | Instantiates prefabs |
| Prefabs | Text, TextImage, Assessment, Result, Complete, Menu, ChoiceOption |

Assign all prefabs on `UIPanelFactory`. Missing **Training Menu Panel** → “Choose a Training” with **no buttons**.

- **Retry** → `RestartSession()`
- **Choose Another** → menu → `StartSession(config)`

---

## 15. AR usage

The core is not VR-only. Use the same flows and assessments; swap **input / activity** adapters.

| VR | AR |
|----|-----|
| XR grab | AR raycast / touch / tracked object |
| Hand socket | AR anchor / tracked socket |
| Room collider | Plane bounds, geo, image marker |
| Screen or world UI | Phone overlay or world canvas |

```csharp
void OnARObjectSelected(GameObject hit)
{
    selectAction.NotifySelected(hit);
}

void OnTrackedImageFound()
{
    TriggerFlowController.Raise(TrainingTrigger.Continue);
}
```

---

## 16. Editor menus

| Menu | Result |
|------|--------|
| **Training → Setup Multi-Flow Connected Scene** | Multi-config + sequence + zone + menu |
| Training → Setup Full Combined Demo | Single flow + sequence |
| Training → Setup Full Demo Scene | Flow / quiz only |
| Tools → Training → Generate ChoiceOption Prefab | Choice row helper |

On a flow asset: **Validate Flow**.

---

## 17. API reference

### Session

```csharp
session.StartSession();
session.StartSession(flowConfig);
session.StartSession(0);
session.RestartSession();
session.AvailableFlowConfigs;
session.CurrentFlowConfig;
session.HasMultipleFlowConfigs;
TrainingSessionController.OnSessionCompleted;
```

### Flow

```csharp
flow.Initialize(config);
flow.StartFlow();
flow.JumpToNode(id);
flow.ReportResultsContinue();
flow.ReportResultsRetry();

// Events
OnNodeEntered, OnShowContentNode, OnWaitingForTrigger,
OnActiveNode, OnAnimationNode, OnShowAssessmentResult,
OnFlowCompleted, OnBackRequested, OnStateChanged
```

### Triggers

```csharp
TriggerFlowController.Raise(TrainingTrigger.TaskCompleted);
TriggerFlowController.RaiseByName("Continue");
instance.WaitForTrigger(expected, onMatched);
instance.IsWaiting;
instance.ExpectedTrigger;
```

### Sequence

```csharp
sequence.StartSequence();
sequence.OnSequenceCompleted;
select.NotifySelected(go);
equip.NotifyEquipped(item, socket);
```

### Active / Animation

```csharp
// Subscribed automatically when controllers are enabled
flow.OnActiveNode += ...;
flow.OnAnimationNode += (node, onComplete) => { /* play; then onComplete(); */ };
```

---

## 18. Extending the system

### New world activity between nodes

1. Add a **Trigger** node with a chosen enum.
2. Implement activity (sequence, collider, XR/AR event).
3. On success: `TriggerFlowController.Raise(thatEnum)`.

### New sequence action

1. Subclass `SequenceActionBase`.
2. Implement `ISequenceValidatable` / `ISequenceTarget` as needed.
3. Call `Complete()` only on real success.

### New node type (pattern)

1. Add `FlowNodeType` value.
2. Handle in `TrainingFlowController.ExecuteNode`.
3. If scene object refs are required → scene controller + Node Id bindings (like Active / Animation).
4. Update `FlowNodeDataDrawer`.

### Custom string triggers (optional future)

Today only the **enum** is supported. Prefer enum values for shipping demos.

---

## 19. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `'{Title}' is not a valid TrainingTrigger` | Raised flow title | Raise `TaskCompleted` / `Continue` |
| Stuck on Trigger node | Nothing raises matching enum | Bridge, zone, or debug key **T** |
| Zone does nothing | Flow already completed, or wrong tag | Enter while `Waiting for…`; tag `Player` |
| “Choose a Training” but no buttons | Menu prefab missing | Assign Training Menu Panel on factory |
| Menu never shows | Bootstrap off or one config | `showMenuOnStart` + 2 configs; FlowRunner off |
| Sequence auto-completes | Null Select/Equip target | Validate + assign targets |
| CS0104 / CS0117 `AnimationPlayMode` | Clash with Unity enum | Use `TrainingAnimationPlayMode` |
| Active/Animation does nothing | No binding for Node Id | Match Id on controller binding |
| Animation never advances | Wrong state name / no timeout | Set State Name, TimeoutSeconds |
| Second flow never runs | Only index 0 auto-starts | Menu / Choose Another / `StartSession(1)` |
| CS0118 `Editor` is a namespace | `namespace Training.Editor` | Use `Training.EditorTools` + `UnityEditor.Editor` |

**Debug key:** In Editor, while waiting for a trigger, press **T** (default) to raise the expected trigger.

---

## 20. Do and don’t

**Do**

- Match Trigger node enum to `Raise(...)`  
- Validate flows in the Inspector  
- Keep enable/disable and Animator refs on **scene** controllers  
- Use `TrainingAnimationPlayMode`, not Unity’s `AnimationPlayMode`  
- Use static `Raise` from activities  
- Use menu or `StartSession(config)` for multi-course scenes  

**Don’t**

- Use flow **Title** as a trigger name  
- Expect the next Available Flow Config to auto-start after completion  
- Put scene GameObject arrays on the ScriptableObject flow asset  
- Complete Select/Equip with null targets  
- Soft-lock on Animation without a timeout  

---

## 21. File map

```
Runtime/Scripts/
  Core/             Enums (FlowNodeType, TrainingTrigger, TrainingAnimationPlayMode)
  Flow/
    Data/           TrainingFlowConfig, FlowNodeData
    Controllers/    TrainingSessionController, TrainingFlowController
    Components/     FlowRunner, ActiveNodeController, AnimationNodeController
  Trigger/          TriggerFlowController, TrainingTriggerSource
  Assessment/       AssessmentConfig, controllers, score
  Question/         QuestionData, QuestionController
  UI/               UIFlowController, panels, UIPanelFactory
  SequenceModule/   Guided sequence, Select/Equip, highlighter, UI
  Integration/      Bridges, zone, bootstrap, mouse test input

Editor/             Drawers, setup menus (Multi-Flow, Combined, Demo)
Runtime/Doc/        This guide
```

### Reference demos

| Flow | Trigger / activity |
|------|---------------------|
| FlowA_PPE | `TaskCompleted` → sequence |
| FlowB_Zone | `Continue` → orange zone |

Shared assessment for both.

---

*End of developer guide.*
