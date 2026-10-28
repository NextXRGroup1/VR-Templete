# Training Framework — Complete Developer Guide

**Package:** `com.training.framework` **v0.0.2** (VR-Templete / TrainingFramework)  
**Unity:** 6000.0+ (Unity 6)  
**Dependencies:** TextMeshPro, uGUI, Input System  

This guide covers **every module** in the package: flow, session, triggers, Active/Animation nodes, sequence (Guided/NonGuided), checklist/progress, assessment, UI panels, integration, editor tools, samples, and extension points.

---

## Table of contents

1. [What this package is](#1-what-this-package-is)
2. [Core concepts](#2-core-concepts)
3. [Architecture](#3-architecture)
4. [Quick start](#4-quick-start)
5. [Enums reference](#5-enums-reference)
6. [TrainingFlowConfig & FlowNodeData](#6-trainingflowconfig--flownodedata)
7. [Node types (all)](#7-node-types-all)
8. [Flow runtime & FlowState](#8-flow-runtime--flowstate)
9. [Session & multiple configs](#9-session--multiple-configs)
10. [Triggers](#10-triggers)
11. [Active node](#11-active-node)
12. [Animation node](#12-animation-node)
13. [Sequence module (Guided / NonGuided)](#13-sequence-module-guided--nonguided)
14. [Checklist & Progress](#14-checklist--progress)
15. [Zone & custom activities](#15-zone--custom-activities)
16. [Assessment & questions](#16-assessment--questions)
17. [Scoring & results](#17-scoring--results)
18. [UI system (all panels)](#18-ui-system-all-panels)
19. [Audio](#19-audio)
20. [Integration components](#20-integration-components)
21. [AR / XR usage](#21-ar--xr-usage)
22. [Editor menus & drawers](#22-editor-menus--drawers)
23. [Samples](#23-samples)
24. [API reference](#24-api-reference)
25. [Extending the system](#25-extending-the-system)
26. [Troubleshooting](#26-troubleshooting)
27. [Do and don’t](#27-do-and-dont)
28. [File map](#28-file-map)

---

## 1. What this package is

Flow-driven training runtime + world activities. **XR-agnostic** (VR, AR, desktop).

| Module | Responsibility |
|--------|----------------|
| **Core** | Enums, `IFlowController`, `IUIPanel` |
| **Flow** | Course graph, session, Active/Animation nodes, FlowRunner |
| **Trigger** | Wait/raise signals (`TrainingTrigger`) |
| **Sequence** | Linear steps, Guided/NonGuided UI, Select/Equip |
| **Checklist / Progress** | Multi-item checks, min/max progress → flow signal |
| **Assessment / Question** | Quizzes, feedback, section results |
| **Score** | Aggregate score, pass/fail |
| **UI** | Text, quiz, results, menu, complete, checklist, progress |
| **Integration** | Bridges sequence/zone/checklist → triggers |
| **Editor** | Drawers, validation, one-click demo setup |

---

## 2. Core concepts

### Flow vs trigger

| | **Training flow** | **Trigger** |
|--|-------------------|-------------|
| What | Whole course asset | Signal that unlocks a **Trigger** node |
| Type | `TrainingFlowConfig` | `TrainingTrigger` enum |
| Example | Title `"PPE Hands-On Training"` | `TaskCompleted`, `Continue` |
| Use | Session → Available Flow Configs | `TriggerFlowController.Raise(...)` |

**Never** pass the flow **title** to `RaiseByName`. Only enum names.

### Scene refs vs ScriptableObject

`TrainingFlowConfig` is a Project asset. **Scene** `GameObject` / `Animator` lists do not store reliably on it.

| On flow asset | On scene component |
|---------------|-------------------|
| Node Id, type, next, text, trigger enum | Active enable/disable lists |
| Assessment reference | Animation Animator + keys |
| | Checklist/Progress controllers |

Match by **Node Id** where needed.

---

## 3. Architecture

```
TrainingSessionController
        │ StartSession(config)
        ▼
TrainingFlowController
        ├─ Text / TextImage     → UIFlowController → TextPanel / TextImagePanel
        ├─ Active                 → ActiveNodeController
        ├─ Animation              → AnimationNodeController → next when done
        ├─ Trigger                → TriggerFlowController.WaitForTrigger
        │                              ▲
        │         Raise(TaskCompleted | Continue | …)
        │         Sequence / Zone / Checklist / Progress / custom
        └─ Assessment             → AssessmentController → QuestionController
                                         → results → ReportResultsContinue
                                         → score → TrainingCompletePanel
```

---

## 4. Quick start

| Menu | Creates |
|------|---------|
| **Training → Setup Full Module Demo (v0.0.2)** | **All modules** in one scene (recommended) |
| **Training → Setup Multi-Flow Connected Scene** | 2 flows, sequence, zone, menu |
| **Training → Setup Checklist & Progress Demo** | Checklist/progress sample |
| **Training → Setup Full Combined Demo** | Flow + sequence |
| **Training → Setup Full Demo Scene** | Flow / quiz only |

### Full Module Demo path (v0.0.2)

```
Text → Active (open bay) → Animation (hatch spin)
  → Text → Sequence PPE (Guided) [TaskCompleted]
  → Text → Zone [Continue]
  → Text → Checklist keys 1/2/3 [InteractionCompleted]
  → Text → Progress +/= [ConditionMet]
  → Assessment → Text → Complete
```

1. Import package.  
2. Empty scene → **Training → Setup Full Module Demo (v0.0.2)**.  
3. Play.

---

## 5. Enums reference

```csharp
public enum FlowNodeType
{
    Text,        // 0
    TextImage,   // 1
    Assessment,  // 2
    Trigger,     // 3
    Active,      // 4 — enable/disable objects
    Animation    // 5 — play Animator then advance
}

public enum TrainingTrigger
{
    None, Start, Continue, InteractionCompleted,
    TaskCompleted, ConditionMet,
    AssessmentStarted, AssessmentCompleted
}

public enum NavigationMode { None, Next, BackNext }

public enum AssessmentNavigationMode { Sequential, FreeNavigation }

public enum QuestionType
{
    QuestionAnswer, YesNo, TrueFalse, SingleChoice, MultipleChoice
}

public enum GuidanceMode
{
    Guided,     // title + description + hint
    NonGuided   // task text only
}

public enum TrainingAnimationPlayMode
{
    Once, Loop, OnceStopAtPercent
}

public enum FlowState
{
    NotStarted, ShowingContent, WaitingForTrigger,
    RunningAssessment, ShowingResults, Completed
    // (exact set may include Idle variants — check FlowState.cs)
}

public enum FlowMode { Sequential, Trigger } // legacy/alternate labeling
```

**Do not** use `UnityEngine.AnimationPlayMode` for training nodes — use **`TrainingAnimationPlayMode`**.

---

## 6. TrainingFlowConfig & FlowNodeData

### TrainingFlowConfig fields

| Field | Meaning |
|-------|---------|
| `Title` | Menu / completion display name (**not** a trigger) |
| `DisplayTitle` | Title or asset name |
| `StartNodeId` | First node |
| `Nodes` | List of `FlowNodeData` |
| `ShowCompletionScreen` | Show final panel when session ends |
| `CalculateScore` | Show score on completion |
| `ShowFinalQuizPanel` | Alternate completion layout (image + Home/Quiz; less score detail) |
| `CompletionImage` | Sprite for Final Quiz layout |

**Validate:** Inspector **Validate Flow** or `config.Validate(out string error)`.

Checks: start exists, unique ids, next targets, no cycles, reachable nodes, trigger ≠ None, assessment valid.

### FlowNodeData fields

| Field | Used by |
|-------|---------|
| `Id` | All nodes |
| `NodeType` | All |
| `NextNodeId` | All (empty = end) |
| `PlayAudio` / `AudioClip` | Text, TextImage, Trigger |
| `Trigger` | Trigger nodes |
| `Navigation` | Text / TextImage (`None` / `Next` / `BackNext`) |
| `AutoAdvanceDelaySeconds` | Content when Navigation None; Active delay |
| `TextContent` | Text / Trigger help |
| `Image` | TextImage |
| `AssessmentReference` | Assessment |

---

## 7. Node types (all)

| Type | Behaviour | Advances when |
|------|-----------|---------------|
| **Text** | Show text panel | Next / Back / auto-delay |
| **TextImage** | Text + image | Same |
| **Trigger** | Wait for `TrainingTrigger` | Matching `Raise` |
| **Active** | Enable/disable scene objects | Immediate or delay |
| **Animation** | Play Animator | Anim complete / % / timeout / fire-and-continue |
| **Assessment** | Run quiz | Assessment finished (+ results Continue if shown) |

Example graph:

```
Text → Active → Animation → Text → Trigger (sequence)
  → Text → Trigger (zone) → Assessment → Text → end
```

---

## 8. Flow runtime & FlowState

### TrainingFlowController

```csharp
Initialize(TrainingFlowConfig config);
StartFlow();
StopFlow();
JumpToNode(string id);
ReportNext();    // UI Next
ReportBack();    // UI Back
ReportResultsContinue();
ReportResultsRetry();
FlowState CurrentState;
```

### Events

| Event | When |
|-------|------|
| `OnNodeEntered` | Any node entered |
| `OnShowContentNode` | Text / TextImage |
| `OnWaitingForTrigger` | Trigger node waiting |
| `OnActiveNode` | Active node |
| `OnAnimationNode(node, onComplete)` | Animation node; call `onComplete` when done |
| `OnShowAssessmentResult` | Section results |
| `OnAutoAdvanceStarted(float seconds)` | Auto-advance timer started |
| `OnBackRequested` | Back pressed (session uses history) |
| `OnStateChanged` | FlowState changed |
| `OnFlowCompleted` | Graph finished |

### Navigation behaviour

- `NavigationMode.None` + delay &gt; 0 → auto-advance after delay.  
- `None` + delay ≤ 0 → advance immediately.  
- `Next` → wait for UI Next.  
- `BackNext` → Next + Back; session keeps **node history** for Back.

### FlowRunner

```csharp
[SerializeField] TrainingSessionController _sessionController;
[SerializeField] bool _startOnAwake = true;
// Start() → StartSession() if _startOnAwake
```

For multi-config **menu first**: `_startOnAwake = false` + `MultiFlowSceneBootstrap`.

---

## 9. Session & multiple configs

### TrainingSessionController

```csharp
List<TrainingFlowConfig> _availableFlowConfigs; // index 0 = default

StartSession();
StartSession(TrainingFlowConfig config);
StartSession(int index);
RestartSession();  // same current config

AvailableFlowConfigs;
CurrentFlowConfig;
HasMultipleFlowConfigs; // Count > 1

static event Action<TrainingScoreData> OnSessionCompleted;
```

| Situation | Behaviour |
|-----------|-----------|
| FlowRunner start on awake | Starts index 0 (or current) |
| Menu bootstrap | User picks config |
| Completion **Retry** | `RestartSession()` |
| Completion **Choose Another** | Menu → `StartSession(config)` |

**Does not** auto-run the next list entry after one flow completes.

On flow complete → score finalize → `OnSessionCompleted` → UI may show `TrainingCompletePanel`.

---

## 10. Triggers

### Static API (preferred)

```csharp
TriggerFlowController.Raise(TrainingTrigger.TaskCompleted);
TriggerFlowController.RaiseByName("Continue"); // enum name only
```

Controller must be **enabled** (subscribes to `OnGlobalTriggerRaised`).

### Instance API

```csharp
WaitForTrigger(expected, onMatched);
RaiseTrigger(trigger);
CancelWait();
IsWaiting; ExpectedTrigger;
OnTriggerReceived; OnWaitStarted;
```

Early raises can be **buffered** until wait starts.

### Editor debug

- `_enableDebugKey` + `_debugKey` (default **T**): raises **currently expected** trigger in Editor.

### TrainingTriggerSource

Optional component to raise a configured trigger from UnityEvent / button without code.

---

## 11. Active node

1. Flow: `NodeType = Active`, set **Id**, **NextNodeId**, optional delay.  
2. Scene: **ActiveNodeController** + bindings:

| Binding | Meaning |
|---------|---------|
| Node Id | Matches flow node Id |
| Objects To Enable | `SetActive(true)` |
| Objects To Disable | `SetActive(false)` |

Runtime: `OnActiveNode` → apply binding → advance.

---

## 12. Animation node

1. Flow: `NodeType = Animation`, **Id**, **NextNodeId**.  
2. Scene: **AnimationNodeController** bindings:

| Field | Purpose |
|-------|---------|
| Node Id | Match |
| Target | GameObject with Animator |
| Trigger Key | `SetTrigger` name |
| State Name | Optional state to wait on |
| Play Mode | `TrainingAnimationPlayMode` |
| Stop At Percent | 1–100 |
| Layer | Animator layer |
| Timeout Seconds | Failsafe |
| Fire And Continue | Advance without waiting |

Flow: `OnAnimationNode(node, done)` → play → `done()` → `MoveToNode`.

---

## 13. Sequence module (Guided / NonGuided)

### Pieces

| Type | Role |
|------|------|
| `GuidedSequenceController` | Runs steps; `Mode`; `OnSequenceCompleted` |
| `GuidedStep` | Per-step content + action |
| `SelectAction` | Correct object selected |
| `EquipAction` | Item (+ optional socket) equipped |
| `SequenceActionBase` | `StartAction`, `Complete`, `OnCompleted` |
| `ISequenceValidatable` | `Validate(out error)` |
| `ISequenceTarget` | Exposes `Target` for highlight |
| `IHighlighter` / `SimpleHighlighter` | Highlight current target |
| `ISequenceUI` / `SequenceUIPanel` | Guided vs task-only UI |

### GuidanceMode (important)

| Mode | UI |
|------|-----|
| **Guided** | `ShowGuided(Title, Description, Hint)` — full coaching |
| **NonGuided** | `ShowTaskOnly(TaskText)` — task line only |

Set on controller: `GuidedSequenceController.Mode`.

### Every step should define

| Field | Guided | NonGuided |
|-------|--------|-----------|
| **TaskText** | Recommended | **Required** (only line shown) |
| **Title** | **Shown** | Hidden |
| **Description** | **Shown** | Hidden |
| **Hint** | Optional shown | Hidden |
| **Action** | Required | Required |

Fill **all** text fields even if you start in NonGuided so switching mode still works.

### SequenceUIPanel

- Guided: shows title, description, hint; hides task field.  
- NonGuided: shows task only; hides title/description/hint.  
- `Hide()` when sequence ends.

### Wiring to flow

1. Trigger node e.g. `TaskCompleted`.  
2. `SequenceToFlowBridge`: same trigger, `startSequenceWhenFlowWaits`, bind wait event.  
3. Sequence complete → `Raise(TaskCompleted)`.

### Notifying actions

```csharp
selectAction.NotifySelected(gameObject);
equipAction.NotifyEquipped(item, socketTransform);
```

Defensive: null target/item does not complete; wrong object ignored.

### Events

```csharp
OnSequenceCompleted;
OnStepStarted(int index, GuidedStep step);
OnStepCompleted(int index, GuidedStep step);
```

### Editor

- `GuidedSequenceControllerEditor`, `GuidedStepDrawer` — nicer Inspector for steps/actions.

---

## 14. Checklist & Progress

### ChecklistController

```csharp
Setup(List<ChecklistItemDef> items); // Id + Label each
MarkComplete(string id);
MarkIncomplete(string id);
ResetAll();
IsChecked(id); IsAllComplete; Progress01;
OnItemChanged; OnAllCompleted; OnReset;
```

Define **all** items first → mark one by one → **all complete** → signal flow.

### ProgressController

```csharp
Setup(label, min, max, completeAtMax: true, manualOnly: false);
Add(float); Set(float); ResetProgress();
Value; Normalized; IsComplete; ManualAdvanceOnly;
OnChanged(value, normalized); OnCompleted; OnReset;
```

| completeWhenMax | manualAdvanceOnly | Result |
|-----------------|-------------------|--------|
| true | false | At max → auto Raise / next |
| true | true | At max → `OnReadyForNext` only (enable Next yourself) |
| false | * | Never auto-complete via progress |

### UI

- `ChecklistPanel` + `ChecklistRowView` — bind with `Bind(controller, title)`.  
- `ProgressPanel` — fill Image (Filled) + labels; `Bind(controller)`.

### Bridges

| Component | Behaviour |
|-----------|-----------|
| `ChecklistProgressFlowBridge` | Checklist all done and/or progress max → one `completionTrigger` |
| `ChecklistThenProgressBridge` | Checklist → `TaskCompleted`; progress → `Continue` |
| `ChecklistProgressDemoInput` | Keys **1/2/3** mark items; **+/=** add progress |

### Sample menu

**Training → Setup Checklist & Progress Demo**  
Creates prefabs under `Assets/TrainingDemo/Prefabs/` and a sample flow.

---

## 15. Zone & custom activities

### SimpleFlowTriggerZone

- Collider **Is Trigger**.  
- `TrainingTrigger triggerToRaise` (enum, not flow title).  
- Optional `requiredTag` (e.g. Player).  
- `TriggerFlowController.Raise(triggerToRaise)`.

### ActivityToFlowBridge

```csharp
CompleteActivity();
CompleteActivityWith(TrainingTrigger t);
```

### Any script

```csharp
TriggerFlowController.Raise(TrainingTrigger.TaskCompleted);
```

---

## 16. Assessment & questions

### AssessmentConfig

| Field | Meaning |
|-------|---------|
| `AssessmentId` | Unique |
| `Questions` | List of `QuestionData` |
| `ShowResultsScreen` | Section results panel |
| `PassThresholdPercent` | Pass/fail |
| `NavigationMode` | Sequential / FreeNavigation |
| Optional assessment-level audio | See asset |

### QuestionData

| Field | Types |
|-------|--------|
| `Id`, `QuestionType`, `QuestionText` | All |
| `PlayAudio`, `AudioClip` | Per-question audio |
| `Options`, `CorrectOptionIndices` | Single/Multiple choice |
| `CorrectAnswerText` | QuestionAnswer |
| `CorrectYesNoAnswer` | YesNo |
| `CorrectTrueFalseAnswer` | TrueFalse |
| `Points` | Scoring |

### Runtime chain

```
Assessment node
  → AssessmentController.StartAssessment
  → QuestionController shows each question
  → feedback delay
  → section result
  → optional AssessmentResultPanel → ReportResultsContinue
  → next flow node
```

---

## 17. Scoring & results

### TrainingScoreController

Accumulates section results; `FinalizeScore()` → `TrainingScoreData` (score %, correct/total, passed, section list).

### AssessmentResultPanel

After a **section** (if `ShowResultsScreen`). Continue / Retry hooks into flow `ReportResultsContinue` / `ReportResultsRetry`.

### TrainingCompletePanel

After **whole session** (`OnSessionCompleted`):

| Layout | When |
|--------|------|
| Full detail | `CalculateScore` and not Final Quiz panel |
| Final Quiz panel | `ShowFinalQuizPanel` — image + Retry/Home/Quiz; less score text |

Events: `OnRestartPressed`, `OnChooseAnotherPressed`, `OnHomePressed`, `OnQuizPressed` (Home/Quiz not wired inside package — project-specific).

`SetChooseAnotherVisible(HasMultipleFlowConfigs)`.

---

## 18. UI system (all panels)

### UIPanelBase

`Show()` / `Hide()` — activate GameObject.

### UIPanelFactory

Prefabs: Text, TextImage, Assessment, AssessmentResult, TrainingComplete, TrainingMenu, **Checklist**, **Progress**.  
Parent transform for instances.

```csharp
GetTextPanel(); GetTextImagePanel(); GetAssessmentPanel();
GetAssessmentResultPanel(); GetTrainingCompletePanel(); GetTrainingMenuPanel();
GetChecklistPanel(); GetProgressPanel();
GetPanelForNode(FlowNodeType); // content types only
```

### UIFlowController

Subscribes to flow/assessment/session events; spawns/shows panels; wires Next/Back/Continue/Restart/Choose Another.

### Panel list

| Panel | Role |
|-------|------|
| TextPanel | Body text, Next/Back as needed |
| TextImagePanel | Text + image |
| AssessmentPanel | Hosts QuestionPanel |
| QuestionPanel | Options / YesNo / feedback |
| AssessmentResultPanel | Section results |
| TrainingCompletePanel | Session end |
| TrainingMenuPanel | List of flow configs |
| ChecklistPanel | Checklist rows |
| ProgressPanel | Progress fill |
| SequenceUIPanel | Sequence guided/task UI (separate from factory often) |

Missing **Training Menu** prefab → “Choose a Training” title with **no buttons**.

---

## 19. Audio

| Location | Behaviour |
|----------|-----------|
| Flow node `PlayAudio` + `AudioClip` | Played when node runs (if assigned) |
| Assessment config audio | Optional whole-assessment clip |
| Question `PlayAudio` + `AudioClip` | Per-question |
| Sequence narration | Not a separate system yet — Guided mode is UI; hook your own audio to `OnStepStarted` if needed |

Missing clip + PlayAudio true → plays nothing (not an error).

---

## 20. Integration components

| Component | Role |
|-----------|------|
| `SequenceToFlowBridge` | Wait trigger → start sequence; complete → Raise |
| `ActivityToFlowBridge` | Manual complete → Raise |
| `SimpleFlowTriggerZone` | Collider → Raise |
| `MultiFlowSceneBootstrap` | Menu on start if multiple configs |
| `CombinedSequenceInput` | Demo mouse/keys for Select/Equip |
| `ChecklistProgressFlowBridge` | Checklist/progress → Raise |
| `ChecklistThenProgressBridge` | Two-phase TaskCompleted then Continue |
| `ChecklistProgressDemoInput` | 1/2/3 and +/- demo keys |

---

## 21. AR / XR usage

Core does not depend on a VR SDK.

| Concept | VR | AR |
|---------|----|----|
| Select | Grab / ray | Tap / raycast / tracked object |
| Equip | Socket | Anchor / tracked socket |
| Zone | Collider | Plane / geo / image marker |
| UI | Screen or world canvas | Same scripts, different canvas mode |

```csharp
selectAction.NotifySelected(hitObject);
TriggerFlowController.Raise(TrainingTrigger.Continue);
```

---

## 22. Editor menus & drawers

| Menu | Purpose |
|------|---------|
| Training → Setup Multi-Flow Connected Scene | Full multi-config demo |
| Training → Setup Checklist & Progress Demo | Checklist/progress prefabs + flow |
| Training → Setup Full Combined Demo | Flow + sequence |
| Training → Setup Full Demo Scene | Flow/quiz |
| Tools → Training → Generate ChoiceOption Prefab | Choice row |

| Drawer / editor | Target |
|-----------------|--------|
| FlowNodeDataDrawer | Per-type fields (incl. Active/Animation help) |
| TrainingFlowConfigEditor | Validate Flow |
| AssessmentConfigEditor | Assessment asset |
| QuestionDataDrawer | Question fields by type |
| GuidedSequenceControllerEditor | Sequence |
| GuidedStepDrawer | Step fields |

Namespaces: use **`Training.EditorTools`** and **`UnityEditor.Editor`** (avoid `namespace Training.Editor` → CS0118).

---

## 23. Samples

```
Samples~/FullDemo/
  Prefabs/     Text, Assessment, Complete, Menu, …
  ScriptableObjects/  FlowA_PPE, FlowB_Zone (subset sample)
  DEVELOPER_GUIDE.md
```

**v0.0.2 full sample** is created in the open scene by:

**Training → Setup Full Module Demo (v0.0.2)** → assets under `Assets/TrainingDemo_v002/`

That run wires Text, Active, Animation, Guided sequence, Zone, Checklist, Progress, Assessment, Complete.

Other setup menus write under `Assets/TrainingDemo/`.

---

## 24. API reference (summary)

### Session

`StartSession()`, `StartSession(config)`, `StartSession(index)`, `RestartSession()`,  
`AvailableFlowConfigs`, `CurrentFlowConfig`, `HasMultipleFlowConfigs`,  
`OnSessionCompleted`

### Flow

`Initialize`, `StartFlow`, `StopFlow`, `JumpToNode`,  
`ReportNext`, `ReportBack`, `ReportResultsContinue`, `ReportResultsRetry`,  
events listed in §8

### Triggers

`Raise`, `RaiseByName`, `WaitForTrigger`, `RaiseTrigger`, `IsWaiting`, `ExpectedTrigger`

### Sequence

`StartSequence`, `Mode`, `Steps`, `OnSequenceCompleted`, `OnStepStarted`, `OnStepCompleted`,  
`NotifySelected`, `NotifyEquipped`

### Checklist / Progress

See §14

### Active / Animation

`OnActiveNode`, `OnAnimationNode`, scene controllers Apply/Play

---

## 25. Extending the system

### New world activity

Trigger node + `Raise` on success.

### New sequence action

Subclass `SequenceActionBase`; optional `ISequenceValidatable`, `ISequenceTarget`; call `Complete()` only on real success.

### New node type

1. `FlowNodeType` value  
2. `ExecuteNode` branch  
3. Scene controller if scene refs needed  
4. FlowNodeDataDrawer case  

### New question type

Extend `QuestionType` + `QuestionData` + QuestionPanel UI + validation.

---

## 26. Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'{Title}' is not a valid TrainingTrigger` | Raise enum name, not flow title |
| Stuck on Trigger | Raise matching enum; or key **T** in Editor |
| Zone no effect | Enter while Waiting; tag Player |
| Menu no buttons | Assign TrainingMenuPanel + item prefab |
| Sequence auto-completes | Assign Select/Equip targets; validation |
| CS0104/CS0117 AnimationPlayMode | Use `TrainingAnimationPlayMode` |
| Active/Animation no effect | Binding Node Id must match |
| Animation never advances | State name / timeout |
| Guided UI empty | Fill Title, Description, Hint |
| NonGuided empty | Fill TaskText |
| Checklist never completes | MarkComplete with exact Ids from Setup |
| Progress never completes | Add until max; check completeWhenMax |
| Second flow never runs | Menu / Choose Another / StartSession(1) |
| CS0118 Editor namespace | Training.EditorTools + UnityEditor.Editor |

---

## 27. Do and don’t

**Do**

- Match trigger enum to `Raise`  
- Validate flows  
- Scene controllers for Active/Animation/Checklist UI  
- Fill all GuidedStep text fields  
- Use `TrainingAnimationPlayMode`  
- Static `Raise` from activities  

**Don’t**

- Use flow Title as trigger  
- Expect next AvailableFlowConfig to auto-start  
- Put scene GameObject arrays on the SO flow asset  
- Complete actions with null targets  
- Confuse Guided UI fields with NonGuided TaskText  

---

## 28. File map

```
Runtime/Scripts/
  Core/           Enums, IFlowController, IUIPanel
  Flow/           Config, nodes, session, flow controller, FlowRunner,
                  ActiveNodeController, AnimationNodeController, state
  Trigger/        TriggerFlowController, TrainingTriggerSource
  Assessment/     Config, AssessmentController, score, result panel
  Question/       QuestionData, QuestionController
  SequenceModule/ GuidedSequence, GuidedStep, GuidanceMode,
                  Select/Equip, highlighter, SequenceUIPanel
  UI/             UIFlowController, UIPanelFactory, panels,
                  Checklist/, Progress/
  Integration/    Bridges, zone, bootstrap, demo inputs

Editor/           Setup menus, drawers, validators
Samples~/FullDemo Prefabs + notes
Runtime/Doc/      Guides
```

---

*End of complete developer guide.*
