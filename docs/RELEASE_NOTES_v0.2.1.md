# RunForge Desktop v0.2.1 Release Notes

**Release Date:** 2026-02-01

## Overview

v0.2.1 introduces **Live Run Visuals** - real-time monitoring of training runs driven entirely by `logs.txt` file data. No fake animations; everything is honest and reflects actual file activity.

## New Features

### Live Status Section

The Run Detail page now includes a dedicated Live Status section with three trustworthy visual cues:

#### Heartbeat Indicator
- **Data Source**: File mtime + file length of `logs.txt`
- **Status Pill** showing real-time status:
  - Green "Receiving logs" when updated recently
  - Amber "No new logs for Xs" when stale (configurable threshold, default 30s)
  - Blue "Run completed" or Red "Run failed" based on result status
- **Signal strip**: Shows `+N lines` when new lines arrive (honest activity indicator)

#### Run Timeline
- **Milestones** that light up based on observed log patterns:
  - Starting
  - Loading Dataset
  - Training
  - Evaluating
  - Writing Artifacts
  - Completed
- **Epoch Progress**: Detects `Epoch X/Y` patterns and displays current progress
- **Pattern-based**: Each milestone triggers only when a marker is actually observed in logs
- **Honest**: If a marker never appears, the milestone stays inactive (no pretending)

#### Live Log Tail
- **Streaming tail**: Last 200 lines, updated via byte offset polling
- **Pause toggle**: Stops auto-scroll while you're reading
- **Search filter**: Filter visible lines by keyword
- **Copy visible**: Copy currently displayed lines to clipboard
- **Open logs.txt**: Quick access to the full log file

### Implementation Details

#### File Monitoring
- Polls `logs.txt` every 500ms (configurable)
- Uses `FileInfo.LastWriteTimeUtc` for staleness detection
- Uses byte offset delta reads for efficiency (only reads new bytes)
- Handles file locking gracefully (allows reading while VS Code writes)

#### Pattern Matching
Log patterns use compiled regex for efficient matching:

```
Starting       → /starting|initializ|begin|run\s+id/i
Loading Dataset → /load(ing)?\s+(dataset|data)|reading\s+data|dataset:/i
Training        → /epoch\s+\d+|training\s+started|fit\(/i
Evaluating      → /evaluat|validation|scor(e|ing)/i
Writing Artifacts → /saving|wrote|artifact|model\s+saved/i
Completed       → /complet(e|ed)|finished|done|success/i
```

#### Memory Management
- Ring buffer implementation for log tail (fixed memory footprint)
- Monitoring stops automatically when run completes
- Proper `IDisposable` implementation for cleanup

## Test Coverage

- 195 total tests (36 new tests for live monitoring)
- `LiveLogServiceTests.cs`: 14 tests covering snapshot, delta reads, tail reads
- `RunTimelineServiceTests.cs`: 11 tests covering timeline state management
- `MilestonePatternTests.cs`: 11 tests covering regex pattern detection

## Technical Changes

### New Services
- `ILiveLogService` / `LiveLogService`: File monitoring and delta reads
- `IRunTimelineService` / `RunTimelineService`: Timeline state management

### New Models
- `LogSnapshot`: Point-in-time log file state
- `LogStatus` enum: NoLogs, Receiving, Stale, Completed, Failed
- `RunMilestone`: Individual timeline milestone
- `MilestoneType` enum: Starting, LoadingDataset, Training, Evaluating, WritingArtifacts, Completed, Failed
- `RunTimelineState`: Full timeline state with epoch progress

### New Converters
- `LogStatusToColorConverter`: Status → color mapping
- `MilestoneToColorConverter`: Reached state → color mapping
- `MilestoneToFontConverter`: Active state → font weight

### ViewModel Updates
- `RunDetailViewModel` now implements `IDisposable`
- Added live monitoring properties: `LogSnapshot`, `TimelineState`, `LogTailLines`
- Added commands: `ToggleLogTailPause`, `ToggleLogTail`, `CopyVisibleLogs`, `ClearLogSearch`

## Breaking Changes

None. This release is fully compatible with v0.2.0 workspaces.

## Upgrade Instructions

1. Update to v0.2.1
2. Open any run that has `logs.txt`
3. Live monitoring starts automatically for in-progress runs
4. Completed runs show the log tail and final timeline state

## Contributors

- Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
