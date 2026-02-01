using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;
using RunForgeDesktop.Views;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the run detail page with robust live monitoring support.
/// </summary>
public partial class RunDetailViewModel : ObservableObject, IQueryAttributable, IDisposable
{
    private readonly IRunDetailService _runDetailService;
    private readonly IWorkspaceService _workspaceService;
    private readonly ILiveLogService _liveLogService;
    private readonly IRunTimelineService _timelineService;

    private CancellationTokenSource? _monitoringCts;
    private bool _disposed;

    // Robust monitoring state
    private readonly LogMonitorState _monitorState = new();

    #region Observable Properties - Core

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _runId;

    [ObservableProperty]
    private string? _runName;

    [ObservableProperty]
    private string? _runDir;

    [ObservableProperty]
    private RunRequest? _request;

    [ObservableProperty]
    private RunResult? _result;

    [ObservableProperty]
    private TrainingMetrics? _metrics;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _showRawJsonModal;

    [ObservableProperty]
    private string? _rawJsonContent;

    [ObservableProperty]
    private string? _rawJsonTitle;

    #endregion

    #region Observable Properties - Live Monitoring

    [ObservableProperty]
    private LogSnapshot? _logSnapshot;

    [ObservableProperty]
    private RunTimelineState? _timelineState;

    [ObservableProperty]
    private ObservableCollection<string> _logTailLines = [];

    [ObservableProperty]
    private bool _isLogTailPaused;

    [ObservableProperty]
    private string? _logSearchQuery;

    [ObservableProperty]
    private bool _showLogTail;

    [ObservableProperty]
    private int _linesAddedThisTick;

    [ObservableProperty]
    private bool _showStuckWarning;

    [ObservableProperty]
    private string? _lastMilestoneName;

    [ObservableProperty]
    private string? _lastMilestoneTime;

    [ObservableProperty]
    private bool _wasFileReset;

    [ObservableProperty]
    private string? _fileResetReason;

    [ObservableProperty]
    private bool _logSizeWarningDismissed;

    /// <summary>
    /// Stale threshold in seconds (configurable).
    /// </summary>
    public TimeSpan StaleThreshold { get; set; } = TimeSpan.FromSeconds(30);

    // Log size warning thresholds
    private const long LogSizeInfoThreshold = 50 * 1024 * 1024;      // 50 MB
    private const long LogSizeWarningThreshold = 250 * 1024 * 1024;  // 250 MB
    private const long LogSizeDangerThreshold = 1024L * 1024 * 1024; // 1 GB

    /// <summary>
    /// Threshold for showing "possible stall" warning.
    /// </summary>
    public TimeSpan StuckWarningThreshold { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum lines to keep in tail buffer.
    /// </summary>
    public int MaxTailLines { get; set; } = 200;

    /// <summary>
    /// Base polling interval (adaptive polling will adjust).
    /// </summary>
    public TimeSpan BasePollingInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    #endregion

    #region Computed Properties

    /// <summary>
    /// Whether the request has optional fields to display.
    /// </summary>
    public bool HasRequestExtras => Request is not null &&
        (!string.IsNullOrEmpty(Request.Name) ||
         !string.IsNullOrEmpty(Request.RerunFrom) ||
         (Request.Tags is not null && Request.Tags.Count > 0) ||
         !string.IsNullOrEmpty(Request.Notes) ||
         !string.IsNullOrEmpty(Request.Device.GpuReason));

    /// <summary>
    /// Formatted tags string.
    /// </summary>
    public string? TagsDisplay => Request?.Tags is not null && Request.Tags.Count > 0
        ? string.Join(", ", Request.Tags)
        : null;

    /// <summary>
    /// Formatted created_at timestamp.
    /// </summary>
    public string? FormattedCreatedAt
    {
        get
        {
            if (Request?.ParsedCreatedAt is { } parsed)
            {
                return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
            return Request?.CreatedAt;
        }
    }

    /// <summary>
    /// Whether the run is still in progress (no result yet or status not set).
    /// </summary>
    public bool IsRunInProgress => Result is null ||
        (Result.Status != "succeeded" && Result.Status != "failed");

    /// <summary>
    /// Whether live monitoring is active.
    /// </summary>
    public bool IsMonitoringActive => _monitoringCts is not null && !_monitoringCts.IsCancellationRequested;

    /// <summary>
    /// Epoch progress display string.
    /// </summary>
    public string? EpochProgressDisplay
    {
        get
        {
            if (TimelineState?.EpochProgress is not { } progress)
                return null;

            return progress.Total > 0
                ? $"Epoch {progress.Current}/{progress.Total}"
                : $"Epoch {progress.Current}";
        }
    }

    /// <summary>
    /// Filtered log lines based on search query.
    /// </summary>
    public IEnumerable<string> FilteredLogLines
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LogSearchQuery))
                return LogTailLines;

            return LogTailLines.Where(line =>
                line.Contains(LogSearchQuery, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Whether there's actionable stuck state (stale + in progress + past threshold).
    /// </summary>
    public bool IsActionableStuck =>
        LogSnapshot?.Status == LogStatus.Stale &&
        IsRunInProgress &&
        LogSnapshot?.TimeSinceLastUpdate > StuckWarningThreshold;

    /// <summary>
    /// Formatted log file size for display.
    /// </summary>
    public string LogFileSizeDisplay
    {
        get
        {
            var bytes = LogSnapshot?.FileSizeBytes ?? 0;
            return FormatFileSize(bytes);
        }
    }

    /// <summary>
    /// Estimated line count (based on ~80 bytes per line).
    /// </summary>
    public string LogLineCountEstimate
    {
        get
        {
            var lines = LogSnapshot?.TotalLineCount ?? 0;
            return lines > 0 ? $"~{lines:N0} lines" : "—";
        }
    }

    /// <summary>
    /// Full path to the logs.txt file.
    /// </summary>
    public string LogFilePath => GetLogFilePath();

    /// <summary>
    /// Log size warning level: none, info, warning, or danger.
    /// </summary>
    public string LogSizeWarningLevel
    {
        get
        {
            var bytes = LogSnapshot?.FileSizeBytes ?? 0;
            if (bytes >= LogSizeDangerThreshold) return "danger";
            if (bytes >= LogSizeWarningThreshold) return "warning";
            if (bytes >= LogSizeInfoThreshold) return "info";
            return "none";
        }
    }

    /// <summary>
    /// Whether to show log size warning banner.
    /// </summary>
    public bool ShowLogSizeWarning => !LogSizeWarningDismissed && LogSizeWarningLevel != "none";

    /// <summary>
    /// Warning message based on log size.
    /// </summary>
    public string LogSizeWarningMessage
    {
        get
        {
            return LogSizeWarningLevel switch
            {
                "danger" => "Very large log file (1GB+). Disk usage is significant. Consider archiving or deleting old runs.",
                "warning" => "Large log file (250MB+). Consider archiving or deleting old runs to free disk space.",
                "info" => "Log file is growing (50MB+). Tail view is optimized, but disk usage may grow.",
                _ => ""
            };
        }
    }

    #endregion

    public RunDetailViewModel(
        IRunDetailService runDetailService,
        IWorkspaceService workspaceService,
        ILiveLogService liveLogService,
        IRunTimelineService timelineService)
    {
        _runDetailService = runDetailService;
        _workspaceService = workspaceService;
        _liveLogService = liveLogService;
        _timelineService = timelineService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("runId", out var runIdObj) && runIdObj is string runId)
        {
            RunId = runId;
        }

        if (query.TryGetValue("runName", out var nameObj) && nameObj is string name)
        {
            RunName = name;
        }

        if (query.TryGetValue("runDir", out var dirObj) && dirObj is string dir)
        {
            RunDir = dir;
            _ = LoadRunDetailAsync();
        }
    }

    #region Property Change Handlers

    partial void OnRequestChanged(RunRequest? value)
    {
        OnPropertyChanged(nameof(HasRequestExtras));
        OnPropertyChanged(nameof(TagsDisplay));
        OnPropertyChanged(nameof(FormattedCreatedAt));
    }

    partial void OnResultChanged(RunResult? value)
    {
        OnPropertyChanged(nameof(IsRunInProgress));
        OnPropertyChanged(nameof(IsActionableStuck));

        // Update timeline if run completed
        if (value is not null && TimelineState is not null)
        {
            TimelineState = _timelineService.SetCompleted(TimelineState, value.IsSucceeded);
        }

        // Stop monitoring if run is complete
        if (value is not null && !IsRunInProgress)
        {
            StopMonitoring();
            ShowStuckWarning = false;
        }
    }

    partial void OnLogSearchQueryChanged(string? value)
    {
        OnPropertyChanged(nameof(FilteredLogLines));
    }

    partial void OnTimelineStateChanged(RunTimelineState? value)
    {
        OnPropertyChanged(nameof(EpochProgressDisplay));
        UpdateLastMilestone();
    }

    partial void OnLogSnapshotChanged(LogSnapshot? value)
    {
        OnPropertyChanged(nameof(IsActionableStuck));
        OnPropertyChanged(nameof(LogFileSizeDisplay));
        OnPropertyChanged(nameof(LogLineCountEstimate));
        OnPropertyChanged(nameof(LogSizeWarningLevel));
        OnPropertyChanged(nameof(ShowLogSizeWarning));
        OnPropertyChanged(nameof(LogSizeWarningMessage));

        // Update stuck warning state
        ShowStuckWarning = IsActionableStuck;
    }

    partial void OnLogSizeWarningDismissedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLogSizeWarning));
    }

    #endregion

    #region Commands - Core

    [RelayCommand]
    private async Task LoadRunDetailAsync()
    {
        if (string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            ErrorMessage = "Invalid run or workspace";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var loadResult = await _runDetailService.LoadRunDetailAsync(
                _workspaceService.CurrentWorkspacePath,
                RunDir);

            if (loadResult.IsSuccess)
            {
                Request = loadResult.Request;
                Result = loadResult.Result;
                Metrics = loadResult.Metrics;

                // Initialize timeline
                TimelineState = _timelineService.CreateTimeline();

                // Start live monitoring if run is in progress
                if (IsRunInProgress)
                {
                    await StartMonitoringAsync();
                }
                else
                {
                    // Load initial log tail even for completed runs
                    await LoadInitialLogTailAsync();

                    // Set timeline to completed state
                    if (Result is not null)
                    {
                        TimelineState = _timelineService.SetCompleted(TimelineState, Result.IsSucceeded);
                    }
                }
            }
            else
            {
                ErrorMessage = loadResult.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load run details: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenRawFileAsync(string fileName)
    {
        if (string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            return;
        }

        var filePath = Path.Combine(
            _workspaceService.CurrentWorkspacePath,
            RunDir.Replace('/', Path.DirectorySeparatorChar),
            fileName);

        if (File.Exists(filePath))
        {
            try
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath)
                });
            }
            catch
            {
                // Silently ignore if we can't open
            }
        }
    }

    [RelayCommand]
    private async Task OpenRunFolderAsync()
    {
        if (string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            return;
        }

        var folderPath = Path.Combine(
            _workspaceService.CurrentWorkspacePath,
            RunDir.Replace('/', Path.DirectorySeparatorChar));

        if (Directory.Exists(folderPath))
        {
            try
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(folderPath)
                });
            }
            catch
            {
                // Silently ignore if we can't open
            }
        }
    }

    [RelayCommand]
    private async Task CopyRequestJsonAsync()
    {
        if (Request is null)
        {
            StatusMessage = "No request data available";
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(Request, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            await Clipboard.Default.SetTextAsync(json);
            StatusMessage = "Request JSON copied to clipboard";
            _ = ClearStatusAfterDelayAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ViewRequestJson()
    {
        if (Request is null)
        {
            return;
        }

        try
        {
            RawJsonContent = JsonSerializer.Serialize(Request, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            RawJsonTitle = "Request JSON";
            ShowRawJsonModal = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to format JSON: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CloseRawJsonModal()
    {
        ShowRawJsonModal = false;
        RawJsonContent = null;
        RawJsonTitle = null;
    }

    [RelayCommand]
    private async Task CopyRawJsonAsync()
    {
        if (string.IsNullOrEmpty(RawJsonContent))
        {
            return;
        }

        try
        {
            await Clipboard.Default.SetTextAsync(RawJsonContent);
            StatusMessage = "JSON copied to clipboard";
            _ = ClearStatusAfterDelayAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task NavigateToInterpretabilityAsync()
    {
        if (string.IsNullOrEmpty(RunId) || string.IsNullOrEmpty(RunDir))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { "runId", RunId },
            { "runName", RunName ?? RunId },
            { "runDir", RunDir }
        };

        await Shell.Current.GoToAsync(nameof(InterpretabilityPage), parameters);
    }

    #endregion

    #region Commands - Live Log

    [RelayCommand]
    private void ToggleLogTailPause()
    {
        IsLogTailPaused = !IsLogTailPaused;
    }

    [RelayCommand]
    private void ToggleLogTail()
    {
        ShowLogTail = !ShowLogTail;
    }

    [RelayCommand]
    private async Task CopyVisibleLogsAsync()
    {
        var lines = FilteredLogLines.ToList();
        if (lines.Count == 0)
        {
            StatusMessage = "No logs to copy";
            return;
        }

        try
        {
            await Clipboard.Default.SetTextAsync(string.Join(Environment.NewLine, lines));
            StatusMessage = $"Copied {lines.Count} lines to clipboard";
            _ = ClearStatusAfterDelayAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearLogSearch()
    {
        LogSearchQuery = null;
    }

    [RelayCommand]
    private async Task CopyLogFilePathAsync()
    {
        var path = GetLogFilePath();
        if (string.IsNullOrEmpty(path))
        {
            StatusMessage = "No log file path available";
            return;
        }

        try
        {
            await Clipboard.Default.SetTextAsync(path);
            StatusMessage = "Log file path copied to clipboard";
            _ = ClearStatusAfterDelayAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DismissLogSizeWarning()
    {
        LogSizeWarningDismissed = true;
    }

    [RelayCommand]
    private async Task CopyDiagnosticsSummaryAsync()
    {
        var summary = BuildDiagnosticsSummary();

        try
        {
            await Clipboard.Default.SetTextAsync(summary);
            StatusMessage = "Diagnostics copied to clipboard";
            _ = ClearStatusAfterDelayAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DismissStuckWarning()
    {
        ShowStuckWarning = false;
    }

    #endregion

    #region Live Monitoring

    private string GetLogFilePath()
    {
        if (string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
            return string.Empty;

        return Path.Combine(
            _workspaceService.CurrentWorkspacePath,
            RunDir.Replace('/', Path.DirectorySeparatorChar),
            "logs.txt");
    }

    private async Task StartMonitoringAsync()
    {
        StopMonitoring();

        _monitorState.Reset();
        _monitoringCts = new CancellationTokenSource();
        var token = _monitoringCts.Token;

        // Load initial tail
        await LoadInitialLogTailAsync();

        // Start polling loop with adaptive interval
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Use adaptive polling interval
                    var interval = _monitorState.CurrentPollingInterval;
                    await Task.Delay(interval, token);
                    await PollLogFileAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Log error but continue polling
                }
            }
        }, token);

        OnPropertyChanged(nameof(IsMonitoringActive));
    }

    private void StopMonitoring()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
        OnPropertyChanged(nameof(IsMonitoringActive));
    }

    private async Task LoadInitialLogTailAsync()
    {
        var logPath = GetLogFilePath();
        if (string.IsNullOrEmpty(logPath))
            return;

        var (lines, totalBytes) = await _liveLogService.ReadTailAsync(logPath, MaxTailLines);

        // Initialize monitor state
        _monitorState.LastByteOffset = totalBytes;
        _monitorState.LastFileSize = totalBytes;
        if (File.Exists(logPath))
        {
            var fileInfo = new FileInfo(logPath);
            _monitorState.LastCreationTimeUtc = fileInfo.CreationTimeUtc;
            _monitorState.LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        }

        // Update on UI thread
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            LogTailLines.Clear();
            foreach (var line in lines)
            {
                LogTailLines.Add(line);
            }

            // Process lines for timeline
            if (TimelineState is not null)
            {
                TimelineState = _timelineService.ProcessLogLines(TimelineState, lines);
            }

            // Get initial snapshot
            var runStatus = Result?.Status;
            LogSnapshot = _liveLogService.GetSnapshot(logPath, _monitorState, runStatus, StaleThreshold);
        });
    }

    private async Task PollLogFileAsync(CancellationToken token)
    {
        var logPath = GetLogFilePath();
        if (string.IsNullOrEmpty(logPath))
            return;

        // Get snapshot with change detection
        var runStatus = Result?.Status;
        var snapshot = _liveLogService.GetSnapshot(logPath, _monitorState, runStatus, StaleThreshold);

        // Check for file changes
        var wasReset = false;
        var resetReason = FileChangeReason.None;

        if (snapshot.FileChange != FileChangeReason.None)
        {
            wasReset = true;
            resetReason = snapshot.FileChange;

            // Reset and reload if file was replaced/truncated
            _monitorState.Reset();
            if (resetReason != FileChangeReason.Deleted)
            {
                await LoadInitialLogTailAsync();
            }
        }

        // Read delta if not paused and file exists
        LogDeltaResult? deltaResult = null;
        if (!IsLogTailPaused && snapshot.Status != LogStatus.NoLogs && !wasReset)
        {
            deltaResult = await _liveLogService.ReadDeltaAsync(logPath, _monitorState, MaxTailLines);

            if (deltaResult.WasReset)
            {
                wasReset = true;
                resetReason = deltaResult.ResetReason;
            }
        }

        var newLines = deltaResult?.Lines ?? Array.Empty<string>();

        // Update adaptive polling interval
        _monitorState.CurrentPollingInterval = snapshot.RecommendedPollingInterval;

        // Update on UI thread
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            LogSnapshot = snapshot;
            LinesAddedThisTick = newLines.Count;

            // Show file reset notification
            if (wasReset && resetReason != FileChangeReason.None)
            {
                WasFileReset = true;
                FileResetReason = resetReason switch
                {
                    FileChangeReason.Truncated => "Log file was truncated",
                    FileChangeReason.Replaced => "Log file was replaced",
                    FileChangeReason.Deleted => "Log file was deleted",
                    _ => "Log file changed"
                };

                // Clear after 5 seconds
                _ = ClearFileResetNotificationAsync();
            }

            if (newLines.Count > 0)
            {
                // Add new lines to tail
                foreach (var line in newLines)
                {
                    LogTailLines.Add(line);
                }

                // Trim to max size
                while (LogTailLines.Count > MaxTailLines)
                {
                    LogTailLines.RemoveAt(0);
                }

                // Update timeline
                if (TimelineState is not null)
                {
                    TimelineState = _timelineService.ProcessLogLines(TimelineState, newLines);
                }

                OnPropertyChanged(nameof(FilteredLogLines));
            }
        });
    }

    private async Task ClearFileResetNotificationAsync()
    {
        await Task.Delay(5000);
        WasFileReset = false;
        FileResetReason = null;
    }

    private void UpdateLastMilestone()
    {
        if (TimelineState is null)
        {
            LastMilestoneName = null;
            LastMilestoneTime = null;
            return;
        }

        // Find the last reached milestone
        var lastReached = TimelineState.Milestones
            .Where(m => m.IsReached)
            .OrderByDescending(m => m.ReachedAtUtc)
            .FirstOrDefault();

        if (lastReached is not null)
        {
            LastMilestoneName = lastReached.Name;
            LastMilestoneTime = lastReached.ReachedAtUtc?.ToLocalTime().ToString("HH:mm:ss");
        }
        else
        {
            LastMilestoneName = null;
            LastMilestoneTime = null;
        }
    }

    private string BuildDiagnosticsSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== RunForge Diagnostics ===");
        sb.AppendLine($"Run ID: {RunId}");
        sb.AppendLine($"Run Name: {RunName}");
        sb.AppendLine($"Run Directory: {RunDir}");
        sb.AppendLine();

        sb.AppendLine("--- Status ---");
        sb.AppendLine($"Log Status: {LogSnapshot?.Status}");
        sb.AppendLine($"Time Since Last Update: {LogSnapshot?.TimeSinceLastUpdate}");
        sb.AppendLine($"File Size: {LogSnapshot?.FileSizeBytes:N0} bytes");
        sb.AppendLine();

        sb.AppendLine("--- Timeline ---");
        sb.AppendLine($"Last Milestone: {LastMilestoneName} at {LastMilestoneTime}");
        sb.AppendLine($"Epoch Progress: {EpochProgressDisplay ?? "Not detected"}");
        sb.AppendLine();

        if (TimelineState?.Milestones is not null)
        {
            sb.AppendLine("Milestones:");
            foreach (var m in TimelineState.Milestones)
            {
                var status = m.IsReached ? "[X]" : "[ ]";
                var time = m.ReachedAtUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "";
                sb.AppendLine($"  {status} {m.Name} {time}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("--- Request ---");
        sb.AppendLine($"Preset: {Request?.Preset}");
        sb.AppendLine($"Model: {Request?.Model.Family}");
        sb.AppendLine($"Device: {Request?.Device.Type}");
        sb.AppendLine();

        sb.AppendLine("--- Last 10 Log Lines ---");
        var lastLines = LogTailLines.TakeLast(10);
        foreach (var line in lastLines)
        {
            sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        return sb.ToString();
    }

    #endregion

    #region Helpers

    private async Task ClearStatusAfterDelayAsync()
    {
        await Task.Delay(3000);
        if (StatusMessage is not null && !StatusMessage.StartsWith("Failed"))
        {
            StatusMessage = null;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            StopMonitoring();
        }

        _disposed = true;
    }

    #endregion
}
