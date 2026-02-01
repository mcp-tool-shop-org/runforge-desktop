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

---

## Desktop Integration

### Python Requirements

- **Required version:** Python 3.10+
- **Dependencies:** scikit-learn, pandas, joblib (installed automatically via pip)

### How Desktop Discovers Python

Desktop uses a multi-step discovery process:

1. **User-specified path** - Settings override (future)
2. **py launcher** - Windows Python Launcher (`py --version`)
3. **python in PATH** - Standard PATH lookup
4. **Common paths** - `C:\Python3XX\`, `%LOCALAPPDATA%\Programs\Python\`
5. **Windows Store** - `%LOCALAPPDATA%\Microsoft\WindowsApps\python.exe`

### How Desktop Invokes the CLI

```
<python> -m runforge_cli run --run-dir "<path>" --workspace "<path>"
```

Where `<python>` is the discovered Python executable.

### Running Tests

```bash
cd src/runforge-cli
pip install -e ".[dev]"
pytest tests/ -v
```

### MSIX Packaging (Future)

For MSIX distribution, options include:

1. **Embedded Python** - Bundle Python runtime in MSIX
2. **Require Python** - Document as prerequisite, discover at runtime
3. **PyInstaller** - Package CLI as standalone .exe

Current v0.3.0 uses option 2 (runtime discovery).
