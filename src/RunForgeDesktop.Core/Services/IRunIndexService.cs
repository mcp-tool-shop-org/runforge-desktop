using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Result of loading the run index.
/// </summary>
public sealed record RunIndexLoadResult
{
    /// <summary>
    /// Whether the index was loaded successfully.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The loaded runs, sorted newest first.
    /// Empty if loading failed.
    /// </summary>
    public required IReadOnlyList<RunIndexEntry> Runs { get; init; }

    /// <summary>
    /// Error message if loading failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Path to the index file that was loaded.
    /// </summary>
    public string? IndexPath { get; init; }

    /// <summary>
    /// Whether this result came from cache.
    /// </summary>
    public required bool FromCache { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static RunIndexLoadResult Success(IReadOnlyList<RunIndexEntry> runs, string indexPath, bool fromCache) =>
        new()
        {
            IsSuccess = true,
            Runs = runs,
            IndexPath = indexPath,
            FromCache = fromCache
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static RunIndexLoadResult Failure(string errorMessage) =>
        new()
        {
            IsSuccess = false,
            Runs = [],
            ErrorMessage = errorMessage,
            FromCache = false
        };
}

/// <summary>
/// Service for loading and caching the run index.
/// </summary>
public interface IRunIndexService
{
    /// <summary>
    /// Gets whether an index is currently loaded.
    /// </summary>
    bool HasLoadedIndex { get; }

    /// <summary>
    /// Gets the current loaded runs (may be empty).
    /// </summary>
    IReadOnlyList<RunIndexEntry> CurrentRuns { get; }

    /// <summary>
    /// Event raised when the run index changes.
    /// </summary>
    event EventHandler<RunIndexChangedEventArgs>? IndexChanged;

    /// <summary>
    /// Loads the run index from the workspace.
    /// Uses cache if available unless forceRefresh is true.
    /// </summary>
    /// <param name="workspacePath">The workspace root path.</param>
    /// <param name="indexPath">Optional explicit index path (uses default if null).</param>
    /// <param name="forceRefresh">Force reload from disk, ignoring cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Load result with runs sorted newest first.</returns>
    Task<RunIndexLoadResult> LoadIndexAsync(
        string workspacePath,
        string? indexPath = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the cache for the specified workspace.
    /// </summary>
    void ClearCache(string? workspacePath = null);

    /// <summary>
    /// Filters the current runs by the specified criteria.
    /// </summary>
    /// <param name="runIdSubstring">Filter by run ID substring (case-insensitive).</param>
    /// <param name="modelFamily">Filter by model family (exact match).</param>
    /// <param name="statusFilter">Filter by status.</param>
    /// <returns>Filtered runs.</returns>
    IReadOnlyList<RunIndexEntry> FilterRuns(
        string? runIdSubstring = null,
        string? modelFamily = null,
        RunStatusFilter statusFilter = RunStatusFilter.All);

    /// <summary>
    /// Gets a run by ID from the current loaded runs.
    /// </summary>
    /// <param name="runId">The run ID to find.</param>
    /// <returns>The run entry, or null if not found.</returns>
    RunIndexEntry? GetRunById(string runId);
}

/// <summary>
/// Status filter options for runs.
/// </summary>
public enum RunStatusFilter
{
    /// <summary>
    /// Show all runs.
    /// </summary>
    All,

    /// <summary>
    /// Show only succeeded runs.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Show only failed runs.
    /// </summary>
    Failed
}

/// <summary>
/// Event args for run index changes.
/// </summary>
public sealed class RunIndexChangedEventArgs : EventArgs
{
    /// <summary>
    /// The new runs list.
    /// </summary>
    public required IReadOnlyList<RunIndexEntry> Runs { get; init; }

    /// <summary>
    /// The workspace path.
    /// </summary>
    public required string WorkspacePath { get; init; }
}
