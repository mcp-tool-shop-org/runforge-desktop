# Result JSON V1 Contract

This document defines the schema contract for `result.json` files produced by the RunForge CLI and consumed by RunForge Desktop. Both projects MUST maintain compatibility with this contract.

## Version

**Contract Version:** 1
**Status:** Stable
**Last Updated:** 2026-02-01

## File Location

```
.ml/runs/<run-id>/result.json
```

Written by CLI when a run completes (success or failure).

## Schema

```json
{
  "version": 1,
  "status": "succeeded",
  "started_at": "2026-02-01T12:00:00Z",
  "finished_at": "2026-02-01T12:05:00Z",
  "duration_ms": 300000,
  "summary": {
    "primary_metric": {
      "name": "accuracy",
      "value": 0.85
    },
    "metrics": {
      "accuracy": 0.85,
      "f1_score": 0.83,
      "precision": 0.86,
      "recall": 0.84
    }
  },
  "effective_config": {
    "preset": "balanced",
    "model": {
      "family": "logistic_regression",
      "hyperparameters": {
        "C": 1.0,
        "max_iter": 1000
      }
    },
    "device": {
      "type": "cpu"
    },
    "dataset": {
      "path": "data/train.csv",
      "label_column": "target"
    }
  },
  "artifacts": [
    {
      "path": "model.pkl",
      "type": "model",
      "bytes": 102400
    },
    {
      "path": "metrics.json",
      "type": "metrics",
      "bytes": 256
    }
  ],
  "error": null
}
```

## Field Definitions

### Required Fields

| Field | Type | Description |
|-------|------|-------------|
| `version` | int | Schema version, MUST be `1` |
| `status` | string | One of: `"succeeded"`, `"failed"`, `"cancelled"` |
| `duration_ms` | int | Total execution time in milliseconds |

### Optional Fields (Recommended)

| Field | Type | Description |
|-------|------|-------------|
| `started_at` | string | ISO 8601 timestamp when run started |
| `finished_at` | string | ISO 8601 timestamp when run completed |
| `summary` | object | Metrics summary (see below) |
| `effective_config` | object | Merged runtime configuration |
| `artifacts` | array | List of produced artifacts |
| `error` | object | Error details if `status == "failed"` |

### Summary Object

```json
{
  "primary_metric": {
    "name": "accuracy",
    "value": 0.85
  },
  "metrics": {
    "accuracy": 0.85,
    "f1_score": 0.83
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `primary_metric` | object | Optional. Contains `name` (string) and `value` (number) |
| `metrics` | object | Dictionary of metric name → numeric value |

### Primary Metric Selection Policy

When `primary_metric` is not specified, consumers SHOULD select in order:
1. `accuracy` if present
2. `f1_score` if present
3. `loss` if present (note: lower is better)
4. First metric alphabetically
5. No primary metric

### Effective Config Object

The `effective_config` captures the actual configuration used at runtime, including:
- Preset defaults merged with user overrides
- Device selection results (CPU/GPU)
- Any runtime-computed values

Structure mirrors `request.json` but may include additional merged fields:

```json
{
  "preset": "balanced",
  "model": {
    "family": "logistic_regression",
    "hyperparameters": {}
  },
  "device": {
    "type": "cpu",
    "gpu_id": null
  },
  "dataset": {
    "path": "data/train.csv",
    "label_column": "target"
  }
}
```

### Artifacts Array

Each artifact object:

| Field | Type | Description |
|-------|------|-------------|
| `path` | string | Relative path from run directory |
| `type` | string | Artifact type (see below) |
| `bytes` | int | File size in bytes |

**Artifact Types:**
- `model` - Trained model file (e.g., `.pkl`, `.pt`)
- `metrics` - Metrics JSON
- `feature_importance` - Feature importance data
- `linear_coefficients` - Linear model coefficients
- `encoder` - Label/feature encoders
- `log` - Log files
- `checkpoint` - Training checkpoint
- `other` - Unclassified artifacts

### Error Object

When `status == "failed"`:

```json
{
  "message": "Division by zero in feature preprocessing",
  "type": "ZeroDivisionError",
  "traceback": "..."
}
```

| Field | Type | Description |
|-------|------|-------------|
| `message` | string | Human-readable error message |
| `type` | string | Exception/error type name |
| `traceback` | string | Optional. Full stack trace |

## Compatibility Rules

### Forward Compatibility (CLI → Desktop)

**Desktop MUST:**
1. Parse and use all V1 required fields
2. Ignore unknown fields gracefully (no errors)
3. Handle missing optional fields with sensible defaults
4. Preserve unknown fields if ever writing result.json

**Desktop SHOULD:**
1. Display warnings for version mismatches
2. Degrade gracefully when optional sections missing

### Backward Compatibility (Desktop → CLI)

**CLI MUST:**
1. Write all required fields
2. Include `version: 1` exactly

**CLI SHOULD:**
1. Include `summary` with at least one metric
2. Include `effective_config` for comparison features
3. Include `artifacts` list for artifact management

## Status Values

| Status | Description | Has Error? |
|--------|-------------|------------|
| `succeeded` | Run completed successfully | No |
| `failed` | Run failed with error | Yes |
| `cancelled` | Run was cancelled by user | Optional |

## Duration Formatting

`duration_ms` is always in milliseconds. Consumers format for display:
- < 1000ms: `"123ms"`
- < 60000ms: `"5.0s"`
- < 3600000ms: `"2.5m"`
- >= 3600000ms: `"1.5h"`

## Test Vectors

See `docs/test-vectors/` for canonical examples:
- `result.v1.succeeded.json` - Successful run with all fields
- `result.v1.failed.json` - Failed run with error
- `result.v1.minimal.json` - Minimum required fields only

## Versioning

- Version 1 is stable and MUST NOT have breaking changes
- New optional fields may be added without version bump
- Breaking changes require version 2 and deprecation period
- Desktop should warn (not error) on `version > 1`

## Changelog

### v1.0 (2026-02-01)
- Initial stable contract
- Required: version, status, duration_ms
- Optional: summary, effective_config, artifacts, error
