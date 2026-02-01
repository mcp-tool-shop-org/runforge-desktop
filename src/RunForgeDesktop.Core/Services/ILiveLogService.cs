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
/// Service for monitoring live log files during training.
/// </summary>
public interface ILiveLogService
{
    /// <summary>
    /// Gets a snapshot of the current log file state.
    /// </summary>
    /// <param name="logFilePath">Full path to logs.txt.</param>
    /// <param name="lastKnownSize">Last known file size (for delta calculation).</param>
    /// <param name="runStatus">Run status from index (succeeded/failed/null for in-progress).</param>
    /// <param name="staleThreshold">Threshold for considering logs stale.</param>
    /// <returns>Current log snapshot.</returns>
    LogSnapshot GetSnapshot(
        string logFilePath,
        long lastKnownSize,
        string? runStatus,
        TimeSpan staleThreshold);

    /// <summary>
    /// Reads new lines from the log file since the last position.
    /// </summary>
    /// <param name="logFilePath">Full path to logs.txt.</param>
    /// <param name="fromByteOffset">Byte offset to start reading from.</param>
    /// <param name="maxLines">Maximum lines to read.</param>
    /// <returns>New lines and the new byte offset.</returns>
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
