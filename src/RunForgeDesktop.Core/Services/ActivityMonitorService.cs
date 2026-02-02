using System.ComponentModel;
using System.Runtime.CompilerServices;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of IActivityMonitorService that polls queue state every 2 seconds.
/// </summary>
public class ActivityMonitorService : IActivityMonitorService
{
    private readonly IExecutionQueueService _queueService;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _staleHeartbeatThreshold = TimeSpan.FromSeconds(30);

    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private string? _workspacePath;
    private QueueStatusSummary? _currentStatus;
    private ActivitySystemState _systemState = ActivitySystemState.Idle;
    private DateTime? _lastActivityTime;
    private string? _statusReason;
    private int _previousCompletedCount;

    public ActivityMonitorService(IExecutionQueueService queueService)
    {
        _queueService = queueService;
    }

    // === INotifyPropertyChanged ===
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    // === Properties ===
    public QueueStatusSummary? CurrentStatus
    {
        get => _currentStatus;
        private set
        {
            if (SetProperty(ref _currentStatus, value))
            {
                // Notify all derived properties
                OnPropertyChanged(nameof(ActiveCpuSlots));
                OnPropertyChanged(nameof(TotalCpuSlots));
                OnPropertyChanged(nameof(ActiveGpuSlots));
                OnPropertyChanged(nameof(TotalGpuSlots));
                OnPropertyChanged(nameof(QueuedGpuCount));
                OnPropertyChanged(nameof(HasGpuSlots));
                OnPropertyChanged(nameof(QueuedCount));
                OnPropertyChanged(nameof(DaemonHealthy));
                OnPropertyChanged(nameof(DaemonRunning));
                OnPropertyChanged(nameof(DaemonStateText));
            }
        }
    }

    public ActivitySystemState SystemState
    {
        get => _systemState;
        private set => SetProperty(ref _systemState, value);
    }

    public DateTime? LastActivityTime
    {
        get => _lastActivityTime;
        private set => SetProperty(ref _lastActivityTime, value);
    }

    public string? StatusReason
    {
        get => _statusReason;
        private set => SetProperty(ref _statusReason, value);
    }

    public int ActiveCpuSlots => _currentStatus?.RunningCount ?? 0;
    public int TotalCpuSlots => _currentStatus?.MaxParallel ?? 2;
    public int ActiveGpuSlots => _currentStatus?.DaemonStatus.ActiveGpuJobs ?? 0;
    public int TotalGpuSlots => _currentStatus?.DaemonStatus.GpuSlots ?? 0;
    public int QueuedGpuCount => _currentStatus?.QueuedGpuCount ?? 0;
    public bool HasGpuSlots => TotalGpuSlots > 0;
    public int QueuedCount => _currentStatus?.QueuedCount ?? 0;
    public bool DaemonHealthy => _currentStatus?.DaemonStatus.IsHealthy ?? false;
    public bool DaemonRunning => _currentStatus?.DaemonStatus.IsRunning ?? false;
    public string DaemonStateText => _currentStatus?.DaemonStatus.State ?? "stopped";

    public bool IsMonitoring => _pollingTask != null && !_pollingTask.IsCompleted;
    public string? WorkspacePath => _workspacePath;

    // === Control ===
    public async Task StartAsync(string workspacePath)
    {
        Stop();

        _workspacePath = workspacePath;
        _pollingCts = new CancellationTokenSource();
        _previousCompletedCount = 0;

        // Initial refresh
        await RefreshAsync();

        // Start polling loop
        _pollingTask = PollLoopAsync(_pollingCts.Token);
    }

    public void Stop()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
        _pollingTask = null;
        _workspacePath = null;

        CurrentStatus = null;
        SystemState = ActivitySystemState.Idle;
        StatusReason = null;
    }

    public async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(_workspacePath))
            return;

        try
        {
            var status = await _queueService.GetQueueStatusAsync(_workspacePath, CancellationToken.None);
            UpdateFromStatus(status);
        }
        catch (Exception ex)
        {
            SystemState = ActivitySystemState.Error;
            StatusReason = $"Failed to load queue: {ex.Message}";
        }
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollInterval, cancellationToken);
                await RefreshAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Continue polling even if refresh fails
            }
        }
    }

    private void UpdateFromStatus(QueueStatusSummary status)
    {
        // Track completed jobs for LastActivityTime
        var currentCompleted = status.SucceededCount + status.FailedCount + status.CanceledCount;
        if (currentCompleted > _previousCompletedCount)
        {
            LastActivityTime = DateTime.Now;
        }
        _previousCompletedCount = currentCompleted;

        // Update status first (triggers property notifications)
        CurrentStatus = status;

        // Calculate system state
        var newState = CalculateSystemState(status);
        SystemState = newState;
    }

    private ActivitySystemState CalculateSystemState(QueueStatusSummary status)
    {
        var daemon = status.DaemonStatus;

        // Error: Daemon not running
        if (!daemon.IsRunning)
        {
            StatusReason = "Daemon not running";
            return ActivitySystemState.Error;
        }

        // Stalled: Daemon unhealthy (stale heartbeat)
        if (!daemon.IsHealthy)
        {
            StatusReason = "Daemon heartbeat stale";
            return ActivitySystemState.Stalled;
        }

        // Busy: Jobs are running or queued
        if (status.RunningCount > 0)
        {
            var gpuInfo = ActiveGpuSlots > 0 ? $", {ActiveGpuSlots} GPU" : "";
            StatusReason = $"{status.RunningCount} running{gpuInfo}";
            return ActivitySystemState.Busy;
        }

        if (status.QueuedCount > 0)
        {
            StatusReason = $"{status.QueuedCount} queued";
            return ActivitySystemState.Busy;
        }

        // Idle: Nothing happening
        StatusReason = LastActivityTime.HasValue
            ? $"Last activity: {FormatTimeAgo(LastActivityTime.Value)}"
            : null;
        return ActivitySystemState.Idle;
    }

    private static string FormatTimeAgo(DateTime time)
    {
        var elapsed = DateTime.Now - time;

        if (elapsed.TotalSeconds < 60)
            return "just now";
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes} min ago";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours} hr ago";

        return time.ToString("MMM d");
    }

    // === IDisposable ===
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
