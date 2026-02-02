using System.Text.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Service for creating and monitoring sweeps.
/// </summary>
public interface ISweepService
{
    /// <summary>
    /// Creates a sweep plan from the given parameters.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="groupName">Human-readable group name.</param>
    /// <param name="notes">Optional notes/description.</param>
    /// <param name="baseRequest">Base request as JSON element.</param>
    /// <param name="parameters">Grid parameters to sweep over.</param>
    /// <param name="maxParallel">Maximum concurrent runs.</param>
    /// <returns>The created sweep plan.</returns>
    SweepPlan CreatePlan(
        string workspacePath,
        string groupName,
        string? notes,
        JsonElement baseRequest,
        IReadOnlyList<SweepParameterConfig> parameters,
        int maxParallel = 2);

    /// <summary>
    /// Saves a sweep plan to a file.
    /// </summary>
    /// <param name="plan">The plan to save.</param>
    /// <param name="outputPath">Path to save the plan.</param>
    Task SavePlanAsync(SweepPlan plan, string outputPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a sweep plan from a file.
    /// </summary>
    /// <param name="planPath">Path to the plan file.</param>
    Task<SweepPlan> LoadPlanAsync(string planPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a sweep using the CLI.
    /// </summary>
    /// <param name="planPath">Path to the sweep plan.</param>
    /// <param name="onOutput">Callback for output lines.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result.</returns>
    Task<SweepExecutionResult> ExecuteSweepAsync(
        string planPath,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all groups in the workspace.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    Task<IReadOnlyList<RunGroup>> ListGroupsAsync(string workspacePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a specific group by ID.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="groupId">Group identifier.</param>
    Task<RunGroup?> LoadGroupAsync(string workspacePath, string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current sweep execution state, if any.
    /// </summary>
    SweepExecutionState? CurrentExecution { get; }

    /// <summary>
    /// Cancels the current sweep execution.
    /// </summary>
    void CancelCurrentExecution();

    /// <summary>
    /// Calculates the total number of runs for a grid sweep.
    /// </summary>
    /// <param name="parameters">Grid parameters.</param>
    /// <returns>Total run count.</returns>
    int CalculateGridSize(IReadOnlyList<SweepParameterConfig> parameters);
}

/// <summary>
/// Configuration for a sweep parameter.
/// </summary>
public sealed record SweepParameterConfig
{
    /// <summary>
    /// Dot-path to the parameter (e.g., "model.hyperparameters.n_estimators").
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Values to sweep over.
    /// </summary>
    public required IReadOnlyList<object?> Values { get; init; }
}

/// <summary>
/// Result of a sweep execution.
/// </summary>
public sealed record SweepExecutionResult
{
    /// <summary>
    /// Exit code from the CLI.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Whether the sweep completed successfully.
    /// </summary>
    public bool Succeeded => ExitCode == 0;

    /// <summary>
    /// Group ID of the executed sweep.
    /// </summary>
    public string? GroupId { get; init; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Duration of execution in milliseconds.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Whether execution was cancelled.
    /// </summary>
    public bool WasCancelled { get; init; }

    /// <summary>
    /// Friendly status string.
    /// </summary>
    public string Status => ExitCode switch
    {
        0 => "completed",
        1 => "failed",
        5 => "canceled",
        6 => "invalid_plan",
        -1 => "cancelled",
        _ => "unknown"
    };
}

/// <summary>
/// State of a running sweep execution.
/// </summary>
public sealed class SweepExecutionState
{
    /// <summary>
    /// Path to the sweep plan.
    /// </summary>
    public required string PlanPath { get; init; }

    /// <summary>
    /// Group ID being executed (if known).
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Start time of execution.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// Process ID of the CLI process.
    /// </summary>
    public int? ProcessId { get; set; }

    /// <summary>
    /// Cancellation token source for cancellation.
    /// </summary>
    public CancellationTokenSource? CancellationSource { get; set; }

    /// <summary>
    /// Current progress (runs completed).
    /// </summary>
    public int RunsCompleted { get; set; }

    /// <summary>
    /// Total runs in the sweep.
    /// </summary>
    public int TotalRuns { get; set; }
}
