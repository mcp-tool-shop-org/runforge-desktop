# RunForge Desktop v0.3.4 Release Notes

## Sweep v1 - Multi-Run Orchestration

This release introduces the sweep system for running parameter sweeps (grid search) with controlled concurrency.

### New CLI Command

```bash
runforge-cli sweep --plan <path-to-sweep_plan.json>
```

### Features

#### Grid Strategy
- Define parameter combinations to sweep over
- Cartesian product expansion (e.g., 2 params × 3 values = 6 runs)
- Support for null values (to remove/unset parameters)

#### Concurrent Execution
- Configurable max_parallel (1-N concurrent runs)
- Runs execute as independent CLI processes
- Progress tracking with RF tokens

#### Group Management
- Groups stored in `.runforge/groups/<group-id>/`
- Atomic group.json updates (consistent reads)
- Best run tracking by primary metric
- Plan copy preserved for reproducibility

#### Cancel Support
- Graceful cancellation via Ctrl+C / SIGTERM
- Remaining runs marked as canceled
- Completed runs preserved

### File Formats

#### Sweep Plan (`sweep_plan.json`)
```json
{
  "version": 1,
  "kind": "sweep_plan",
  "workspace": "C:\\workspace",
  "group": { "name": "My Sweep", "notes": "Optional" },
  "base_request": { /* v1 request */ },
  "strategy": {
    "type": "grid",
    "parameters": [
      { "path": "model.hyperparameters.n_estimators", "values": [50, 100, 200] }
    ]
  },
  "execution": { "max_parallel": 2, "fail_fast": false, "stop_on_cancel": true }
}
```

#### Run Group (`group.json`)
- Version 1 schema with status tracking
- Per-run status and metrics
- Summary with best run selection

### Desktop Integration

- `ISweepService` for creating and executing sweeps
- `SweepPlan` and `RunGroup` models in Core
- Group listing and loading support

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | All runs succeeded |
| 1 | At least one run failed |
| 5 | Sweep was canceled |
| 6 | Invalid sweep plan |

### Tests

#### Python (CLI)
- 16 new tests for sweep functionality
- Grid expansion, override application, group.json structure
- Total: 32 Python tests passing

#### C# (Desktop)
- 10 new tests for SweepPlan/RunGroup deserialization
- Total: 327 C# tests passing

### Documentation

- Added `docs/SWEEP_V1_CONTRACT.md` with full schema documentation

### Supported Override Paths

For v0.3.4:
- `model.family`
- `model.hyperparameters.<key>`

### Compatibility

- .NET 10 Preview
- Python 3.10+
- Windows 10/11

### Breaking Changes

None. Existing run/compare functionality unchanged.

### Next Steps

v0.3.5 will add:
- Desktop sweep builder UI
- Group view with run list
- Compare within group
