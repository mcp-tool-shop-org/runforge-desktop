using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Result of attempting to start a training run.
/// </summary>
public sealed class StartRunResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static StartRunResult Ok() => new() { Success = true };
    public static StartRunResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

/// <summary>
/// Service for spawning and managing training runner processes.
/// </summary>
public interface IRunnerService
{
    /// <summary>
    /// Create a new run folder and manifest.
    /// </summary>
    Task<RunManifest> CreateRunAsync(string name, string preset, string datasetPath, DeviceType device, TrainingConfig? config = null);

    /// <summary>
    /// Start the training process for a run.
    /// Returns a result with success/failure and error message.
    /// </summary>
    Task<StartRunResult> StartRunAsync(string runId);

    /// <summary>
    /// Cancel a running process.
    /// </summary>
    Task<bool> CancelRunAsync(string runId);

    /// <summary>
    /// Check if a run's process is still alive.
    /// </summary>
    bool IsRunning(string runId);

    /// <summary>
    /// Get all runs in the workspace.
    /// </summary>
    Task<List<RunManifest>> GetAllRunsAsync();

    /// <summary>
    /// Get a single run manifest.
    /// </summary>
    Task<RunManifest?> GetRunAsync(string runId);

    /// <summary>
    /// Tail the stdout log file.
    /// </summary>
    Task<string[]> TailLogsAsync(string runId, int lines = 100);

    /// <summary>
    /// Read metrics from metrics.jsonl.
    /// </summary>
    Task<List<MetricsEntry>> GetMetricsAsync(string runId);

    /// <summary>
    /// Incremental tail for metrics - only reads new data since last call.
    /// Returns new entries and updated byte offset.
    /// </summary>
    Task<(List<MetricsEntry> NewEntries, long NewOffset)> TailMetricsAsync(string runId, long fromOffset);

    /// <summary>
    /// Incremental tail for logs - only reads new data since last call.
    /// Returns new lines and updated byte offset.
    /// </summary>
    Task<(string[] NewLines, long NewOffset)> TailLogsIncrementalAsync(string runId, long fromOffset);

    /// <summary>
    /// Check for orphaned runs (marked Running but process dead) and mark them as Failed.
    /// Call on app startup.
    /// </summary>
    Task RecoverOrphanedRunsAsync();

    /// <summary>
    /// Check if there's already a run in progress.
    /// </summary>
    Task<bool> HasActiveRunAsync();
}
