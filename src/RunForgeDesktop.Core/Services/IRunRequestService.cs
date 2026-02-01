using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Result of saving a run request.
/// </summary>
public sealed record RunRequestSaveResult
{
    /// <summary>
    /// Whether the save succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The path where the request was saved.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Validation errors if save failed due to invalid request.
    /// </summary>
    public IReadOnlyList<string>? ValidationErrors { get; init; }

    /// <summary>
    /// Error message if save failed for other reasons.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static RunRequestSaveResult Success(string filePath) =>
        new()
        {
            IsSuccess = true,
            FilePath = filePath
        };

    /// <summary>
    /// Creates a validation failure result.
    /// </summary>
    public static RunRequestSaveResult ValidationFailure(IReadOnlyList<string> errors) =>
        new()
        {
            IsSuccess = false,
            ValidationErrors = errors,
            ErrorMessage = $"Request validation failed: {string.Join(", ", errors)}"
        };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static RunRequestSaveResult Failure(string errorMessage) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Service for loading and saving RunRequest files.
/// Implements atomic save with temp file + rename pattern.
/// </summary>
public interface IRunRequestService
{
    /// <summary>
    /// Loads a run request from a run directory.
    /// </summary>
    /// <param name="runDir">Absolute path to the run directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Load result with the request or error details.</returns>
    Task<ArtifactLoadResult<RunRequest>> LoadAsync(
        string runDir,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a run request to a run directory atomically.
    /// Uses temp file + rename pattern for crash safety.
    /// Validates the request before saving.
    /// </summary>
    /// <param name="runDir">Absolute path to the run directory.</param>
    /// <param name="request">The run request to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Save result with success/error details.</returns>
    Task<RunRequestSaveResult> SaveAsync(
        string runDir,
        RunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a run request synchronously.
    /// Use LoadAsync when possible for better UI responsiveness.
    /// </summary>
    ArtifactLoadResult<RunRequest> Load(string runDir);

    /// <summary>
    /// Saves a run request synchronously with atomic write.
    /// Use SaveAsync when possible for better UI responsiveness.
    /// </summary>
    RunRequestSaveResult Save(string runDir, RunRequest request);
}
