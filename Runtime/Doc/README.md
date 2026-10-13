# Training Framework

Flow + Sequence + Integration in one package.

## Quick start (recommended)

**Training → Setup Multi-Flow Connected Scene**

Creates one scene with:

| Flow | Path | Activity signal |
|------|------|-----------------|
| **PPE Hands-On Training** | Text → Sequence → Quiz → End | `TaskCompleted` (click cubes) |
| **Zone Walk-Through Training** | Text → Zone → Text → Quiz → End | `Continue` (enter orange zone) |

### Play Mode

1. **Menu** appears → pick a training  
2. Complete activity (sequence or zone)  
3. Quiz → results → completion  
4. **Retry** same training or **Choose Another**

### Important: Flow vs Trigger

| Concept | What it is | Example |
|---------|------------|---------|
| **TrainingFlowConfig** | Whole training asset (title, nodes) | "PPE Hands-On Training" |
| **TrainingTrigger** | Signal that unlocks a Trigger *node* | `TaskCompleted`, `Continue` |

Never raise the flow **title**. Raise the **trigger enum** the node waits for.

```csharp
TriggerFlowController.Raise(TrainingTrigger.TaskCompleted); // OK
TriggerFlowController.RaiseByName("TaskCompleted");         // OK
// TriggerFlowController.RaiseByName("PPE Hands-On Training"); // WRONG
```

### Multiple configs

`TrainingSessionController.Available Flow Configs` holds every training in the scene.

- Index 0 = default if auto-start  
- Menu / Choose Another uses the full list  
- `StartSession(config)` or `StartSession(index)` selects one  

### Other menus

| Menu | Purpose |
|------|---------|
| Setup Multi-Flow Connected Scene | Full multi-config demo (use this) |
| Setup Full Combined Demo | Single combined flow |
| Setup Full Demo Scene | Flow-only |
