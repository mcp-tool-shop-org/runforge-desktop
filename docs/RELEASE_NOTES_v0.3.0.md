# Release Notes v0.3.0

## Rerun as First-Class Workflow

This release makes RunForge Desktop a true training tool by enabling execution and rerun workflows.

### New Features

#### runforge-cli
A new Python CLI for executing ML training runs:
- Command: `runforge-cli run --run-dir <path>`
- Supports model families: `logistic_regression`, `random_forest`, `linear_svc`
- Streams RF tokens for Desktop timeline (`[RF:STAGE=X]`, `[RF:EPOCH=X/Y]`)
- Exit codes: 0=success, 1=failed, 2=invalid, 3=missing, 4=internal
- Writes `result.json` with metrics and artifact paths
- Atomic file writes for crash safety

Installation:
```bash
cd src/runforge-cli
pip install -e .
```

#### Execute Draft Runs
- "Execute" button on run detail page for draft runs
- Real-time log streaming via existing live monitoring
- Timeline and milestones update as training progresses
- Status updates: Starting → Running → Completed/Failed
- Cancel button to abort running execution

#### Rerun Workflow
- "Rerun" button on completed/failed runs
- Clones `request.json` into new run directory
- Sets `rerun_from` field to track lineage
- Automatically navigates to new run for editing/execution

### Technical Details

#### New Services
- `ICliExecutionService` / `CliExecutionService` - Subprocess management for runforge-cli
- `IRunCreationService` / `RunCreationService` - Run creation and cloning

#### CLI Contract
```
runforge-cli run --run-dir <path> [--workspace <path>]
```

Inputs:
- `request.json` (must exist in run-dir)

Outputs:
- `logs.txt` (streaming with RF tokens)
- `result.json` (atomic write at completion)
- `artifacts/` (model.pkl, metrics.json, etc.)

RF Tokens emitted:
```
[RF:STAGE=STARTING]
[RF:STAGE=LOADING_DATASET]
[RF:STAGE=TRAINING]
[RF:EPOCH=1/10]
[RF:STAGE=EVALUATING]
[RF:STAGE=WRITING_ARTIFACTS]
[RF:STAGE=COMPLETED]  // or [RF:STAGE=FAILED]
```

#### ViewModel Additions
- `RunDetailViewModel.ExecuteRunCommand` - Execute draft runs
- `RunDetailViewModel.RerunCommand` - Clone and navigate to rerun
- `RunDetailViewModel.CancelExecutionCommand` - Abort running execution
- `RunDetailViewModel.CanExecute` / `CanRerun` computed properties
- `RunDetailViewModel.IsExecuting` / `ExecutionStatus` state

### Tests
- 220 .NET tests passing
- 16 Python CLI tests passing

### Files Changed

#### CLI (new package)
- `src/runforge-cli/` - Python package
- `runforge_cli/cli.py` - Main entry point
- `runforge_cli/runner.py` - Training execution
- `runforge_cli/request.py` - Request parsing
- `runforge_cli/result.py` - Result writing
- `runforge_cli/tokens.py` - RF token generation

#### Desktop (new services)
- `ICliExecutionService.cs` - CLI execution interface
- `CliExecutionService.cs` - Process management
- `IRunCreationService.cs` - Run creation interface
- `RunCreationService.cs` - Clone and create runs

#### Desktop (updated)
- `RunDetailViewModel.cs` - Execute/Rerun commands
- `MauiProgram.cs` - Service registration

## Upgrading

**Requirements:**
- Python 3.10+ with `pip install -e src/runforge-cli`
- scikit-learn, pandas, joblib (installed automatically)

**Breaking Changes:** None. Existing runs continue to work. New features are additive.

## Roadmap

- v0.3.1: Request editor UI (edit hyperparameters before execution)
- v0.3.2: Side-by-side comparison view (original vs rerun)
- v0.4.0: GPU support with RTX 5080 optimizations

## Full Changelog

- 07a207e feat(desktop): add Execute and Rerun commands via CLI
- c647109 feat(cli): scaffold runforge-cli with run --run-dir command
