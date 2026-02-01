# runforge-cli

Command-line tool for executing RunForge ML training runs.

## Installation

```bash
cd src/runforge-cli
pip install -e .
```

For development:
```bash
pip install -e ".[dev]"
```

## Usage

```bash
runforge-cli run --run-dir <path-to-run-directory>
```

The run directory must contain a valid `request.json` file.

### Example

```bash
runforge-cli run --run-dir "C:/workspace/.ml/runs/20260201-120000-my-run-a1b2"
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Training failed (runtime error) |
| 2 | Invalid request.json (schema/validation) |
| 3 | Missing files (request.json, dataset, etc.) |
| 4 | Internal error (CLI bug) |

## Output

The CLI writes to:
- `logs.txt` - Streaming log output with RF tokens
- `result.json` - Final result with metrics and artifacts
- `artifacts/` - Model files and other outputs

### RF Tokens

The CLI emits stage tokens for Desktop timeline display:

```
[RF:STAGE=STARTING]
[RF:STAGE=LOADING_DATASET]
[RF:STAGE=TRAINING]
[RF:EPOCH=1/10]
[RF:EPOCH=2/10]
...
[RF:STAGE=EVALUATING]
[RF:STAGE=WRITING_ARTIFACTS]
[RF:STAGE=COMPLETED]
```

On failure:
```
[RF:STAGE=FAILED]
```

## Supported Models

- `logistic_regression` - Logistic Regression (scikit-learn)
- `random_forest` - Random Forest Classifier (scikit-learn)
- `linear_svc` - Linear Support Vector Classifier (scikit-learn)

## Request Schema

See `docs/V1_CONTRACT.md` for the full request.json schema.

Minimal example:
```json
{
  "version": 1,
  "preset": "balanced",
  "dataset": {
    "path": "data/iris.csv",
    "label_column": "species"
  },
  "model": {
    "family": "logistic_regression"
  },
  "device": {
    "type": "cpu"
  },
  "created_at": "2026-02-01T12:00:00Z",
  "created_by": "runforge-desktop@0.3.0"
}
```

## Result Schema

```json
{
  "version": 1,
  "status": "succeeded",
  "started_at": "2026-02-01T12:00:00Z",
  "finished_at": "2026-02-01T12:00:05Z",
  "duration_ms": 5000,
  "summary": {
    "primary_metric": {
      "name": "accuracy",
      "value": 0.95
    },
    "metrics": {
      "accuracy": 0.95,
      "precision": 0.94,
      "recall": 0.95,
      "f1_score": 0.94
    }
  },
  "artifacts": [
    {"path": "artifacts/model.pkl", "type": "model", "bytes": 12345}
  ]
}
```

On failure:
```json
{
  "version": 1,
  "status": "failed",
  "error": {
    "message": "Dataset not found: data/missing.csv",
    "type": "FileNotFoundError"
  }
}
```
