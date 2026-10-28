# Checklist & Progress

## Controllers

- `ChecklistController` — define items, `MarkComplete(id)`, `OnAllCompleted`
- `ProgressController` — min/max, `Add`/`Set`, `OnCompleted` (auto or manual)

## Bridge

- `ChecklistProgressFlowBridge` — single completion trigger
- `ChecklistThenProgressBridge` — checklist → `TaskCompleted`, then progress → `Continue`

## UI

- Prefabs (created by setup menu): `ChecklistPanel`, `ChecklistRow`, `ProgressPanel`
- Bind: `panel.Bind(controller)`

## Sample setup

**Training → Setup Checklist & Progress Demo**

Keys: **1 / 2 / 3** checklist, **+ / =** progress.
