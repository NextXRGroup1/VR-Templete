# VR Training Flow & UI Framework — Technical Documentation

Every field, event, and method in every script, with what it's for, when
it's called, and how to call it yourself if you're extending the system.
Organized in the same module order as the README's inventory.

Conventions used throughout:
- `[SerializeField] private` fields are Inspector-only.
- `public event Action ...` — subscribe with `+=`, unsubscribe with `-=`
  in `OnDisable()`.
- "Called by" tells you who normally calls this, so you know whether
  you'd ever call it yourself.
- A recurring pattern across almost every `PlayAudio`/`AudioClip` pair in
  this codebase: **`PlayAudio == false` never plays. `PlayAudio == true`
  with no clip assigned, or no `AudioSource` wired up, silently does
  nothing — neither case is ever treated as an error.** Noted once here
  rather than repeated at every occurrence below.

---

## Core (`Runtime/Scripts/Core/`)

### `Enums.cs`
| Enum | Values | Used by |
|---|---|---|
| `FlowMode` | `Sequential`, `Trigger` | **Unused anywhere.** Flow behavior is entirely determined by each node's `NodeType` instead. |
| `FlowNodeType` | `Text`, `TextImage`, `Assessment`, `Trigger` | `FlowNodeData.NodeType` — decides which branch `TrainingFlowController.ExecuteNode()` takes. |
| `QuestionType` | `QuestionAnswer`, `YesNo`, `TrueFalse`, `SingleChoice`, `MultipleChoice` | `QuestionData.QuestionType` — decides validation in `QuestionController` and which controls `QuestionPanel` builds. |
| `TrainingTrigger` | `None`, `Start`, `Continue`, `InteractionCompleted`, `TaskCompleted`, `ConditionMet`, `AssessmentStarted`, `AssessmentCompleted` | `FlowNodeData.Trigger`, `TriggerFlowController`. `None` is a sentinel — never assign it to an actual Trigger-type node; `TrainingFlowConfig.Validate()` rejects that. |
| `NavigationMode` | `None`, `Next`, `BackNext` | `FlowNodeData.Navigation` — which buttons `TextPanel`/`TextImagePanel` show. `None` = no buttons, node auto-advances. |
| `AssessmentNavigationMode` | `Sequential`, `FreeNavigation` | `AssessmentConfig.NavigationMode`, read via `AssessmentController.IsFreeNavigation`. |

### `IFlowController.cs` / `IUIPanel.cs`
```csharp
public interface IFlowController { event Action OnFlowCompleted; void StartFlow(); void StopFlow(); }
public interface IUIPanel { void Show(); void Hide(); }
```
Implemented by `TrainingFlowController` and `UIPanelBase` respectively.

---

## Flow (`Runtime/Scripts/Flow/`)

### `Data/FlowNodeData.cs`
Plain `[Serializable]` class — an element in `TrainingFlowConfig.Nodes`.

| Field | Type | Purpose |
|---|---|---|
| `Id` | `string` | Unique per node. Referenced by other nodes' `NextNodeId`. |
| `NodeType` | `FlowNodeType` | Decides everything below. |
| `NextNodeId` | `string` | Empty/null = last node, flow completes. |
| `PlayAudio` / `AudioClip` | `bool` / `AudioClip` | Text/TextImage/Trigger nodes only — Assessment nodes' audio is per-question instead (see `QuestionData`). |
| `Trigger` | `TrainingTrigger` | Trigger nodes only. Must not be `None`. |
| `Navigation` | `NavigationMode` | Text/TextImage nodes only. |
| `AutoAdvanceDelaySeconds` | `float`, min 0, default `4` | Only used when `Navigation == None`. `0` = advance immediately; `>0` = show with no buttons, auto-advance after this many seconds. |
| `TextContent` | `string`, multi-line | Text/TextImage nodes. |
| `Image` | `Sprite` | TextImage nodes. |
| `AssessmentReference` | `AssessmentConfig` | Assessment nodes only. Must be assigned. |

### `Data/TrainingFlowConfig.cs`
**ScriptableObject.** `Create > Training > Flow > Training Flow Config`

| Field | Type | Purpose |
|---|---|---|
| `Title` | `string` | Shown on the training-selection menu. |
| `ShowCompletionScreen` | `bool`, default `true` | Off = nothing shown when this training finishes (score still computed and delivered via the session's `OnSessionCompleted`). |
| `CalculateScore` | `bool`, default `true` | On = full completion layout (title/score/detail/badges). Ignored while `ShowFinalQuizPanel` is on. |
| `ShowFinalQuizPanel` | `bool`, default `false` | On = the simpler Final Quiz layout instead (image + Retry/Home/Quiz, no score text). Takes priority over `CalculateScore`. |
| `CompletionImage` | `Sprite` | Only used by the Final Quiz layout. |
| `StartNodeId` | `string` | First node entered. |
| `Nodes` | `List<FlowNodeData>` | Order in the list doesn't matter — only `NextNodeId` chains matter. |

| Property/Method | Signature | Purpose |
|---|---|---|
| `DisplayTitle` | `string` (property) | `Title` if set, else falls back to the asset's file name (`name`). Used by `TrainingMenuPanel`. |
| `GetNodeById` | `FlowNodeData GetNodeById(string id)` | Linear search. `null` if not found. |
| `Validate` | `bool Validate(out string error)` | Checks: start node exists; every set `NextNodeId` resolves; every Trigger node has a non-`None` trigger; every Assessment node has an `AssessmentReference`. Called automatically by `TrainingFlowController.StartFlow()`. |

### `Data/FlowRuntimeData.cs`
**Unused.** `TrainingSessionController` tracks its own history with a plain `List<string>` instead.

### `State/FlowState.cs`
```csharp
public enum FlowState { NotStarted, ShowingContent, WaitingForTrigger, RunningAssessment, ShowingResults, Completed }
```
Read via `TrainingFlowController.CurrentState` / pushed via `OnStateChanged`.

### `Controllers/TrainingFlowController.cs`
**The engine.** Resolves and advances nodes. Doesn't run individual questions or calculate score.

**Inspector fields**

| Field | Type | Purpose |
|---|---|---|
| `_triggerFlowController` | `TriggerFlowController` | Required if any node is Trigger-type. |
| `_assessmentController` | `AssessmentController` | Required if any node is Assessment-type. |
| `_scoreController` | `TrainingScoreController` | Where finished assessment results get recorded. |
| `_audioSource` | `AudioSource` | Optional. Plays Text/TextImage/Trigger node audio (Assessment nodes are skipped here — see `PlayNodeAudio` below). |
| `_verboseLogging` | `bool`, default `true` | Logs every node transition, state change, and trigger wait. |

**Events**

| Event | Signature | Fires when |
|---|---|---|
| `OnFlowCompleted` | `Action` | Flow reaches a node with no `NextNodeId`. |
| `OnNodeEntered` | `Action<FlowNodeData>` | **Every** node type, before that node's own type-specific event. Use for "clear the screen no matter what's coming" logic. |
| `OnShowContentNode` | `Action<FlowNodeData>` | Text/TextImage nodes only. |
| `OnWaitingForTrigger` | `Action<TrainingTrigger>` | A Trigger node started waiting. |
| `OnAutoAdvanceStarted` | `Action<float>` | A `Navigation.None` node with `AutoAdvanceDelaySeconds > 0` started its countdown. Payload is the delay — use it for a countdown UI, or ignore it (auto-advance works either way). |
| `OnShowAssessmentResult` | `Action<AssessmentSectionResultData>` | An assessment finished AND its `AssessmentConfig.ShowResultsScreen` is true. Flow pauses here until `ReportResultsContinue()`/`ReportResultsRetry()`. Never fires if `ShowResultsScreen` is false. |
| `OnBackRequested` | `Action` | `ReportBack()` was called. Resolution happens in `TrainingSessionController`, not here. |
| `OnStateChanged` | `Action<FlowState>` | `CurrentState` actually changed (not every frame). |

**Properties:** `CurrentNodeId` (`string`), `CurrentState` (`FlowState`).

**Public methods**

| Method | Signature | Called by | Purpose |
|---|---|---|---|
| `Initialize` | `void Initialize(TrainingFlowConfig config)` | `TrainingSessionController` | Assigns config, resets state to `NotStarted`. |
| `StartFlow` | `void StartFlow()` | `TrainingSessionController` | Validates, moves to `StartNodeId`. |
| `StopFlow` | `void StopFlow()` | You, for a hard-abort | Cancels any pending auto-advance, clears current node and pending results, resets state. |
| `ReportNext` | `void ReportNext()` | `TextPanel`/`TextImagePanel`'s Next button | Moves to `NextNodeId`. |
| `ReportBack` | `void ReportBack()` | Their Back button | Fires `OnBackRequested`. |
| `JumpToNode` | `void JumpToNode(string nodeId)` | `TrainingSessionController` (Back navigation) | Moves directly to a node, bypassing `NextNodeId`. |
| `ReportResultsContinue` | `void ReportResultsContinue()` | `AssessmentResultPanel`'s Continue button | Moves to the paused node's `NextNodeId`. No-op if nothing's waiting. |
| `ReportResultsRetry` | `void ReportResultsRetry()` | `AssessmentResultPanel`'s Retry button | Re-enters the SAME assessment node from question 1 — a fresh `StartAssessment()` call. No-op if nothing's waiting. |

**Key internal behavior:**
- `MoveToNode(string nodeId)` is the single choke point every navigation method funnels through: cancels any pending auto-advance, fires `OnNodeEntered`, plays node audio, then `ExecuteNode`.
- `BeginAutoAdvance` / `AutoAdvanceRoutine` / `CancelAutoAdvance`: a `Navigation.None` node with `AutoAdvanceDelaySeconds > 0` runs a coroutine (`_autoAdvanceCoroutine`) that's always cancelled the moment the flow moves anywhere else — whether that move came from the timer itself, a trigger, or a jump.
- `PlayNodeAudio` explicitly skips Assessment nodes (`if (node.NodeType == FlowNodeType.Assessment) return;`) — each question inside plays its own clip via `AssessmentController.PlayQuestionAudio` instead.
- `OnAssessmentCompleted` branches on `node.AssessmentReference.ShowResultsScreen`: true → sets `_pendingResultsNode`, state `ShowingResults`, fires `OnShowAssessmentResult`, and *stops* (doesn't call `MoveToNode`) until `ReportResultsContinue`/`ReportResultsRetry`. False → moves on immediately, exactly as if the results-screen feature didn't exist.


### `Controllers/TrainingSessionController.cs`
Owns the session lifecycle across a **list** of available trainings — not a single fixed config.

**Inspector fields**

| Field | Type | Purpose |
|---|---|---|
| `_availableFlowConfigs` | `List<TrainingFlowConfig>` | Every training this session can run. Index 0 auto-starts. |
| `_flowController` | `TrainingFlowController` | |
| `_scoreController` | `TrainingScoreController` | |

**Events:** `OnSessionCompleted` — `static Action<TrainingScoreData>`. Fires when the flow completes. Static, so it's reachable without a scene reference — remember to unsubscribe (e.g. `OnDestroy`) since static events live for the app's lifetime.

**Properties**

| Property | Type | Purpose |
|---|---|---|
| `AvailableFlowConfigs` | `IReadOnlyList<TrainingFlowConfig>` | Read-only view of the list, for a menu to enumerate. |
| `CurrentFlowConfig` | `TrainingFlowConfig` | Whichever training is currently loaded (or was last run). |
| `HasMultipleFlowConfigs` | `bool` | `_availableFlowConfigs.Count > 1` — UI uses this to decide whether a picker/"Choose Another" ever makes sense to show. |

**Public methods**

| Method | Signature | Called by | Purpose |
|---|---|---|---|
| `StartSession` | `void StartSession()` | `FlowRunner.Start()` | Starts (or re-runs) whatever `CurrentFlowConfig` currently is. **First call ever**, with nothing set: defaults to `_availableFlowConfigs[0]`. |
| `StartSession` | `void StartSession(TrainingFlowConfig config)` | A selection menu, or any script holding a config reference | Switches `CurrentFlowConfig` to `config`, then runs it. |
| `StartSession` | `void StartSession(int index)` | UnityEvents/Inspector calls that can't serialize an asset reference, or your own code keyed by index | Resolves `_availableFlowConfigs[index]` and calls the reference overload. Logs an error if out of range. |
| `RestartSession` | `void RestartSession()` | `TrainingCompletePanel`'s Restart button | Thin wrapper over `StartSession()` — reruns the CURRENT config. To run a *different* one, use one of the two overloads above instead. |

**Key internal behavior:** every actual reset (`_scoreController.Reset()`, clearing `_nodeHistory`, `_flowController.Initialize` + `StartFlow`) funnels through a single private `RunCurrentConfig()`, called by all three `StartSession` variants — so switching trainings, restarting the same one, and the very first auto-start all get an identical clean-slate guarantee.

### `Controllers/TrainingTriggerSource.cs`
The piece that actually **raises** a trigger — nothing else does this on its own.

**Inspector fields**

| Field | Type | Purpose |
|---|---|---|
| `_triggerFlowController` | `TriggerFlowController` | Required for `Fire()` to do anything. |
| `_trigger` | `TrainingTrigger`, default `TaskCompleted` | Which trigger this source raises. |
| `_fireOnStart` / `_startDelaySeconds` | `bool` / `float` | Auto-fire N seconds after scene start. |
| `_fireOnTriggerEnter` / `_requiredTag` | `bool` / `string` | Auto-fire when a (optionally tagged) collider enters this object's trigger volume. |
| `_fireOnlyOnce` | `bool`, default `true` | Subsequent `Fire()` calls after the first do nothing. |

**Public methods**

| Method | Signature | Called by | Purpose |
|---|---|---|---|
| `Fire` | `void Fire()` | A Button's `OnClick`, an XR Interactable's select/activate event, an Animation Event | Raises `_trigger`. Respects `_fireOnlyOnce`. |
| `ResetSource` | `void ResetSource()` | You, on a retry/restart | Clears the "already fired" flag. |

### `Components/FlowRunner.cs`
| Field | Type | Purpose |
|---|---|---|
| `_sessionController` | `TrainingSessionController` | |
| `_startOnAwake` | `bool`, default `true` | Calls `Run()` automatically in `Start()`. |

| Method | Purpose |
|---|---|
| `Run()` | Calls `_sessionController.StartSession()`. Call manually instead of `_startOnAwake` if you need to wait for something first (e.g. an XR rig). |

---

## Trigger (`Runtime/Scripts/Trigger/`)

### `Controllers/TriggerFlowController.cs`
Waits for a `TrainingTrigger`, resolves a callback when it arrives. Knows nothing about UI.

**Inspector fields**

| Field | Type | Purpose |
|---|---|---|
| `_verboseLogging` | `bool`, default `true` | |
| `_enableDebugKey` | `bool`, default `true` | |
| `_debugKey` | `KeyCode`, default `T` | Editor-only (`Application.isEditor` gated) — fires whatever's currently being waited on. |

**Events:** `OnTriggerReceived` (`Action<TrainingTrigger>`, any raise, matched or not), `OnWaitStarted` (`Action<TrainingTrigger>`, a Trigger node just started waiting).

**Properties:** `ExpectedTrigger` (`TrainingTrigger`), `IsWaiting` (`bool`).

**Public methods**

| Method | Signature | Called by | Purpose |
|---|---|---|---|
| `WaitForTrigger` | `void WaitForTrigger(TrainingTrigger expectedTrigger, Action onMatched)` | `TrainingFlowController.ExecuteTriggerNode` | Starts waiting. If the trigger already arrived a frame early (`_pendingTrigger`), resolves immediately instead of waiting forever. |
| `CancelWait` | `void CancelWait()` | You, to abort a wait early | |
| `RaiseTrigger` | `void RaiseTrigger(TrainingTrigger trigger)` | `TrainingTriggerSource.Fire()`, or any script directly | Matches → resolves immediately. No match → buffered in `_pendingTrigger` in case the flow starts waiting for it moments later. |
| `RaiseTriggerByName` | `void RaiseTriggerByName(string triggerName)` | UnityEvents (can't serialize enum params) | Parses the string, logs an error if it doesn't match any `TrainingTrigger` name. This is the one string-based entry point in an otherwise enum-only system — a deliberate convenience trade-off. |
| `DebugRaiseExpected` | `void DebugRaiseExpected()` | The debug key, or a test script | Fires whatever `ExpectedTrigger` is. Warns if nothing's being waited on. |

### `Data/TrainingTriggerData.cs`
**Unused.** `RaiseTrigger` takes a raw `TrainingTrigger` directly, never this wrapper.

---

## Question (`Runtime/Scripts/Question/`)

### `Data/QuestionData.cs`
Plain `[Serializable]` class. **Only the fields relevant to the selected `QuestionType` matter** — others are ignored by `QuestionController.ValidateAnswer` even if filled in.

| Field | Used by |
|---|---|
| `Id`, `QuestionType`, `QuestionText`, `PlayAudio`, `AudioClip`, `Points` | All types |
| `Options`, `CorrectOptionIndices` | SingleChoice (exactly 1 index), MultipleChoice (0+ indices) |
| `CorrectAnswerText` | QuestionAnswer — matched case-insensitively, trimmed |
| `CorrectYesNoAnswer` | YesNo — `true` = index 0 ("Yes") is correct |
| `CorrectTrueFalseAnswer` | TrueFalse — `true` = index 0 ("True") is correct |

Per-question audio is independent of `AssessmentConfig`'s own results-screen setting — it plays once per question shown, driven by `AssessmentController.PlayQuestionAudio`.

### `Data/QuestionResultData.cs`
Plain runtime result: `QuestionId`, `QuestionType`, `IsCorrect`, `PointsEarned`, `PointsAvailable`, `AttemptCount`, `SubmittedAnswer`.

### `Controllers/QuestionController.cs`
Runs and validates exactly one question at a time.

**Inspector fields:** `_feedbackDelaySeconds` (`float`, default `0.2`) — how long to hold on the answered question (feedback color) before the completion callback fires. `0` disables the pause entirely.

**Events**

| Event | Signature | Fires when |
|---|---|---|
| `OnAnswerValidated` | `Action<QuestionResultData>` | Immediately on submit, before the delay. `QuestionPanel.ShowFeedback` uses this. |
| `OnFeedbackDelayElapsed` | `Action` | The delay finishes and `IsProcessingAnswer` goes back to `false`. `UIFlowController` uses this to re-enable Free Navigation's Next button. |

**Properties:** `IsProcessingAnswer` (`bool`) — `true` from submit until the delay finishes. `AssessmentController.GoToQuestion`/`FinishAssessment` check this and refuse to act while it's `true`.

**Public methods**

| Method | Signature | Called by | Purpose |
|---|---|---|---|
| `RunQuestion` | `void RunQuestion(QuestionData question, Action<QuestionResultData> onCompleted)` | `AssessmentController.ShowQuestion` | Sets up the question, resets attempt count and `IsProcessingAnswer`. |
| `ReceiveAnswer` | `void ReceiveAnswer(string rawAnswer)` | `QuestionPanel.OnAnswerSubmitted` | Validates, fires `OnAnswerValidated`, starts the feedback-delay coroutine. No-op if already processing a previous answer. |

**`rawAnswer` format, if calling `ReceiveAnswer` directly:**

| QuestionType | Format |
|---|---|
| QuestionAnswer | Free text |
| YesNo | `"0"` = Yes, `"1"` = No |
| TrueFalse | `"0"` = True, `"1"` = False |
| SingleChoice | Selected index as a string, e.g. `"2"` |
| MultipleChoice | Comma-separated indices, e.g. `"0,2,3"` |


---

## Assessment (`Runtime/Scripts/Assessment/`)

### `Data/AssessmentConfig.cs`
**ScriptableObject.** `Create > Training > Assessment > Assessment Config`

| Field | Type | Purpose |
|---|---|---|
| `AssessmentId` | `string` | **Must be unique per assessment.** Used as the key in `TrainingScoreController`'s results dictionary — two configs sharing an Id will overwrite each other's recorded score. |
| `ShowResultsScreen` | `bool`, default `true` | On = `TrainingFlowController` pauses after this section and shows `AssessmentResultPanel`. Off = straight through to the next node, no pause. |
| `Questions` | `List<QuestionData>` | Freely mixed `QuestionType`s. |
| `NavigationMode` | `AssessmentNavigationMode`, default `Sequential` | Read via `AssessmentController.IsFreeNavigation`. |
| `PassThresholdPercent` | `float` 0–100, default `60` | Minimum `Score` for `Passed`. |

| Method | Purpose |
|---|---|
| `Validate(out string error)` | Checks: ≥1 question; every SingleChoice/MultipleChoice question has non-empty `Options`. **Does not** check that `CorrectOptionIndices` is non-empty/in-range or that `CorrectAnswerText` is non-blank — a documented gap, see README §8. |

### `Data/AssessmentSectionResultData.cs`
`AssessmentId`, `TotalQuestions`, `CorrectAnswers`, `IncorrectAnswers`, `AttemptCount`, `Score` (0–100, weighted by `Points`), `Passed`, `QuestionResults` (`List<QuestionResultData>`).

### `Data/TrainingScoreData.cs`
`TotalQuestions`, `CorrectAnswers`, `IncorrectAnswers`, `Score`, `Passed`, `SectionResults` (`List<AssessmentSectionResultData>`).

### `Controllers/AssessmentController.cs`
Runs one complete assessment section — Sequential auto-advance AND Free Navigation. Doesn't decide overall training progression.

**Inspector fields:** `_questionController` (`QuestionController`, required), `_audioSource` (`AudioSource`, optional — plays each question's clip).

**Events:** `OnShowQuestion` — `Action<QuestionData, int, int>` (question, index, total). Fires every time a question is shown, including Free Navigation jumps.

**Properties**

| Property | Type | Purpose |
|---|---|---|
| `IsFreeNavigation` | `bool` | `_config.NavigationMode == FreeNavigation` (false if no config loaded). |
| `QuestionCount` | `int` | `_config.Questions.Count`, or 0. |
| `CurrentIndex` | `int` | The question currently shown. |

**Public methods**

| Method | Signature | Called by | Purpose |
|---|---|---|---|
| `StartAssessment` | `void StartAssessment(AssessmentConfig config, Action<AssessmentSectionResultData> onCompleted)` | `TrainingFlowController.ExecuteAssessmentNode` | Validates, resets state, shows question 0. |
| `GoToQuestion` | `void GoToQuestion(int index)` | `GoToNext`/`GoToPrevious`, or a "jump to question N" UI | **FreeNavigation only** — warns and no-ops on Sequential. Also refuses (warns) while `_questionController.IsProcessingAnswer` is true — the exact source of *"GoToQuestion ignored — still showing feedback for the current answer."* Working as intended; `UIFlowController` graying out Next during that window is what prevents this from being hit in normal use. |
| `GoToNext` / `GoToPrevious` | `void GoToNext()` / `void GoToPrevious()` | `AssessmentPanel`'s Next/Previous buttons | Shortcuts for `GoToQuestion(CurrentIndex ± 1)`. |
| `FinishAssessment` | `void FinishAssessment()` | `AssessmentPanel`'s Finish button | **FreeNavigation only.** Completes using whatever's answered so far — unvisited questions score 0 but still count toward `TotalQuestions`. Same `IsProcessingAnswer` guard as `GoToQuestion`. |

**Key internal behavior:**
- `ShowQuestion(index)` captures `index` in a closure passed straight into `QuestionController.RunQuestion` — `_questionController.RunQuestion(question, result => OnQuestionAnswered(index, result))` — rather than reading the (mutable) `_currentIndex` field when the delayed callback eventually fires. Without this, navigating to a different question during the feedback delay would file the answer under the wrong index.
- `_results` is `Dictionary<int, QuestionResultData>` keyed by question index, not an append-only list — re-answering a previously-visited FreeNavigation question overwrites (last answer wins), doesn't duplicate.
- `CalculateSectionResult()` synthesizes a 0-point "unanswered" result for any index missing from `_results` (FreeNavigation-only scenario — Sequential always fills every index before it can complete).
- `PlayQuestionAudio` follows the same PlayAudio/AudioClip convention noted at the top of this document.

### `Controllers/TrainingScoreController.cs`
Accumulates finished sections into the final score.

**Inspector fields:** `_passThresholdPercent` (`float` 0–100, default `60`) — the OVERALL threshold, independent of each individual `AssessmentConfig.PassThresholdPercent`. A trainee can fail one section's own threshold but still pass overall, or vice versa, depending on how both are set.

**Events:** `OnScoreFinalized` — `Action<TrainingScoreData>`.

**Public methods**

| Method | Signature | Called by | Purpose |
|---|---|---|---|
| `RecordResult` | `void RecordResult(AssessmentSectionResultData result)` | `TrainingFlowController.OnAssessmentCompleted` | Stores keyed by `result.AssessmentId` — **overwrites** any existing entry for that Id. This is what makes Retry replace rather than stack. |
| `FinalizeScore` | `TrainingScoreData FinalizeScore()` | `TrainingSessionController` (`HandleFlowCompleted`) | Aggregates current dictionary values (so retried sections count once), fires `OnScoreFinalized`, returns the result. |
| `Reset` | `void Reset()` | `TrainingSessionController` (every `StartSession`) | Clears all recorded results. |

### `Controllers/AssessmentResultPanel.cs`
*(Namespace `Training.UI.Panels` despite the folder.)* Shown after one assessment section, only if `ShowResultsScreen` is true.

**Inspector fields:** `_titleText`, `_scoreText`, `_detailText` (`TextMeshProUGUI`), `_passBadge`/`_failBadge` (`GameObject`), `_continueButton`/`_retryButton` (`Button`), `_passColor`/`_failColor` (`Color`).

**Events:** `OnContinuePressed`, `OnRetryPressed` (both `Action`).

**Public methods:** `SetResult(AssessmentSectionResultData result)` — called by `UIFlowController.HandleShowAssessmentResult`, populates every field above.


---

## UI (`Runtime/Scripts/UI/`)

### `Panels/UIPanelBase.cs`
Abstract base. `virtual void Show()` → `SetActive(true)`. `virtual void Hide()` → `SetActive(false)`. Override for more (e.g. a fade).

### `Panels/TextPanel.cs` / `TextImagePanel.cs`
Nearly identical; TextImagePanel adds an `Image` field.

**Fields:** `_bodyText` (`TextMeshProUGUI`), `_nextButton`/`_backButton` (`Button`), `_image` (`Image`, TextImagePanel only).
**Events:** `OnNextPressed`, `OnBackPressed`.
**Method:** `SetContent(string text, [Sprite image,] NavigationMode navigation)` — sets content, shows/hides Next/Back per `navigation` (`None` = both hidden, `Next` = Next only, `BackNext` = both).

### `Panels/QuestionPanel.cs`
Renders any `QuestionType`. Reports the raw answer (format table above) and shows feedback afterward. No scoring logic of its own.

**Inspector fields**

| Field | Type | Purpose |
|---|---|---|
| `_questionText` | `TextMeshProUGUI` | |
| `_optionsContainer` | `Transform` | Parent spawned options go under. |
| `_choiceOptionPrefab` | `GameObject` | Needs a `Button` + `Image` (background). Looks for children named `"Index"` and `"Label"`; falls back to the first `TextMeshProUGUI`/`Text` found if absent. |
| `_freeTextInput` | `TMP_InputField` | QuestionAnswer only. |
| `_submitButton` | `Button` | QuestionAnswer and MultipleChoice — both need an explicit submit; everything else submits on tap. |
| `_showOptionIndex` | `bool`, default `true` | Shows "1. 2. 3." numbering. |
| `_indexFormat` | `string`, default `"{0}."` | `{0}` = 1-based index. |
| `_normalColor` / `_selectedColor` | `Color` | Option background before/while selected. |
| `_useFeedbackColors` | `bool`, default `true` | Master switch for post-answer coloring. |
| `_revealCorrectAnswer` | `bool`, default `true` | Also colors the correct option green on a wrong answer. |
| `_correctColor` / `_incorrectColor` | `Color` | |

**Events:** `OnAnswerSubmitted` — `Action<string>`, fires when an answer is ready to validate (immediate for single-select, on Submit click for QuestionAnswer/MultipleChoice).

**Public methods**

| Method | Called by | Purpose |
|---|---|---|
| `SetQuestion(QuestionData question)` | `UIFlowController.HandleShowQuestion` | Clears old options, builds new ones, resets colors/interactivity. |
| `ShowFeedback(QuestionResultData result)` | `UIFlowController.HandleAnswerValidated` | Disables further input, colors options/input by correctness. |

**Key internal behavior:**
- Selection is tracked in a `HashSet<int>` (`_selectedIndices`), not `Toggle.isOn` — there's no `Toggle` component in this version at all. `StripLegacyToggle()` deactivates any leftover `Toggle` on an *old* prefab automatically at spawn time, so previously-generated prefabs stop showing a stray green checkmark without needing to be regenerated.
- Every spawned option's `Button.transition` is forced to `Selectable.Transition.None` — this (plus removing the `Toggle`) is what eliminates the default green hover/press tint that Unity's `ColorTint` transition produces.
- `ClearOptions()` detaches children from `_optionsContainer` *before* `Destroy()` — `Destroy()` is deferred to end-of-frame, so without detaching first, `_optionsContainer.childCount` would still include the about-to-be-destroyed objects for the rest of that frame.
- `IsCorrectOptionIndex(int index)` branches on `QuestionType`: YesNo/TrueFalse check the bool fields (`CorrectYesNoAnswer`/`CorrectTrueFalseAnswer`); everything else checks `CorrectOptionIndices`. Checking `CorrectOptionIndices` directly for YesNo/TrueFalse (it's always empty for those types) is the bug this method exists to avoid repeating.

### `Panels/AssessmentPanel.cs`
Hosts a `QuestionPanel`, shows progress, and (Free Navigation only) Previous/Next/Finish.

**Fields:** `_progressText` (`TextMeshProUGUI`), `_questionPanel` (`QuestionPanel`), `_freeNavigationControls` (`GameObject`, groups the 3 buttons — leave everything in this group unassigned if you only use Sequential assessments), `_previousQuestionButton`/`_nextQuestionButton`/`_finishAssessmentButton` (`Button`).

**Property:** `QuestionPanel` (read-only accessor).

**Events:** `OnPreviousQuestionPressed`, `OnNextQuestionPressed`, `OnFinishAssessmentPressed`.

**Public methods**

| Method | Called by | Purpose |
|---|---|---|
| `SetProgress(int currentIndex, int totalQuestions)` | `UIFlowController.HandleShowQuestion` | Updates progress text. |
| `SetFreeNavigationControlsVisible(bool visible)` | Once, when the panel is first created | Shows/hides the whole button group, based on `AssessmentController.IsFreeNavigation`. |
| `SetNextButtonVisible(bool visible)` | Every question shown | **Hides** (`SetActive(false)`) Next on the last question — Finish is the way forward there instead. |
| `SetNextButtonInteractable(bool interactable)` | Around each answer's feedback window | Briefly **grays out** (stays visible) Next during the feedback delay — a click during it shouldn't silently fail. Previous/Finish are never touched by this. |

Note the deliberate distinction: last-question = hidden, feedback-window = grayed. Different mechanisms for different reasons — see `UIFlowController`'s `HandleShowQuestion`/`HandleAnswerValidated`/`HandleFeedbackDelayElapsed` below for exactly when each fires.

### `Panels/TrainingCompletePanel.cs`
Shown once the **whole session** finishes — distinct from `AssessmentResultPanel` (one section). Two layouts, driven entirely by the `TrainingFlowConfig` that just finished:

- **Full Detail** (`CalculateScore` on, `ShowFinalQuizPanel` off): title, score %, detail text, pass/fail badge, Retry + optional Choose Another.
- **Final Quiz Panel** (`ShowFinalQuizPanel` on — takes priority over `CalculateScore`): completion image + Retry/Home/Quiz only, no score text at all. Choose Another is suppressed entirely in this layout regardless of `HasMultipleFlowConfigs`.

**Inspector fields**

| Field | Type | Layout |
|---|---|---|
| `_titleText`, `_scoreText`, `_detailText` | `TextMeshProUGUI` | Full Detail only |
| `_passBadge`, `_failBadge` | `GameObject` | Full Detail only |
| `_restartButton` | `Button` | Both — labeled "Retry" in the UI, always visible whenever the panel shows |
| `_chooseAnotherButton` | `Button` | Full Detail only (optional — leave unassigned for a single-training app) |
| `_finalQuizControls` | `GameObject` | Groups Home + Quiz as a set |
| `_completionImage` | `Image` | Final Quiz Panel only — never shown in Full Detail regardless of whether a sprite is assigned |
| `_homeButton`, `_quizButton` | `Button` | Final Quiz Panel only |

**Events:** `OnRestartPressed`, `OnChooseAnotherPressed`, `OnHomePressed`, `OnQuizPressed`. **`OnHomePressed`/`OnQuizPressed` have no behavior wired to them anywhere in this package** — subscribe to them in your own project wherever Home/Quiz should actually navigate to.

**Public methods**

| Method | Signature | Called by | Purpose |
|---|---|---|---|
| `SetResult` | `void SetResult(TrainingScoreData data, bool calculateScore, bool showFinalQuizPanel, Sprite completionImage)` | `UIFlowController.HandleSessionCompleted` | Picks the layout (`showFinalQuizPanel` wins if both are true) and populates only the fields that layout uses. |
| `SetChooseAnotherVisible` | `void SetChooseAnotherVisible(bool visible)` | Same, right after (or before) `SetResult` | Shows/hides Choose Another — but forced hidden regardless of `visible` while in Final Quiz Panel mode, so call order doesn't matter (`SetResult` also re-applies the suppression itself if it runs after this). |

### `Panels/TrainingMenuPanel.cs`
Lists `TrainingSessionController.AvailableFlowConfigs` as buttons; reports the pick. Purely presentational.

**Fields:** `_titleText` (`TextMeshProUGUI`), `_listContainer` (`Transform`), `_menuItemPrefab` (`GameObject` — needs a `Button` + a `TextMeshProUGUI` label).

**Events:** `OnConfigSelected` — `Action<TrainingFlowConfig>`.

**Method:** `PopulateMenu(IReadOnlyList<TrainingFlowConfig> configs)` — clears old items, spawns one button per config labeled with `config.DisplayTitle`.

### `Components/UIPanelFactory.cs`
Instantiates panels from Inspector-assigned prefabs under `_panelParent`.

| Field | Required? |
|---|---|
| `_textPanelPrefab`, `_textImagePanelPrefab`, `_assessmentPanelPrefab` | Yes |
| `_assessmentResultPanelPrefab` | Only if any assessment has `ShowResultsScreen` on |
| `_trainingCompletePanelPrefab` | Recommended — without it, Restart/Choose Another are unreachable |
| `_trainingMenuPanelPrefab` | Only if more than one training is available |

`GetX()` methods instantiate and return the matching prefab; the three optional ones (`GetAssessmentResultPanel`, `GetTrainingCompletePanel`, `GetTrainingMenuPanel`) log an error and return `null` if their prefab isn't assigned — callers must null-check. `GetPanelForNode(FlowNodeType)` dispatches by node type; returns `null` for `Trigger` (no panel of its own).

### `Data/UIData.cs`
`TextPanelData`, `TextImagePanelData`, `TrainingUISettings` (a `ScriptableObject` with `PanelFadeDuration`/`AutoAdvanceOnTrigger`) — **all unused.** `TextPanel`/`TextImagePanel.SetContent` take raw parameters directly, never these wrapper classes; nothing reads either `TrainingUISettings` field.

### `Controllers/UIFlowController.cs`
**The bridge.** Listens to everything, creates/destroys the right panel via `UIPanelFactory`. Never decides what happens next or calculates score.

**Inspector fields:** `_panelFactory`, `_flowController`, `_assessmentController`, `_questionController`, `_sessionController` (needed for Restart/Choose Another/Start-by-config).

**Event routing — who it listens to, and what happens:**

| Listens to | Handler | Effect |
|---|---|---|
| `TrainingFlowController.OnNodeEntered` | `HandleNodeEntered` | Tears down whichever panels don't belong on the entering node's type. A Trigger node tears down *everything* (content, assessment, result, complete, menu) since it has no UI of its own. |
| `TrainingFlowController.OnShowContentNode` | `HandleShowContentNode` | Destroys old content panels, shows the right one, wires Next/Back. |
| `TrainingFlowController.OnShowAssessmentResult` | `HandleShowAssessmentResult` | Destroys the assessment panel, shows the result panel, wires Continue/Retry. |
| `AssessmentController.OnShowQuestion` | `HandleShowQuestion` | Creates the assessment panel on first call; wires Free Navigation buttons if applicable; every call updates question/progress/Next-visibility. |
| `QuestionController.OnAnswerValidated` | `HandleAnswerValidated` | Calls `QuestionPanel.ShowFeedback`; grays (not hides) Next for the feedback window if Free Navigation. |
| `QuestionController.OnFeedbackDelayElapsed` | `HandleFeedbackDelayElapsed` | Re-enables Next's interactivity. |
| `TrainingSessionController.OnSessionCompleted` (static) | `HandleSessionCompleted` | Shows `TrainingCompletePanel` with the correct layout, unless the finished training has `ShowCompletionScreen` off — then does nothing (score was already delivered via the event itself). |
| `TrainingFlowController.OnFlowCompleted` | `HandleFlowCompleted` | Destroys content/assessment/result panels (leftover-cleanup only — the actual completion screen is driven by the session-level event above, not this one). |

**Handlers with no corresponding subscription (called from panel button events instead):**

| Handler | Triggered by | Effect |
|---|---|---|
| `HandleResultContinuePressed` | `AssessmentResultPanel.OnContinuePressed` | Destroys result panel, `_flowController.ReportResultsContinue()`. |
| `HandleResultRetryPressed` | `AssessmentResultPanel.OnRetryPressed` | Destroys result panel, `_flowController.ReportResultsRetry()`. |
| `HandleRestartPressed` | `TrainingCompletePanel.OnRestartPressed` | Destroys complete panel, `_sessionController.RestartSession()`. |
| `HandleChooseAnotherPressed` | `TrainingCompletePanel.OnChooseAnotherPressed` | Destroys complete panel, shows the menu panel populated from `AvailableFlowConfigs`. |
| `HandleConfigSelected` | `TrainingMenuPanel.OnConfigSelected` | Destroys menu panel, `_sessionController.StartSession(config)`. |

**Note what's deliberately absent:** no handler subscribes to `TrainingCompletePanel.OnHomePressed`/`OnQuizPressed` — those events fire into nothing here, by design (see `TrainingCompletePanel` above).

**Extending with a new node/panel type** — follow this pattern:
```csharp
// 1. Add the enum value to FlowNodeType.
// 2. Add whatever fields it needs to FlowNodeData.
// 3. Add a case to TrainingFlowController.ExecuteNode for it.
// 4. Add a prefab field + GetX() method to UIPanelFactory.
// 5. Add a case (or new handler) in UIFlowController that creates the
//    panel and wires its events back to the flow controller.
```

---

## Editor (`Editor/`)

All four custom Inspectors/drawers below have no public API to call — Unity invokes them automatically. Compiled Editor-only via the asmdef's `includePlatforms: ["Editor"]`.

### `AssessmentConfigEditor.cs`
`[CustomEditor(typeof(AssessmentConfig))]` — labeled Results/Questions sections with contextual help text (e.g. explains what `ShowResultsScreen` does right where you toggle it).

### `TrainingFlowConfigEditor.cs`
`[CustomEditor(typeof(TrainingFlowConfig))]` — the completion-screen fields are conditionally shown/grayed based on each other: `CalculateScore` is hidden entirely if `ShowCompletionScreen` is off, grayed (not hidden) while `ShowFinalQuizPanel` is on so it's clear it still holds a value even though it's not currently used, and `CompletionImage` only appears once `ShowFinalQuizPanel` is on. A help box at the bottom explains which of the three completion layouts the current combination produces.

### `FlowNodeDataDrawer.cs`
`[CustomPropertyDrawer(typeof(FlowNodeData))]` — collapses each node into a foldout (`Id — NodeType`), shows only the fields the selected `NodeType` uses (including the conditional `AutoAdvanceDelaySeconds` when `Navigation == None`, and conditional `AudioClip` when `PlayAudio` is ticked). Warns inline if a Trigger node's `Trigger` is still `None`.

### `QuestionDataDrawer.cs`
`[CustomPropertyDrawer(typeof(QuestionData))]` — same idea for `QuestionData`: foldout shows `Id — QuestionType — text preview`, expanded view shows only the answer field(s) that `QuestionType` actually uses, plus an inline hint on SingleChoice/MultipleChoice about how many `Correct Option Indices` to set.

### `ChoiceOptionPrefabCreator.cs`
**Tools → Training → Generate ChoiceOption Prefab.** Builds `Assets/Prefabs/UI/ChoiceOption.prefab` from scratch: a `Button` + `Image` root (no `Toggle`, `Button.transition = None` — this combination is what prevents any hover/press tint), an `"Index"` child and a `"Label"` child (both `TextMeshProUGUI`), matching exactly what `QuestionPanel.SetOptionLabel` looks for. Re-running it overwrites the prefab at the same path.

---

## Worked example: answering a SingleChoice question, end to end

1. Trainee taps an option → `QuestionPanel.SelectSingle(index)` → fires `OnAnswerSubmitted(index.ToString())`.
2. `UIFlowController` wired that to `QuestionController.ReceiveAnswer` → runs.
3. `QuestionController` sets `IsProcessingAnswer = true`, validates, builds a `QuestionResultData`, fires `OnAnswerValidated`, starts the feedback-delay coroutine.
4. `UIFlowController.HandleAnswerValidated` calls `QuestionPanel.ShowFeedback` (options recolor) and, if Free Navigation, grays out Next.
5. After `_feedbackDelaySeconds`: `IsProcessingAnswer = false`, fires `OnFeedbackDelayElapsed` (re-enables Next), then invokes the callback passed into `RunQuestion` — `AssessmentController.OnQuestionAnswered(answeredIndex, result)`.
6. `AssessmentController` records into `_results[answeredIndex]`. Sequential → immediately shows the next question (fires `OnShowQuestion` again). FreeNavigation → stays put; trainee moves on via Previous/Next/Finish.
7. Once the section is done, `CompleteAssessment()` computes `AssessmentSectionResultData` and invokes the callback `TrainingFlowController` passed into `StartAssessment` — `OnAssessmentCompleted(result, node)`.
8. `TrainingFlowController` records into `TrainingScoreController`, then either pauses for a results screen (`ShowResultsScreen` true) or moves straight to `node.NextNodeId`.
9. Once the *whole flow* eventually completes (no more `NextNodeId` anywhere down the chain), `TrainingSessionController.HandleFlowCompleted` calls `_scoreController.FinalizeScore()` and raises the static `OnSessionCompleted` — which is what `UIFlowController.HandleSessionCompleted` is listening for, to show `TrainingCompletePanel` in whichever layout that training's `TrainingFlowConfig` asks for.