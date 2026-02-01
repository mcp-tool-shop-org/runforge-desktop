using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;
using RunForgeDesktop.Views;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the run detail page with live monitoring support.
/// </summary>
public partial class RunDetailViewModel : ObservableObject, IQueryAttributable, IDisposable
{
    private readonly IRunDetailService _runDetailService;
    private readonly IWorkspaceService _workspaceService;
    private readonly ILiveLogService _liveLogService;
    private readonly IRunTimelineService _timelineService;

    private CancellationTokenSource? _monitoringCts;
    private bool _disposed;

    // Monitoring state
    private long _lastKnownLogSize;
    private long _lastKnownByteOffset;

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

    /// <summary>
    /// Stale threshold in seconds (configurable).
    /// </summary>
    public TimeSpan StaleThreshold { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum lines to keep in tail buffer.
    /// </summary>
    public int MaxTailLines { get; set; } = 200;

    /// <summary>
    /// Polling interval for log monitoring.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(500);

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

        // Update timeline if run completed
        if (value is not null && TimelineState is not null)
        {
            TimelineState = _timelineService.SetCompleted(TimelineState, value.IsSucceeded);
        }

        // Stop monitoring if run is complete
        if (value is not null && !IsRunInProgress)
        {
            StopMonitoring();
        }
    }

    partial void OnLogSearchQueryChanged(string? value)
    {
        OnPropertyChanged(nameof(FilteredLogLines));
    }

    partial void OnTimelineStateChanged(RunTimelineState? value)
    {
        OnPropertyChanged(nameof(EpochProgressDisplay));
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

        _monitoringCts = new CancellationTokenSource();
        var token = _monitoringCts.Token;

        // Load initial tail
        await LoadInitialLogTailAsync();

        // Start polling loop
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(PollingInterval, token);
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

        _lastKnownByteOffset = totalBytes;
        _lastKnownLogSize = totalBytes;

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
            LogSnapshot = _liveLogService.GetSnapshot(logPath, 0, runStatus, StaleThreshold);
        });
    }

    private async Task PollLogFileAsync(CancellationToken token)
    {
        var logPath = GetLogFilePath();
        if (string.IsNullOrEmpty(logPath))
            return;

        // Get snapshot
        var runStatus = Result?.Status;
        var snapshot = _liveLogService.GetSnapshot(logPath, _lastKnownLogSize, runStatus, StaleThreshold);

        // Read delta if not paused and there's new data
        IReadOnlyList<string> newLines = Array.Empty<string>();
        if (!IsLogTailPaused && snapshot.FileSizeBytes > _lastKnownByteOffset)
        {
            (newLines, _lastKnownByteOffset) = await _liveLogService.ReadDeltaAsync(
                logPath,
                _lastKnownByteOffset,
                MaxTailLines);
        }

        _lastKnownLogSize = snapshot.FileSizeBytes;

        // Update on UI thread
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            LogSnapshot = snapshot;
            LinesAddedThisTick = newLines.Count;

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
