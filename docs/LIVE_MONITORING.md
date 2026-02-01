# Live Run Monitoring

RunForge Desktop provides real-time visibility into running ML training jobs through file-based monitoring. This document describes the monitoring architecture, explicit tokens for guaranteed detection, and robustness features.

## Overview

Live monitoring is driven by actual file activity, not fake spinners. RunForge polls the `logs.txt` file to detect:
- New log lines (streaming log tail)
- Run milestones (Starting, Loading Dataset, Training, Evaluating, Writing Artifacts, Completed)
- Epoch progress (e.g., "Epoch 5/10")
- Stalled or stuck runs

## File-Based Monitoring

### Primary Files
- **logs.txt** - Live log output from the runner (stdout/stderr captured)
- **request.json** - Run configuration (read at start)
- **result.json** - Final run result (written on completion)
- **metrics.json** - Training metrics (optional)

### Polling Strategy
RunForge uses adaptive polling to balance responsiveness with CPU efficiency:

| Condition | Polling Interval |
|-----------|------------------|
| Active (new data within 60s) | 500ms (fast) |
| Stale (no data for 60s+) | 3s (slow) |
| Completed/Failed | No polling |

### Robustness Features
- **Truncation detection**: Detects when log file size decreases
- **Replacement detection**: Detects when log file is deleted and recreated
- **MaxBytesPerTick cap**: Limits to 64KB per poll to prevent UI hitching
- **Partial line handling**: Only returns complete lines to avoid garbled output

## Milestone Detection

### Heuristic Detection (Default)
RunForge scans log lines for patterns that indicate pipeline stages:

| Milestone | Example Patterns |
|-----------|------------------|
| Starting | "starting", "initializing", "run id" |
| Loading Dataset | "loading dataset", "loaded 150 samples" |
| Training | "epoch 1/10", "training started", "fit(" |
| Evaluating | "evaluating", "validation accuracy" |
| Writing Artifacts | "saving model", "wrote metrics.json" |
| Completed | "training complete", "finished" |
| Failed | "error", "exception", "failed" |

### Explicit Tokens (Recommended)
For guaranteed detection, runners can emit explicit RunForge tokens:

```
[RF:STAGE=STARTING]
[RF:STAGE=LOADING_DATASET]
[RF:STAGE=TRAINING]
[RF:STAGE=EVALUATING]
[RF:STAGE=WRITING_ARTIFACTS]
[RF:STAGE=COMPLETED]
[RF:STAGE=FAILED]
```

Explicit tokens always take priority over heuristic detection.

### Epoch Progress
Track epoch progress with either format:

**Heuristic (auto-detected):**
```
Epoch 5/10: loss=0.123
epoch 3 of 10
```

**Explicit token (recommended):**
```
[RF:EPOCH=5/10]
```

## Stuck Detection

RunForge warns users when a run appears stalled:

| Warning Threshold | Condition |
|-------------------|-----------|
| 60 seconds | No new log output while run is in progress |

When stuck is detected, users can:
1. **Open Logs** - View raw logs.txt in default editor
2. **Open Folder** - Browse run directory in Explorer
3. **Copy Diagnostics** - Copy debug summary to clipboard

### Diagnostics Summary
The clipboard diagnostics include:
- Run ID and status
- Last log update time
- Current milestone state
- Epoch progress (if any)
- Last 10 log lines

## Implementation Notes

### LogMonitorState
Stateful tracking between polling cycles:
```csharp
public sealed class LogMonitorState
{
    public long LastByteOffset { get; set; }
    public long LastFileSize { get; set; }
    public DateTime LastCreationTimeUtc { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public DateTime LastActivityUtc { get; set; }
    public TimeSpan CurrentPollingInterval { get; set; }
}
```

### FileChangeReason
Detected file changes:
- `None` - Normal operation
- `Truncated` - File size decreased
- `Replaced` - File creation time changed
- `Deleted` - File no longer exists

### LogDeltaResult
Result from reading new log content:
```csharp
public record LogDeltaResult
{
    public required IReadOnlyList<string> Lines { get; init; }
    public bool WasReset { get; init; }
    public FileChangeReason ResetReason { get; init; }
    public bool WasCapped { get; init; }
    public long BytesRemaining { get; init; }
    public string? Error { get; init; }
}
```

## Runner Integration

To integrate with RunForge live monitoring:

1. **Write to stdout** - All output is captured to `logs.txt`
2. **Emit stage tokens** - Use `[RF:STAGE=X]` for guaranteed milestone detection
3. **Emit epoch tokens** - Use `[RF:EPOCH=X/Y]` for precise progress tracking
4. **Flush output** - Ensure Python output is unbuffered (`-u` flag or `PYTHONUNBUFFERED=1`)

### Example Runner Output
```
2024-01-15 10:30:00 INFO [RF:STAGE=STARTING] Initializing training run
2024-01-15 10:30:01 INFO [RF:STAGE=LOADING_DATASET] Loading iris.csv
2024-01-15 10:30:02 INFO Loaded 150 samples
2024-01-15 10:30:03 INFO [RF:STAGE=TRAINING] Starting training
2024-01-15 10:30:04 INFO [RF:EPOCH=1/10] loss=0.892
2024-01-15 10:30:05 INFO [RF:EPOCH=2/10] loss=0.654
...
2024-01-15 10:30:15 INFO [RF:STAGE=EVALUATING] Running evaluation
2024-01-15 10:30:16 INFO Accuracy: 0.95
2024-01-15 10:30:17 INFO [RF:STAGE=WRITING_ARTIFACTS] Saving model
2024-01-15 10:30:18 INFO [RF:STAGE=COMPLETED] Training finished
```

## Changelog

### v0.2.2
- Added log truncation/replacement detection
- Added adaptive polling (fast/slow based on activity)
- Added MaxBytesPerTick cap (64KB) for UI smoothness
- Added stuck detection with actionable UI
- Added explicit runner tokens `[RF:STAGE=X]` and `[RF:EPOCH=X/Y]`

### v0.2.1
- Initial live monitoring with heartbeat indicator
- Run timeline with milestone detection
- Live log tail with pause, search, copy
