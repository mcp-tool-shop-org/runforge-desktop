using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Result of loading the interpretability index.
/// </summary>
public sealed record InterpretabilityLoadResult
{
    /// <summary>
    /// Whether the load succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The interpretability index, if available.
    /// </summary>
    public InterpretabilityIndexV1? Index { get; init; }

    /// <summary>
    /// Error message if loading failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether the index was not found (vs corrupt/error).
    /// </summary>
    public bool NotFound { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static InterpretabilityLoadResult Success(InterpretabilityIndexV1 index) =>
        new()
        {
            IsSuccess = true,
            Index = index
        };

    /// <summary>
    /// Creates a not found result.
    /// </summary>
    public static InterpretabilityLoadResult NotFoundResult() =>
        new()
        {
            IsSuccess = false,
            NotFound = true,
            ErrorMessage = "Interpretability index not present for this run"
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static InterpretabilityLoadResult Failure(string errorMessage) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Service for loading interpretability artifacts.
/// </summary>
public interface IInterpretabilityService
{
    /// <summary>
    /// Loads the interpretability index for a run.
    /// </summary>
    /// <param name="workspacePath">The workspace root path.</param>
    /// <param name="runDir">The run directory (relative path from workspace).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Load result with the index or error.</returns>
    Task<InterpretabilityLoadResult> LoadIndexAsync(
        string workspacePath,
        string runDir,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a specific interpretability artifact.
    /// </summary>
    /// <typeparam name="T">The artifact type.</typeparam>
    /// <param name="workspacePath">The workspace root path.</param>
    /// <param name="runDir">The run directory.</param>
    /// <param name="artifactPath">The artifact path relative to run directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The artifact or null if not found.</returns>
    Task<T?> LoadArtifactAsync<T>(
        string workspacePath,
        string runDir,
        string artifactPath,
        CancellationToken cancellationToken = default) where T : class;
}
