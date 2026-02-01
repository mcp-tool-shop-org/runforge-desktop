using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Result of loading run details.
/// </summary>
public sealed record RunDetailLoadResult
{
    /// <summary>
    /// Whether the load succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The run request (training parameters).
    /// </summary>
    public RunRequest? Request { get; init; }

    /// <summary>
    /// The run result (outcome).
    /// </summary>
    public RunResult? Result { get; init; }

    /// <summary>
    /// The training metrics.
    /// </summary>
    public TrainingMetrics? Metrics { get; init; }

    /// <summary>
    /// Error message if loading failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static RunDetailLoadResult Success(RunRequest? request, RunResult? result, TrainingMetrics? metrics) =>
        new()
        {
            IsSuccess = true,
            Request = request,
            Result = result,
            Metrics = metrics
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static RunDetailLoadResult Failure(string errorMessage) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Service for loading run details from artifacts.
/// </summary>
public interface IRunDetailService
{
    /// <summary>
    /// Loads run details from the run directory.
    /// </summary>
    /// <param name="workspacePath">The workspace root path.</param>
    /// <param name="runDir">The run directory (relative path from workspace).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Load result with available artifacts.</returns>
    Task<RunDetailLoadResult> LoadRunDetailAsync(
        string workspacePath,
        string runDir,
        CancellationToken cancellationToken = default);
}
