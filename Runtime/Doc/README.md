# VR Training Flow & UI Framework

A Unity UPM package for building VR/desktop training courses that a
designer configures entirely through ScriptableObject assets — no code
required per training. A flow is a sequence of steps (content screens,
wait-for-trigger moments, assessments); each assessment is a mix of five
question types; every step, question, and result can carry its own audio,
and the whole thing supports multiple trainings, retries, and a
results/completion screen out of the box.

This README covers **what's in the package and how to wire a scene.** For
every field, event, and method with call examples, see
[`TECHNICAL_DOCUMENTATION.md`](TECHNICAL_DOCUMENTATION.md).

---

## Contents

1. [Package info](#1-package-info)
2. [How a training is built](#2-how-a-training-is-built)
3. [Script inventory](#3-script-inventory)
4. [Scene setup](#4-scene-setup)
5. [Prefabs you need](#5-prefabs-you-need)
6. [Assets you create per training](#6-assets-you-create-per-training)
7. [Unused scaffolding](#7-unused-scaffolding)
8. [Still open](#8-still-open)

---

## 1. Package info

| | |
|---|---|
| Package name | `com.vrtrainingsystem.core` |
| Unity version | 2021.3+ |
| Dependencies | `com.unity.textmeshpro` 3.0.6, `com.unity.ugui` 1.0.0 |
| Layout | `Runtime/Scripts`, `Runtime/Prefabs`, `Runtime/Scenes` (a working `Demo.unity`), `Editor/` |

Add it via Package Manager → **Add package from disk** → point at this
package's `package.json`, or drop the whole `com.vrtrainingsystem.core`
folder into your project's `Packages/` folder directly.

The included `Demo.unity` scene has a working `TrainingSystem` hierarchy
already wired up — the fastest way to see the pieces working together is
to open it and look at how each component's Inspector fields are
assigned, rather than building from scratch first.

---

## 2. How a training is built

- **One `TrainingFlowConfig`** = one training course. A `Start Node Id`
  and a list of `FlowNodeData` — Text, TextImage, Trigger, or Assessment
  steps, chained by `Next Node Id`.
- **One `AssessmentConfig`** per assessment *section* — its own list of
  questions (any mix of the 5 `QuestionType`s), navigation mode
  (Sequential or Free Navigation), pass threshold, and whether to show a
  results screen. Assessment-type flow nodes reference one of these.
- **`TrainingSessionController`** holds a *list* of `TrainingFlowConfig`s
  — this is what makes multiple trainings and a "choose another training"
  menu possible without any extra wiring.
- Every content node, every question, and the completion screen can each
  carry their **own** audio and (for content nodes) an auto-advance delay
  — nothing here relies on external animation or VO systems.

---

## 3. Script inventory

Paths below are relative to the package root.

### Core (`Runtime/Scripts/Core/`)
Shared enums and interfaces. No MonoBehaviours.

| File | Purpose |
|---|---|
| `Enums.cs` | `FlowMode` (unused — see §7), `FlowNodeType`, `QuestionType`, `TrainingTrigger`, `NavigationMode`, `AssessmentNavigationMode` |
| `IFlowController.cs` | `StartFlow()`, `StopFlow()`, `OnFlowCompleted` |
| `IUIPanel.cs` | `Show()`, `Hide()` |

### Flow (`Runtime/Scripts/Flow/`)

| File | Purpose |
|---|---|
| `Controllers/TrainingFlowController.cs` | **The engine.** Resolves and advances flow nodes; owns node/trigger-node audio, auto-advance timing, and the assessment-results pause. |
| `Controllers/TrainingSessionController.cs` | Owns a session's lifecycle across a *list* of available `TrainingFlowConfig`s — start, restart, switch training, Back-navigation history. |
| `Controllers/TrainingTriggerSource.cs` | Put this on anything that should *raise* a `TrainingTrigger` — a button, a collider, an XR interactable, or a timed auto-fire. Nothing else in the package raises triggers on its own. |
| `Components/FlowRunner.cs` | Scene entry point — calls `TrainingSessionController.StartSession()` on `Start()`. |
| `Data/FlowNodeData.cs` | One flow step — type, next node, per-node audio, trigger, navigation, auto-advance delay, text/image, or an `AssessmentConfig` reference. |
| `Data/TrainingFlowConfig.cs` | **ScriptableObject.** One training: title, completion-screen settings, start node, and the node list. Has `Validate()`. |
| `Data/FlowRuntimeData.cs` | Unused scaffolding — see §7. |
| `State/FlowState.cs` | `NotStarted / ShowingContent / WaitingForTrigger / RunningAssessment / ShowingResults / Completed`, tracked live on `TrainingFlowController.CurrentState`. |

### Trigger (`Runtime/Scripts/Trigger/`)

| File | Purpose |
|---|---|
| `Controllers/TriggerFlowController.cs` | Waits for a specific `TrainingTrigger` and resolves a callback when raised. Buffers early triggers; has an in-Editor debug key (`T`) to fire whatever's currently being waited on. |
| `Data/TrainingTriggerData.cs` | Unused scaffolding — see §7. |

### Question (`Runtime/Scripts/Question/`)

| File | Purpose |
|---|---|
| `Controllers/QuestionController.cs` | Runs and validates exactly one question at a time. |
| `Data/QuestionData.cs` | One question — type, text, per-question audio, and whichever answer fields its `QuestionType` actually uses. |
| `Data/QuestionResultData.cs` | Plain runtime result for one answered question. |

### Assessment (`Runtime/Scripts/Assessment/`)

| File | Purpose |
|---|---|
| `Controllers/AssessmentController.cs` | Runs a whole assessment section — Sequential auto-advance *and* Free Navigation (Previous/Next/Finish/jump-to). Plays per-question audio. |
| `Controllers/TrainingScoreController.cs` | Accumulates finished sections into the final score, keyed by `AssessmentId` so Retry replaces rather than stacks. |
| `Controllers/AssessmentResultPanel.cs` | *(UI panel; filed under Controllers for historical reasons — namespace is `Training.UI.Panels`.)* Shown after one assessment section if `Show Results Screen` is on — score, pass/fail, Continue + Retry. |
| `Data/AssessmentConfig.cs` | **ScriptableObject.** One assessment section. Has `Validate()`. |
| `Data/AssessmentSectionResultData.cs` | Plain runtime result for one finished section. |
| `Data/TrainingScoreData.cs` | Plain runtime result for the whole session. |

### UI (`Runtime/Scripts/UI/`)

| File | Purpose |
|---|---|
| `Controllers/UIFlowController.cs` | **The bridge.** Listens to every flow/assessment/question event and creates/destroys the right panel via `UIPanelFactory`. Never decides what happens next. |
| `Components/UIPanelFactory.cs` | Instantiates panels from Inspector-assigned prefabs. |
| `Panels/UIPanelBase.cs` | Abstract `Show()`/`Hide()` base. |
| `Panels/TextPanel.cs` / `TextImagePanel.cs` | Content screens, optional Next/Back. |
| `Panels/QuestionPanel.cs` | Renders any `QuestionType` — spawns numbered choice options, colors right/wrong feedback, handles free text and multi-select. |
| `Panels/AssessmentPanel.cs` | Hosts a `QuestionPanel`, shows progress, and the Free Navigation Previous/Next/Finish controls. |
| `Panels/TrainingCompletePanel.cs` | Shown when the **whole session** finishes. Two layouts (Full Detail / Final Quiz Panel) — see §6. |
| `Panels/TrainingMenuPanel.cs` | Lists every available training as a button; reports which one got picked. |
| `Data/UIData.cs` | `TextPanelData`/`TextImagePanelData` + `TrainingUISettings` — all unused, see §7. |

### Editor (`Editor/`)
Compiled Editor-only (`includePlatforms: ["Editor"]` in the asmdef) — never ships in a build.

| File | Purpose |
|---|---|
| `AssessmentConfigEditor.cs` | Custom Inspector for `AssessmentConfig` — labeled sections, contextual help text. |
| `TrainingFlowConfigEditor.cs` | Custom Inspector for `TrainingFlowConfig` — only shows completion-screen fields relevant to the current toggle combination (e.g. hides `Calculate Score` entirely when `Show Completion Screen` is off, grays it out when `Show Final Quiz Panel` is on). |
| `FlowNodeDataDrawer.cs` | Collapses each `FlowNodeData` into a foldout, shows only the fields the selected `NodeType` actually uses. |
| `QuestionDataDrawer.cs` | Same idea, for `QuestionData` per `QuestionType`. |
| `ChoiceOptionPrefabCreator.cs` | **Tools → Training → Generate ChoiceOption Prefab.** Builds the choice-option prefab `QuestionPanel` needs, with correct component wiring (no stray hover-tint or leftover Toggle). |

---

## 4. Scene setup

Create one GameObject (e.g. `TrainingSystem`) and attach:

- `Training Session Controller`
- `Training Flow Controller`
- `Trigger Flow Controller`
- `Assessment Controller`
- `Question Controller`
- `Training Score Controller`
- `UI Panel Factory`
- `UI Flow Controller`
- `Flow Runner`

Wire the Inspector references:

| Component | Field | Points to |
|---|---|---|
| Training Session Controller | Available Flow Configs | every `TrainingFlowConfig` you want runnable — index 0 auto-starts |
| | Flow Controller | Training Flow Controller |
| | Score Controller | Training Score Controller |
| Training Flow Controller | Trigger Flow Controller | Trigger Flow Controller |
| | Assessment Controller | Assessment Controller |
| | Score Controller | Training Score Controller |
| | Audio Source | *(optional)* plays Text/TextImage/Trigger node audio |
| Assessment Controller | Question Controller | Question Controller |
| | Audio Source | *(optional)* plays per-question audio |
| UI Flow Controller | Panel Factory | UI Panel Factory |
| | Flow Controller / Assessment Controller / Question Controller | (matching components) |
| | Session Controller | Training Session Controller — needed for Restart/Choose Another |
| Flow Runner | Session Controller | Training Session Controller |

For any `Trigger`-type node to ever resolve, add a `Training Trigger
Source` on whatever represents task completion in the scene, point its
`Trigger Flow Controller` field at the one above, and match its `Trigger`
dropdown to the node.

---

## 5. Prefabs you need

Assign all of these to `UIPanelFactory`.

| Prefab | Script | Required? |
|---|---|---|
| `TextPanel` | `TextPanel.cs` | Yes |
| `TextImagePanel` | `TextImagePanel.cs` | Yes |
| `AssessmentPanel` (with nested `QuestionPanel`) | `AssessmentPanel.cs` / `QuestionPanel.cs` | Yes, if any Assessment nodes exist |
| `ChoiceOption` | *(no script — plain Button/Image/label, found by name)* | Yes, generate via **Tools → Training → Generate ChoiceOption Prefab** |
| `AssessmentResultPanel` | `AssessmentResultPanel.cs` | Only if any `AssessmentConfig.Show Results Screen` is on |
| `TrainingCompletePanel` | `TrainingCompletePanel.cs` | Recommended — without it, Restart/Choose Another are unreachable |
| `TrainingMenuPanel` | `TrainingMenuPanel.cs` | Only if `Available Flow Configs` has more than one entry |

---

## 6. Assets you create per training

1. **`AssessmentConfig`** (`Create > Training > Assessment > Assessment Config`) — `Assessment Id` (unique!), `Questions`, `Navigation Mode`, `Pass Threshold Percent`, `Show Results Screen`.
2. **`TrainingFlowConfig`** (`Create > Training > Flow > Training Flow Config`) — `Title`, `Start Node Id`, `Nodes`, and the completion-screen settings below.

### Completion screen settings, per training

| Field | Effect |
|---|---|
| `Show Completion Screen` | Off = nothing shown when this training ends (score is still computed and delivered via `TrainingSessionController.OnSessionCompleted`). |
| `Calculate Score` | On = full layout: title, score %, detail text, pass/fail badge, Retry + Choose Another. Ignored while `Show Final Quiz Panel` is on. |
| `Show Final Quiz Panel` | On = a different, simpler layout instead: completion image + **Retry / Home / Quiz** buttons, no score text at all. Takes priority over `Calculate Score`. Home and Quiz fire their events (`OnHomePressed`/`OnQuizPressed`) with **no behavior wired to them** — that's intentionally left for your project to hook up. |
| `Completion Image` | Only used by the Final Quiz Panel layout. Ignored (and hidden in the Inspector) otherwise. |

---

## 7. Unused scaffolding

Present because the original architecture doc's file tree names them, but
nothing reads or writes them. Safe to leave, safe to delete:

- `FlowMode` enum (`Core/Enums.cs`)
- `FlowRuntimeData` (`Flow/Data/FlowRuntimeData.cs`)
- `TrainingTriggerData` (`Trigger/Data/TrainingTriggerData.cs`)
- `TextPanelData` / `TextImagePanelData` / `TrainingUISettings` (`UI/Data/UIData.cs`)

---

## 8. Still open

- **No VR device/headset testing has been done.** Everything so far runs
  as flat UI in Editor Play mode. No World Space Canvas confirmed, no XR
  Rig, no controller-based interaction, no headset performance pass.
- `AssessmentConfig.Validate()` doesn't check that a question's configured
  correct answer is itself valid (e.g. an empty `Correct Answer Text` on a
  Question Answer type) — only that choice questions have options.
- Home/Quiz buttons on the Final Quiz Panel layout have no behavior — by
  design, but worth remembering before you ship expecting them to do
  something.