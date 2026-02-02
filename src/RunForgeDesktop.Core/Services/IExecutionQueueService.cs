using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Service for managing the execution queue and daemon.
/// </summary>
public interface IExecutionQueueService
{
    /// <summary>
    /// Loads the current queue state.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ExecutionQueue> LoadQueueAsync(string workspacePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the current daemon status.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DaemonStatus> LoadDaemonStatusAsync(string workspacePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the daemon process.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="maxParallel">Maximum concurrent jobs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if started successfully.</returns>
    Task<bool> StartDaemonAsync(string workspacePath, int maxParallel = 2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the daemon process gracefully.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if stopped successfully.</returns>
    Task<bool> StopDaemonAsync(string workspacePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a single run.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="runId">Run ID to enqueue.</param>
    /// <param name="groupId">Optional group ID.</param>
    /// <param name="priority">Priority (higher = first).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> EnqueueRunAsync(
        string workspacePath,
        string runId,
        string? groupId = null,
        int priority = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues all runs from a sweep plan.
    /// </summary>
    /// <param name="planPath">Path to sweep_plan.json.</param>
    /// <param name="workspacePath">Optional workspace override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Group ID of the created sweep, or null on failure.</returns>
    Task<string?> EnqueueSweepAsync(
        string planPath,
        string? workspacePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a group (stops new jobs from starting).
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="groupId">Group ID to pause.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> PauseGroupAsync(string workspacePath, string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused group.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="groupId">Group ID to resume.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> ResumeGroupAsync(string workspacePath, string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-enqueues failed runs in a group.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="groupId">Group ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of runs re-enqueued.</returns>
    Task<int> RetryFailedAsync(string workspacePath, string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels all queued runs in a group.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="groupId">Group ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of runs canceled.</returns>
    Task<int> CancelGroupAsync(string workspacePath, string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the queue status summary.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<QueueStatusSummary> GetQueueStatusAsync(string workspacePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Summary of queue status.
/// </summary>
public sealed record QueueStatusSummary
{
    /// <summary>Maximum parallel jobs.</summary>
    public int MaxParallel { get; init; }

    /// <summary>Total jobs in queue.</summary>
    public int TotalJobs { get; init; }

    /// <summary>Jobs waiting to run.</summary>
    public int QueuedCount { get; init; }

    /// <summary>Jobs currently running.</summary>
    public int RunningCount { get; init; }

    /// <summary>Jobs that succeeded.</summary>
    public int SucceededCount { get; init; }

    /// <summary>Jobs that failed.</summary>
    public int FailedCount { get; init; }

    /// <summary>Jobs that were canceled.</summary>
    public int CanceledCount { get; init; }

    /// <summary>Daemon status.</summary>
    public required DaemonStatus DaemonStatus { get; init; }

    /// <summary>Paused group IDs.</summary>
    public IReadOnlyList<string> PausedGroups { get; init; } = [];
}
