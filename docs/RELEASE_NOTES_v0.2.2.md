# Release Notes v0.2.2

## Live Monitoring Robustness

This release makes live log monitoring resilient to edge cases and adds actionable guidance when runs appear stuck.

### New Features

#### Robustness Improvements
- **Log truncation detection**: Gracefully handles when `logs.txt` is truncated or rotated
- **File replacement detection**: Detects when log file is deleted and recreated
- **Adaptive polling**: Fast polling (500ms) when active, slow polling (3s) when stale
- **MaxBytesPerTick cap**: Limits to 64KB per poll to prevent UI hitching on large logs

#### Stuck Detection
- Shows "Possible Stall" warning when no log output for 60+ seconds
- Quick actions: Open Logs, Open Folder, Copy Diagnostics
- Diagnostics summary includes run state, milestones, and last 10 log lines

#### Explicit Runner Tokens
Runners can now emit guaranteed milestone markers:
```
[RF:STAGE=STARTING]
[RF:STAGE=LOADING_DATASET]
[RF:STAGE=TRAINING]
[RF:STAGE=EVALUATING]
[RF:STAGE=WRITING_ARTIFACTS]
[RF:STAGE=COMPLETED]
[RF:STAGE=FAILED]
```

Epoch progress with explicit tokens:
```
[RF:EPOCH=5/10]
```

Explicit tokens take priority over heuristic pattern matching.

### Technical Details

#### New Types
- `FileChangeReason` enum: None, Truncated, Replaced, Deleted
- `LogMonitorState` class: Stateful tracking across polling cycles
- `LogDeltaResult` record: Robust delta read results with capping info

#### New Properties
- `ILiveLogService.MaxBytesPerTick` (default: 64KB)
- `ILiveLogService.SlowPollingThreshold` (default: 60s)
- `ILiveLogService.SlowPollingInterval` (default: 3s)
- `ILiveLogService.FastPollingInterval` (default: 500ms)

### Tests
- 220 total tests (13 new in this release)
- 100% pass rate

### Documentation
- Added `docs/LIVE_MONITORING.md` with full integration guide

### Files Changed
- `ILiveLogService.cs` - Extended with robustness types and properties
- `LiveLogService.cs` - Rewritten with truncation detection and adaptive polling
- `RunMilestone.cs` - Added explicit token detection
- `RunDetailViewModel.cs` - Added stuck detection and diagnostics
- `RunDetailPage.xaml` - Added stuck warning UI

## Upgrading

No breaking changes. Drop-in replacement for v0.2.1.

## Full Changelog

- b17b9a5 feat(live-logs): add robustness and stuck detection
- d22a6d3 feat(timeline): add explicit runner tokens [RF:STAGE=X]
