# Training Framework — Developer Guide

**Package:** `com.training.framework`  
**Unity:** 6000.0+ (Unity 6)  
**Dependencies:** TextMeshPro, uGUI, Input System  

This guide is for developers integrating and extending the framework in a VR/desktop training project.

---

## Table of contents

1. [What this package is](#1-what-this-package-is)
2. [Core concepts](#2-core-concepts)
3. [Architecture](#3-architecture)
4. [Quick start](#4-quick-start)
5. [TrainingFlowConfig (designing a course)](#5-trainingflowconfig-designing-a-course)
6. [Triggers (unlocking a step)](#6-triggers-unlocking-a-step)
7. [Multiple flow configs in one scene](#7-multiple-flow-configs-in-one-scene)
8. [Sequence activities (PPE / Select / Equip)](#8-sequence-activities-ppe--select--equip)
9. [Zone and custom activities](#9-zone-and-custom-activities)
10. [Assessment and scoring](#10-assessment-and-scoring)
11. [UI system](#11-ui-system)
12. [Editor menus](#12-editor-menus)
13. [API reference (most used)](#13-api-reference-most-used)
14. [Extending the system](#14-extending-the-system)
15. [Troubleshooting](#15-troubleshooting)
16. [Do and don’t](#16-do-and-dont)

---

## 1. What this package is

A **flow-driven training runtime** plus **world activities**, in one package:

| Module | Responsibility |
|--------|----------------|
| **Flow** | Ordered steps: text, wait-for-signal, assessment, completion |
| **Session** | Which `TrainingFlowConfig` is running; multi-course list |
| **Trigger** | Named signals that resume a waiting flow node |
| **Assessment / Question** | Quizzes, results, section scores |
| **UI** | Panels for text, questions, results, menu, completion |
| **Sequence** | Guided multi-step world tasks (select, equip, custom) |
| **Integration** | Bridges sequence/zones/custom code → flow triggers |

It is **XR-agnostic**. Wire XR grab/ray/hand events to `Raise(...)` or sequence `NotifySelected` / `NotifyEquipped`.

---

## 2. Core concepts

### Flow vs trigger (do not confuse these)

| | **Training flow** | **Trigger** |
|--|-------------------|-------------|
| What | Entire course asset | One signal that unlocks a **Trigger node** |
| Type | `TrainingFlowConfig` ScriptableObject | `TrainingTrigger` enum |
| Example | Title: `"PPE Hands-On Training"` | `TaskCompleted`, `Continue` |
| How you use it | Assign to Session → Available Flow Configs | `TriggerFlowController.Raise(TaskCompleted)` |

**Never** pass the flow title into `RaiseByName`. Only enum names are valid.

### Node types

```csharp
public enum FlowNodeType
{
    Text,        // 0 — show content
    TextImage,   // 1 — text + image
    Assessment,  // 2 — quiz
    Trigger      // 3 — wait for TrainingTrigger
}
```

### Built-in triggers

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

---

## 3. Architecture

```
TrainingSessionController          // which course
        │ StartSession(config)
        ▼
TrainingFlowController             // walks nodes
        │
        ├─ Text/TextImage  → UIFlowController shows panels
        ├─ Trigger         → TriggerFlowController.WaitForTrigger
        │                         ▲
        │                         │ Raise(TaskCompleted | Continue | …)
        │                         │
        │              Sequence / Zone / your activity
        │
        └─ Assessment      → AssessmentController → QuestionController
                                  │
                                  ▼
                           score + optional results panel
                                  │
                                  ▼
                           flow complete → TrainingCompletePanel
                           (Retry / Choose Another)
```

**Integration bridges**

| Component | Role |
|-----------|------|
| `SequenceToFlowBridge` | Flow waits → start sequence; sequence done → `Raise(completionTrigger)` |
| `SimpleFlowTriggerZone` | `OnTriggerEnter` → `Raise(triggerToRaise)` |
| `ActivityToFlowBridge` | Call `CompleteActivity()` from any script |
| `MultiFlowSceneBootstrap` | Optional menu when multiple configs exist |
| `CombinedSequenceInput` | Editor/mouse test clicks for Select/Equip (no XR) |

---

## 4. Quick start

### Recommended (full multi-course scene)

1. Import package (or copy `TrainingFramework` into the project).
2. Open an empty scene.
3. Menu: **Training → Setup Multi-Flow Connected Scene**.
4. Press **Play**.
5. Menu **Choose a Training** → pick PPE or Zone.

### What the setup creates

| Asset / object | Purpose |
|----------------|---------|
| `Assets/TrainingDemo/FlowA_PPE.asset` | Sequence path (`TaskCompleted`) |
| `Assets/TrainingDemo/FlowB_Zone.asset` | Zone path (`Continue`) |
| `Assets/TrainingDemo/SharedAssessment.asset` | Shared quiz |
| `TrainingSystem` hierarchy | Controllers, UI, sequence, zone, bridges |
| `Player` (tag) | For zone testing |

### Hierarchy (simplified)

```
TrainingSystem
├── Controllers
│   ├── TriggerFlowController
│   ├── QuestionController
│   ├── AssessmentController
│   ├── TrainingScoreController
│   ├── TrainingFlowController
│   ├── TrainingSessionController   ← both flows assigned
│   └── FlowRunner                  ← Start On Awake = false (menu first)
├── UIPanelFactory
├── UIFlowController
├── MultiFlowSceneBootstrap         ← show menu on start
├── SequenceActivity + bridge
├── ContinueTriggerZone
└── TrainingCanvas
```

---

## 5. TrainingFlowConfig (designing a course)

Create via **Assets → Create → Training → Flow → Training Flow Config**  
(or use the setup menu).

### Important fields

| Field | Meaning |
|-------|---------|
| `Title` | Display name (menu / completion) — **not** a trigger id |
| `StartNodeId` | First node id |
| `Nodes` | Ordered list of `FlowNodeData` |
| `ShowCompletionScreen` | Final session panel |
| `CalculateScore` | Show score on completion |
| `ShowFinalQuizPanel` | Alternate completion layout |

### FlowNodeData

| Field | Used when |
|-------|-----------|
| `Id` | Unique string (`A01`, `NODE_02`, …) |
| `NodeType` | Text / TextImage / Assessment / Trigger |
| `NextNodeId` | Next step; empty = end of flow |
| `TextContent` | Text / Trigger help text |
| `Image` | TextImage |
| `Trigger` | **Trigger nodes only** — enum to wait for |
| `Navigation` | None / Next / BackNext on content nodes |
| `AutoAdvanceDelaySeconds` | When Navigation is None |
| `AssessmentReference` | Assessment nodes only |

### Example graphs

**PPE + quiz**

```
A01 Text (auto)
A02 Trigger TaskCompleted
A03 Assessment
A04 Text (auto) → end
```

**Zone + quiz**

```
B01 Text (auto)
B02 Trigger Continue
B03 Text (Next)
B04 Assessment
B05 Text (auto) → end
```

### Validation

Inspector button **Validate Flow** (or `config.Validate(out string error)`):

- Start node exists  
- No duplicate / empty ids  
- NextNodeId targets exist  
- No self-loops / cycles / unreachable nodes  
- Trigger ≠ None on Trigger nodes  
- Assessment reference present and valid  

---

## 6. Triggers (unlocking a step)

### Static API (preferred — no scene reference)

```csharp
using Training.Core;
using Training.Trigger.Controllers;

TriggerFlowController.Raise(TrainingTrigger.TaskCompleted);
TriggerFlowController.Raise(TrainingTrigger.Continue);

// String form — must be the enum name, not the flow title
TriggerFlowController.RaiseByName("TaskCompleted");
```

### Instance API

```csharp
triggerFlowController.RaiseTrigger(TrainingTrigger.TaskCompleted);
```

### Behaviour

1. Flow hits a Trigger node → `WaitForTrigger(expected, onMatched)`.
2. Something calls `Raise(same value)`.
3. Controller resolves → flow moves to `NextNodeId`.
4. If raise happens **before** wait, it can be **buffered** (pending) and applied when wait starts.

### Matching rules

| Flow node waits for | You must raise |
|---------------------|----------------|
| `TaskCompleted` | `TaskCompleted` |
| `Continue` | `Continue` |

Raising a different enum does not advance that node.

---

## 7. Multiple flow configs in one scene

### Session

```csharp
// TrainingSessionController
List<TrainingFlowConfig> _availableFlowConfigs; // index 0 = default

StartSession();                 // current or [0]
StartSession(TrainingFlowConfig config);
StartSession(int index);
RestartSession();               // same current config again

bool HasMultipleFlowConfigs;    // Count > 1
```

### Menu behaviour

| Situation | Behaviour |
|-----------|-----------|
| `MultiFlowSceneBootstrap.showMenuOnStart` + 2+ configs | Menu first; `FlowRunner.startOnAwake` should be **false** |
| `FlowRunner.startOnAwake` true | Auto-starts index 0 |
| Completion **Choose Another** | Menu again (`UIFlowController`) |
| Completion **Retry** | `RestartSession()` |

### Same scene, different activities

Both flows share the same sequence objects and zone. Only the **active** flow’s Trigger node decides which signal matters:

- Flow A waiting on `TaskCompleted` → sequence completion matters  
- Flow B waiting on `Continue` → zone entry matters  

---

## 8. Sequence activities (PPE / Select / Equip)

### Pieces

| Type | Role |
|------|------|
| `GuidedSequenceController` | Ordered steps |
| `GuidedStep` | Task text, title, description, hint, action |
| `SelectAction` | Wait until correct GameObject selected |
| `EquipAction` | Wait until item (+ optional socket) equipped |
| `SequenceActionBase` | Base; call `Complete()` when done |
| `ISequenceValidatable` | Editor + runtime validation |
| `GuidanceMode` | Guided (UI on) / NonGuided (task only) |

### Wiring to flow

1. Flow node: **Trigger** + `TaskCompleted`.
2. `SequenceToFlowBridge`:
   - `completionTrigger = TaskCompleted`
   - `startSequenceWhenFlowWaits = true`
   - `bindFlowWaitEvent = true`
3. When sequence completes → `TriggerFlowController.Raise(TaskCompleted)`.

### Notifying actions (from XR or test input)

```csharp
selectAction.NotifySelected(clickedGameObject);
equipAction.NotifyEquipped(itemGameObject, socketTransform);
```

Wrong object → ignored. Null target/item → fails validation (does not auto-complete).

### Custom action

```csharp
public class RotateValveAction : SequenceActionBase, ISequenceValidatable
{
    public bool Validate(out string error) { /* require refs */ }

    public void NotifyRotatedEnough()
    {
        if (IsCompleted) return;
        Complete();
    }
}
```

---

## 9. Zone and custom activities

### Zone

```csharp
// SimpleFlowTriggerZone on trigger collider
triggerToRaise = Continue;  // must match flow node
requiredTag = "Player";     // or empty = any
```

```csharp
TriggerFlowController.Raise(TrainingTrigger.Continue);
```

### Any script

```csharp
public class MyExperiment : MonoBehaviour
{
    public void OnExperimentSuccess()
    {
        TriggerFlowController.Raise(TrainingTrigger.TaskCompleted);
    }
}
```

Or use `ActivityToFlowBridge.CompleteActivity()`.

---

## 10. Assessment and scoring

### AssessmentConfig

| Field | Meaning |
|-------|---------|
| `AssessmentId` | Unique id |
| `Questions` | List of `QuestionData` |
| `ShowResultsScreen` | Pause for section results |
| `PassThresholdPercent` | Pass/fail |
| `NavigationMode` | Sequential / FreeNavigation |

### Question types

`QuestionAnswer`, `YesNo`, `TrueFalse`, `SingleChoice`, `MultipleChoice`

### Runtime chain

```
Assessment node
  → AssessmentController.StartAssessment
  → QuestionController per question
  → section result
  → optional AssessmentResultPanel
  → flow continues (ReportResultsContinue)
```

Overall score: `TrainingScoreController` → completion panel.

---

## 11. UI system

| Component | Role |
|-----------|------|
| `UIFlowController` | Subscribes to flow/assessment events; shows/hides panels |
| `UIPanelFactory` | Instantiates prefabs under panel parent |
| Prefabs | Text, TextImage, Assessment, Result, Complete, Menu, ChoiceOption |

Assign all prefabs on `UIPanelFactory`. Missing **Training Menu Panel** → “Choose a Training” title with **no buttons**.

Completion:

- **Retry** → `RestartSession()`
- **Choose Another** → menu → `StartSession(config)` (only if multiple configs)

---

## 12. Editor menus

| Menu path | Result |
|-----------|--------|
| **Training → Setup Multi-Flow Connected Scene** | Full multi-config + sequence + zone + menu (recommended) |
| Training → Setup Full Combined Demo | Single flow + sequence |
| Training → Setup Full Demo Scene | Flow / quiz only |
| Tools → Training → Generate ChoiceOption Prefab | Choice row prefab helper |

On a `TrainingFlowConfig` asset: **Validate Flow**.

---

## 13. API reference (most used)

### Session

```csharp
session.StartSession();
session.StartSession(flowConfig);
session.StartSession(0);
session.RestartSession();
session.AvailableFlowConfigs;
session.CurrentFlowConfig;
session.HasMultipleFlowConfigs;
TrainingSessionController.OnSessionCompleted; // static
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
OnShowAssessmentResult, OnFlowCompleted, OnBackRequested, OnStateChanged
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

---

## 14. Extending the system

### New world activity between nodes

1. Add a **Trigger** node with a chosen enum value.
2. Implement activity (sequence step, collider, XR event).
3. On success: `TriggerFlowController.Raise(thatEnum)`.

### New sequence action

1. Subclass `SequenceActionBase`.
2. Implement `ISequenceValidatable` if configuration can be wrong.
3. Implement `ISequenceTarget` if it owns a highlight target.
4. Call `Complete()` only when the real interaction succeeds.

### New question type

1. Extend `QuestionType` and `QuestionData`.
2. Handle UI in `QuestionPanel` / `QuestionController`.
3. Extend `AssessmentConfig.Validate`.

### Custom string triggers (future)

Today only the **enum** is supported. For free-form ids, extend `FlowNodeData` + `TriggerFlowController` with a parallel string wait/raise path (see design notes in project history). Prefer enum values for shipping demos.

---

## 15. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `'{Title}' is not a valid TrainingTrigger` | Raised flow title | Raise `TaskCompleted` / `Continue` |
| Stuck on Trigger node | Nothing raises matching enum | Sequence bridge, zone, or debug key **T** |
| Zone does nothing | Flow already Completed, or wrong tag | Enter while `Waiting for…`; tag `Player` or clear tag filter |
| “Choose a Training” but no buttons | Menu prefab / item prefab missing | Assign Training Menu Panel on UIPanelFactory |
| Menu never shows | Bootstrap off or only one config | Enable `showMenuOnStart` + 2 configs; FlowRunner start off |
| Sequence auto-completes | Null target on Select/Equip | Validation + assign targets |
| Input System errors on old `Input` | Legacy Input API | Use Input System (`Key`, `Keyboard.current`) |
| CS0118 `Editor` is a namespace | `namespace Training.Editor` | Use `Training.EditorTools` + `UnityEditor.Editor` |
| Second flow never runs | Only index 0 auto-starts | Menu / Choose Another / `StartSession(1)` |

### Debug key

In Editor, with flow waiting: press **T** (default) to raise the **expected** trigger (`TriggerFlowController` debug key).

---

## 16. Do and don’t

**Do**

- Match Trigger node enum to `Raise(...)` value  
- Validate flows in the Inspector  
- Keep sequence/zone logic out of `TrainingFlowController`  
- Use static `Raise` from activities  
- Use menu or `StartSession(config)` for multi-course scenes  

**Don’t**

- Use flow **Title** as a trigger name  
- Expect the next `AvailableFlowConfigs` entry to auto-start after completion  
- Complete Select/Equip with null targets  
- Put sample-only assets under Runtime (use `Samples~` or `Assets/TrainingDemo` from setup)  

---

## Reference demo flows

**FlowA_PPE** — `TaskCompleted` at node A02 → sequence.  
**FlowB_Zone** — `Continue` at node B02 → orange zone.

Shared assessment asset for both.

---

## File map (runtime)

```
Runtime/Scripts/
  Core/           Enums, interfaces
  Flow/           Session, FlowController, FlowRunner, FlowNodeData, TrainingFlowConfig
  Trigger/        TriggerFlowController, TrainingTriggerSource
  Assessment/     AssessmentConfig, AssessmentController, score
  Question/       QuestionData, QuestionController
  UI/             UIFlowController, panels, UIPanelFactory
  SequenceModule/ Guided sequence, Select/Equip, highlighter, sequence UI
  Integration/    Bridges, zone, bootstrap, mouse test input
```

---

*End of developer guide.*
