# Sweep v1 Contract

This document defines the file formats for sweep plans and run groups in RunForge.

## Overview

A **sweep** is a set of training runs generated from a single base request with parameter variations. The sweep system consists of:

1. **Sweep Plan** (`sweep_plan.json`) - Input recipe that defines what to run
2. **Run Group** (`group.json`) - Output record of what happened

## Sweep Plan Schema (v1)

Location: User-specified (typically workspace root or `.runforge/plans/`)

```json
{
  "version": 1,
  "kind": "sweep_plan",
  "created_at": "2026-02-01T15:00:00Z",
  "created_by": "runforge-desktop@0.3.4",

  "workspace": "C:\\path\\to\\workspace",
  "group": {
    "name": "rf depth x estimators",
    "notes": "CPU-only sweep, baseline comparison"
  },

  "base_request": {
    "$schema": "https://runforge.dev/schemas/request.v1.json",
    "version": 1,
    "preset": "balanced",
    "dataset": { "path": "data/train.csv", "label_column": "target" },
    "model": { "family": "random_forest", "hyperparameters": { "random_state": 42 } },
    "device": { "type": "cpu", "gpu_reason": null },
    "created_at": "2026-02-01T14:59:00Z",
    "created_by": "runforge-desktop@0.3.4"
  },

  "strategy": {
    "type": "grid",
    "parameters": [
      { "path": "model.hyperparameters.n_estimators", "values": [50, 100, 200] },
      { "path": "model.hyperparameters.max_depth", "values": [null, 10, 30] }
    ]
  },

  "execution": {
    "max_parallel": 2,
    "fail_fast": false,
    "stop_on_cancel": true
  }
}
```

### Required Fields

| Field | Type | Description |
|-------|------|-------------|
| `version` | int | Must be `1` |
| `kind` | string | Must be `"sweep_plan"` |
| `created_at` | string | ISO8601 timestamp |
| `created_by` | string | Tool identifier |
| `workspace` | string | Absolute workspace path |
| `group.name` | string | Human-readable sweep name |
| `base_request` | object | Complete v1 request schema |
| `strategy.type` | string | `"grid"` or `"list"` |
| `execution.max_parallel` | int | >= 1 |

### Optional Fields

| Field | Type | Description |
|-------|------|-------------|
| `group.notes` | string | Description/notes |
| `strategy.parameters` | array | Parameters to vary |
| `execution.fail_fast` | bool | Stop on first failure |
| `execution.stop_on_cancel` | bool | Stop remaining on cancel |

### Strategy Types

#### Grid Strategy

Generates a cartesian product of all parameter values:

```json
"strategy": {
  "type": "grid",
  "parameters": [
    { "path": "model.hyperparameters.n_estimators", "values": [50, 100] },
    { "path": "model.hyperparameters.max_depth", "values": [10, 20] }
  ]
}
```

This generates 4 runs (2 × 2).

#### Parameter Paths

Paths use dot notation to target nested fields:

- `model.family` - Model family
- `model.hyperparameters.n_estimators` - Hyperparameter
- `dataset.path` - Dataset path

Setting a value to `null` removes the key from the request.

## Run Group Schema (v1)

Location: `.runforge/groups/<group-id>/group.json`

```json
{
  "version": 1,
  "kind": "run_group",
  "group_id": "grp_20260201_150000_rfdepth",
  "created_at": "2026-02-01T15:00:00Z",
  "created_by": "runforge-cli@0.3.4",
  "name": "rf depth x estimators",
  "notes": "CPU-only sweep, baseline comparison",

  "plan_ref": "plan.json",

  "status": "completed",
  "execution": {
    "max_parallel": 2,
    "started_at": "2026-02-01T15:00:10Z",
    "finished_at": "2026-02-01T15:45:30Z",
    "cancelled": false
  },

  "runs": [
    {
      "run_id": "20260201-150010-sweep-0000",
      "status": "succeeded",
      "request_overrides": {
        "model.hyperparameters.n_estimators": 50,
        "model.hyperparameters.max_depth": null
      },
      "result_ref": ".ml/runs/20260201-150010-sweep-0000/result.json",
      "primary_metric": { "name": "accuracy", "value": 0.9112 }
    }
  ],

  "summary": {
    "total": 9,
    "succeeded": 9,
    "failed": 0,
    "canceled": 0,
    "best_run_id": "20260201-150025-sweep-0004",
    "best_primary_metric": { "name": "accuracy", "value": 0.9345 }
  }
}
```

### Status Values

| Status | Description |
|--------|-------------|
| `running` | Sweep is in progress |
| `completed` | All runs finished successfully |
| `failed` | At least one run failed |
| `canceled` | User canceled the sweep |

### Run Entry Status

| Status | Description |
|--------|-------------|
| `pending` | Not yet started |
| `running` | Currently executing |
| `succeeded` | Completed successfully |
| `failed` | Failed with error |
| `canceled` | Canceled by user |

## Group Folder Layout

```
.runforge/
  groups/
    <group-id>/
      group.json      # Main state file (updated atomically)
      plan.json       # Copy of the plan used
      group.log       # Optional orchestration log
```

## CLI Usage

```bash
runforge-cli sweep --plan <path-to-sweep_plan.json>
```

### Behavior

1. Validates plan
2. Creates group folder + initial `group.json` (status=running)
3. Expands grid into run configs
4. Creates run folders with `request.json` per run
5. Executes runs with max_parallel concurrency
6. Updates `group.json` atomically as runs complete
7. Writes final status on completion/cancel

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | All runs succeeded |
| 1 | At least one run failed |
| 5 | Sweep was canceled |
| 6 | Invalid sweep plan |

## Tokens

Group tokens for log parsing:

```
[RF:GROUP=START grp_20260201_150000 runs=9]
[RF:GROUP=RUN 20260201-150010-sweep-0000 1/9]
[RF:GROUP=RUN_DONE 20260201-150010-sweep-0000 status=succeeded]
[RF:GROUP=COMPLETE grp_20260201_150000 succeeded=9 failed=0 canceled=0]
[RF:GROUP=CANCELED grp_20260201_150000]
```

## Atomic Updates

`group.json` is always written atomically:

1. Write to temp file in same directory
2. Rename temp file to `group.json`

This ensures Desktop always reads a consistent state.

## Best Run Selection

The "best run" is determined by highest primary metric value. This assumes higher is better (accuracy, F1, etc.). Future versions may support configurable optimization direction.

## Supported Override Paths (v0.3.4)

For v0.3.4, the following paths are supported:

- `model.family`
- `model.hyperparameters.<key>` (n_estimators, max_depth, max_iter, etc.)

Future versions will add:

- `dataset.path`
- `device.type` (for GPU sweeps)
- Search spaces (log scale, ranges)
