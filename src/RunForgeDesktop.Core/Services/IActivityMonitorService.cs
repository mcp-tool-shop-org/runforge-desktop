using System.ComponentModel;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// System-wide activity state for the Visual Activity System.
/// </summary>
public enum ActivitySystemState
{
    /// <summary>No jobs running or queued, daemon may or may not be active.</summary>
    Idle,

    /// <summary>Jobs are currently executing.</summary>
    Busy,

    /// <summary>Daemon heartbeat stale or jobs appear stuck.</summary>
    Stalled,

    /// <summary>Daemon not running or error state.</summary>
    Error
}

/// <summary>
/// Service that monitors execution queue state and provides real-time activity status.
/// Polls the queue service and exposes observable properties for UI binding.
/// </summary>
public interface IActivityMonitorService : INotifyPropertyChanged, IDisposable
{
    /// <summary>Current queue status snapshot. Null if no workspace loaded.</summary>
    QueueStatusSummary? CurrentStatus { get; }

    /// <summary>High-level system state for UI display.</summary>
    ActivitySystemState SystemState { get; }

    /// <summary>When the last job completed. Null if never or unknown.</summary>
    DateTime? LastActivityTime { get; }

    /// <summary>Reason for stalled/error state, if applicable.</summary>
    string? StatusReason { get; }

    // === CPU Slot Info ===
    /// <summary>Number of CPU jobs currently running.</summary>
    int ActiveCpuSlots { get; }

    /// <summary>Maximum parallel CPU jobs allowed.</summary>
    int TotalCpuSlots { get; }

    // === GPU Slot Info ===
    /// <summary>Number of GPU jobs currently running.</summary>
    int ActiveGpuSlots { get; }

    /// <summary>Maximum parallel GPU jobs allowed.</summary>
    int TotalGpuSlots { get; }

    /// <summary>Number of jobs waiting for GPU slot.</summary>
    int QueuedGpuCount { get; }

    /// <summary>True if GPU slots are configured (GpuSlots > 0).</summary>
    bool HasGpuSlots { get; }

    // === Queue Info ===
    /// <summary>Total jobs waiting in queue (CPU + GPU).</summary>
    int QueuedCount { get; }

    // === Daemon Info ===
    /// <summary>True if daemon is running and healthy (recent heartbeat).</summary>
    bool DaemonHealthy { get; }

    /// <summary>True if daemon is running (regardless of health).</summary>
    bool DaemonRunning { get; }

    /// <summary>Display text for daemon state (e.g., "running", "stopped").</summary>
    string DaemonStateText { get; }

    // === Control ===
    /// <summary>Start monitoring the specified workspace.</summary>
    Task StartAsync(string workspacePath);

    /// <summary>Stop monitoring and clear state.</summary>
    void Stop();

    /// <summary>True if currently monitoring a workspace.</summary>
    bool IsMonitoring { get; }

    /// <summary>Path of the workspace being monitored.</summary>
    string? WorkspacePath { get; }

    /// <summary>Force an immediate refresh of the status.</summary>
    Task RefreshAsync();
}
