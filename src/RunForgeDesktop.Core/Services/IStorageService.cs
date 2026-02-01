namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Storage usage information for a run.
/// </summary>
public sealed record RunStorageInfo
{
    /// <summary>Run ID.</summary>
    public required string RunId { get; init; }

    /// <summary>Run name (from request.json or run_id).</summary>
    public required string Name { get; init; }

    /// <summary>Relative path to run directory.</summary>
    public required string RunDir { get; init; }

    /// <summary>Total folder size in bytes.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Logs.txt size in bytes.</summary>
    public long LogsBytes { get; init; }

    /// <summary>Artifacts size in bytes (excluding logs.txt).</summary>
    public long ArtifactsBytes { get; init; }

    /// <summary>Last modified timestamp.</summary>
    public DateTime LastModifiedUtc { get; init; }

    /// <summary>Run status (if result.json exists).</summary>
    public string? Status { get; init; }

    /// <summary>Formatted total size for display.</summary>
    public string TotalSizeDisplay => FormatSize(TotalBytes);

    /// <summary>Formatted logs size for display.</summary>
    public string LogsSizeDisplay => FormatSize(LogsBytes);

    /// <summary>Formatted artifacts size for display.</summary>
    public string ArtifactsSizeDisplay => FormatSize(ArtifactsBytes);

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

/// <summary>
/// Overall workspace storage summary.
/// </summary>
public sealed record WorkspaceStorageSummary
{
    /// <summary>Total size of all runs in bytes.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Total logs size across all runs.</summary>
    public long TotalLogsBytes { get; init; }

    /// <summary>Total artifacts size across all runs.</summary>
    public long TotalArtifactsBytes { get; init; }

    /// <summary>Number of runs scanned.</summary>
    public int RunCount { get; init; }

    /// <summary>Top runs by size, largest first.</summary>
    public required IReadOnlyList<RunStorageInfo> TopRunsBySize { get; init; }

    /// <summary>Formatted total size for display.</summary>
    public string TotalSizeDisplay => FormatSize(TotalBytes);

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

/// <summary>
/// Service for calculating and managing workspace storage usage.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Calculates storage usage for all runs in the workspace.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="topN">Number of top runs to include (by size).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Storage summary with top runs.</returns>
    Task<WorkspaceStorageSummary> CalculateStorageAsync(
        string workspacePath,
        int topN = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets storage info for a specific run.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="runDir">Relative run directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Run storage info.</returns>
    Task<RunStorageInfo?> GetRunStorageAsync(
        string workspacePath,
        string runDir,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a run folder completely.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="runDir">Relative run directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully.</returns>
    Task<bool> DeleteRunAsync(
        string workspacePath,
        string runDir,
        CancellationToken cancellationToken = default);
}
