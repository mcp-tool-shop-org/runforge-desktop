# Release Notes v0.2.3

## Logs: Safe and Understandable

This release adds log size visibility, workspace storage management, and intelligent polling backoff to reduce resource usage.

### New Features

#### Log Size Telemetry (Live Status)
- Shows `logs.txt` file size and estimated line count in Live Status section
- Tiered warning banners:
  - **Info** (blue): Log file exceeds 50 MB
  - **Warning** (orange): Log file exceeds 250 MB
  - **Danger** (red): Log file exceeds 1 GB
- Copy log file path button for quick access
- Dismissible warnings per session

#### Workspace Storage View (Diagnostics)
- New **Storage** section in Diagnostics page
- Shows total workspace size, logs size, and artifacts size
- Lists **Top 10 Largest Runs** with:
  - Run name and total size
  - Breakdown of logs vs artifacts
  - Run status (succeeded/failed)
  - **Open** button to open run folder in Explorer
  - **Delete** button with confirmation dialog
- Automatic refresh after deletions

#### Polling Backoff
- Progressive polling intervals based on run state:
  - **Active**: 500ms (fast polling)
  - **Stale > 30s**: 2s (slow polling)
  - **Stale > 5m**: 10s (very slow polling)
  - **Completed/Failed**: 30s (terminal polling)
- Reduces CPU and I/O overhead for idle or finished runs

### Technical Details

#### New Services
- `IStorageService` / `StorageService` - Workspace storage calculation and cleanup

#### New Types
- `RunStorageInfo` - Storage info for a single run (size, logs, artifacts)
- `WorkspaceStorageSummary` - Aggregate storage summary for workspace

#### New Properties
- `ILiveLogService.VeryStaleThreshold` (default: 5 minutes)
- `ILiveLogService.VeryStalePollingInterval` (default: 10s)
- `ILiveLogService.TerminalPollingInterval` (default: 30s)

#### ViewModel Additions
- `RunDetailViewModel`: Log size display, warning levels, copy path command
- `DiagnosticsViewModel`: Storage loading, top runs list, delete confirmation flow

### Tests
- 220 total tests
- 100% pass rate

### Files Changed
- `IStorageService.cs` - New storage service interface and types
- `StorageService.cs` - Storage calculation implementation
- `LiveLogService.cs` - Progressive polling backoff
- `RunDetailViewModel.cs` - Log size telemetry and warnings
- `RunDetailPage.xaml` - Log size display and warning banners
- `DiagnosticsViewModel.cs` - Storage section logic
- `DiagnosticsPage.xaml` - Storage UI with top runs list
- `MauiProgram.cs` - StorageService DI registration

## Upgrading

No breaking changes. Drop-in replacement for v0.2.2.

## Roadmap

- **Log rotation** - Automatic archival/compression of large logs (planned for future release)

## Full Changelog

- feecd53 feat(polling): add progressive backoff for stale/terminal runs
- a2d42ae feat(diagnostics): add Storage section with disk usage and cleanup
- b663864 feat(live-status): add log size telemetry and warning banners
