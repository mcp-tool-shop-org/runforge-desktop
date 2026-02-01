namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Service for executing runs via the runforge-cli.
/// </summary>
public interface ICliExecutionService
{
    /// <summary>
    /// Gets whether the CLI is available (Python + runforge-cli installed).
    /// </summary>
    bool IsCliAvailable { get; }

    /// <summary>
    /// Gets the reason why CLI is not available (if applicable).
    /// </summary>
    string? CliUnavailableReason { get; }

    /// <summary>
    /// Checks if the CLI is available and updates the status.
    /// </summary>
    Task<bool> CheckCliAvailabilityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a run using the CLI.
    /// </summary>
    /// <param name="workspacePath">Path to the workspace root.</param>
    /// <param name="runDir">Workspace-relative run directory.</param>
    /// <param name="onOutput">Callback for streaming output lines.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result.</returns>
    Task<CliExecutionResult> ExecuteRunAsync(
        string workspacePath,
        string runDir,
        Action<string>? onOutput = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently running execution, if any.
    /// </summary>
    CliExecutionState? CurrentExecution { get; }

    /// <summary>
    /// Cancels the current execution if one is running.
    /// </summary>
    void CancelCurrentExecution();
}

/// <summary>
/// Result of a CLI execution.
/// </summary>
public sealed record CliExecutionResult
{
    /// <summary>
    /// Exit code from the CLI (0=success, 1=failed, 2=invalid, 3=missing, 4=internal).
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Whether the execution succeeded (exit code 0).
    /// </summary>
    public bool Succeeded => ExitCode == 0;

    /// <summary>
    /// Friendly status string based on exit code.
    /// </summary>
    public string Status => ExitCode switch
    {
        0 => "succeeded",
        1 => "failed",
        2 => "invalid_request",
        3 => "missing_files",
        4 => "internal_error",
        -1 => "cancelled",
        _ => "unknown"
    };

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
}

/// <summary>
/// State of a running CLI execution.
/// </summary>
public sealed class CliExecutionState
{
    /// <summary>
    /// Run directory being executed.
    /// </summary>
    public required string RunDir { get; init; }

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
}
