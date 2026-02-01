namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Status of the log file monitoring.
/// </summary>
public enum LogStatus
{
    /// <summary>No logs file exists.</summary>
    NoLogs,

    /// <summary>Receiving logs (updated recently).</summary>
    Receiving,

    /// <summary>Logs are stale (no updates for threshold period).</summary>
    Stale,

    /// <summary>Run completed successfully.</summary>
    Completed,

    /// <summary>Run failed.</summary>
    Failed
}

/// <summary>
/// Reason for file state change detection.
/// </summary>
public enum FileChangeReason
{
    /// <summary>No change detected.</summary>
    None,

    /// <summary>File was truncated (size decreased).</summary>
    Truncated,

    /// <summary>File was replaced (different creation time or inode-like change).</summary>
    Replaced,

    /// <summary>File was deleted.</summary>
    Deleted
}

/// <summary>
/// Snapshot of log file state at a point in time.
/// </summary>
public sealed record LogSnapshot
{
    /// <summary>Current status.</summary>
    public required LogStatus Status { get; init; }

    /// <summary>Time since last log update.</summary>
    public required TimeSpan TimeSinceLastUpdate { get; init; }

    /// <summary>Lines added since last snapshot.</summary>
    public required int LinesAddedSinceLastSnapshot { get; init; }

    /// <summary>Total line count in file.</summary>
    public required long TotalLineCount { get; init; }

    /// <summary>File size in bytes.</summary>
    public required long FileSizeBytes { get; init; }

    /// <summary>Last write time (UTC).</summary>
    public required DateTime LastWriteTimeUtc { get; init; }

    /// <summary>File creation time (UTC) for detecting replacement.</summary>
    public DateTime CreationTimeUtc { get; init; }

    /// <summary>Whether the file was truncated or replaced since last check.</summary>
    public FileChangeReason FileChange { get; init; } = FileChangeReason.None;

    /// <summary>Recommended polling interval based on current state.</summary>
    public TimeSpan RecommendedPollingInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Human-readable status message.</summary>
    public string StatusMessage => Status switch
    {
        LogStatus.NoLogs => "No logs available",
        LogStatus.Receiving => FormatReceiving(),
        LogStatus.Stale => $"No new logs for {FormatDuration(TimeSinceLastUpdate)}",
        LogStatus.Completed => "Run completed",
        LogStatus.Failed => "Run failed",
        _ => "Unknown"
    };

    /// <summary>Status indicator emoji/icon.</summary>
    public string StatusIndicator => Status switch
    {
        LogStatus.NoLogs => "○",
        LogStatus.Receiving => "●",
        LogStatus.Stale => "◐",
        LogStatus.Completed => "✓",
        LogStatus.Failed => "✗",
        _ => "?"
    };

    private string FormatReceiving()
    {
        if (TimeSinceLastUpdate.TotalSeconds < 1)
            return "Receiving logs";
        return $"Last update {FormatDuration(TimeSinceLastUpdate)} ago";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1) return "<1s";
        if (duration.TotalSeconds < 60) return $"{duration.TotalSeconds:F1}s";
        if (duration.TotalMinutes < 60) return $"{duration.TotalMinutes:F0}m {duration.Seconds}s";
        return $"{duration.TotalHours:F0}h {duration.Minutes}m";
    }
}

/// <summary>
/// State tracked between polling cycles for robust file monitoring.
/// </summary>
public sealed class LogMonitorState
{
    /// <summary>Last known byte offset for delta reads.</summary>
    public long LastByteOffset { get; set; }

    /// <summary>Last known file size.</summary>
    public long LastFileSize { get; set; }

    /// <summary>Last known file creation time (for detecting replacement).</summary>
    public DateTime LastCreationTimeUtc { get; set; }

    /// <summary>Last known file write time.</summary>
    public DateTime LastWriteTimeUtc { get; set; }

    /// <summary>Current polling interval (adaptive).</summary>
    public TimeSpan CurrentPollingInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Time when logs last had activity.</summary>
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Resets state when file is detected as truncated or replaced.</summary>
    public void Reset()
    {
        LastByteOffset = 0;
        LastFileSize = 0;
        LastCreationTimeUtc = DateTime.MinValue;
        LastWriteTimeUtc = DateTime.MinValue;
        CurrentPollingInterval = TimeSpan.FromMilliseconds(500);
    }
}

/// <summary>
/// Service for monitoring live log files during training.
/// Handles truncation, replacement, and adaptive polling.
/// </summary>
public interface ILiveLogService
{
    /// <summary>
    /// Maximum bytes to read per polling tick to prevent UI hitching.
    /// </summary>
    int MaxBytesPerTick { get; set; }

    /// <summary>
    /// Threshold for switching to slow polling when stale.
    /// </summary>
    TimeSpan SlowPollingThreshold { get; set; }

    /// <summary>
    /// Slow polling interval when logs are stale.
    /// </summary>
    TimeSpan SlowPollingInterval { get; set; }

    /// <summary>
    /// Fast polling interval when logs are active.
    /// </summary>
    TimeSpan FastPollingInterval { get; set; }

    /// <summary>
    /// Gets a snapshot of the current log file state with change detection.
    /// </summary>
    /// <param name="logFilePath">Full path to logs.txt.</param>
    /// <param name="state">Monitor state for tracking changes between calls.</param>
    /// <param name="runStatus">Run status from index (succeeded/failed/null for in-progress).</param>
    /// <param name="staleThreshold">Threshold for considering logs stale.</param>
    /// <returns>Current log snapshot with file change detection.</returns>
    LogSnapshot GetSnapshot(
        string logFilePath,
        LogMonitorState state,
        string? runStatus,
        TimeSpan staleThreshold);

    /// <summary>
    /// Gets a snapshot of the current log file state (simple version for compatibility).
    /// </summary>
    LogSnapshot GetSnapshot(
        string logFilePath,
        long lastKnownSize,
        string? runStatus,
        TimeSpan staleThreshold);

    /// <summary>
    /// Reads new lines from the log file since the last position.
    /// Handles truncation by resetting offset automatically.
    /// </summary>
    /// <param name="logFilePath">Full path to logs.txt.</param>
    /// <param name="state">Monitor state (will be updated with new offset).</param>
    /// <param name="maxLines">Maximum lines to read.</param>
    /// <returns>New lines, whether a reset occurred, and any read errors.</returns>
    Task<LogDeltaResult> ReadDeltaAsync(
        string logFilePath,
        LogMonitorState state,
        int maxLines = 500);

    /// <summary>
    /// Reads new lines from the log file since the last position (simple version).
    /// </summary>
    Task<(IReadOnlyList<string> Lines, long NewByteOffset)> ReadDeltaAsync(
        string logFilePath,
        long fromByteOffset,
        int maxLines = 500);

    /// <summary>
    /// Reads the last N lines from the log file.
    /// </summary>
    /// <param name="logFilePath">Full path to logs.txt.</param>
    /// <param name="lineCount">Number of lines to read from the end.</param>
    /// <returns>Lines from the end of the file and total byte count.</returns>
    Task<(IReadOnlyList<string> Lines, long TotalBytes)> ReadTailAsync(
        string logFilePath,
        int lineCount = 200);
}

/// <summary>
/// Result of reading log delta with robustness information.
/// </summary>
public sealed record LogDeltaResult
{
    /// <summary>Lines read (may be empty).</summary>
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>Whether the file was reset (truncated/replaced) and offset was reset.</summary>
    public bool WasReset { get; init; }

    /// <summary>Reason for reset if applicable.</summary>
    public FileChangeReason ResetReason { get; init; } = FileChangeReason.None;

    /// <summary>Any error that occurred during read.</summary>
    public string? Error { get; init; }

    /// <summary>Whether read was capped due to MaxBytesPerTick limit.</summary>
    public bool WasCapped { get; init; }

    /// <summary>Bytes remaining to read (if capped).</summary>
    public long BytesRemaining { get; init; }
}
