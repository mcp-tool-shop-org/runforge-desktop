using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Service for creating and managing run requests.
/// </summary>
public interface IRunCreationService
{
    /// <summary>
    /// Creates a new run by cloning an existing run's request.
    /// </summary>
    /// <param name="workspacePath">Path to the workspace root.</param>
    /// <param name="sourceRunDir">Workspace-relative path to the source run.</param>
    /// <param name="name">Optional name for the new run.</param>
    /// <returns>The new run directory (workspace-relative).</returns>
    Task<string> CloneForRerunAsync(
        string workspacePath,
        string sourceRunDir,
        string? name = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new run with the given request.
    /// </summary>
    /// <param name="workspacePath">Path to the workspace root.</param>
    /// <param name="request">The run request.</param>
    /// <returns>The new run directory (workspace-relative).</returns>
    Task<string> CreateRunAsync(
        string workspacePath,
        RunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing run's request.json.
    /// </summary>
    /// <param name="workspacePath">Path to the workspace root.</param>
    /// <param name="runDir">Workspace-relative path to the run.</param>
    /// <param name="request">The updated request.</param>
    Task UpdateRequestAsync(
        string workspacePath,
        string runDir,
        RunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a unique run ID.
    /// </summary>
    /// <param name="name">Optional name to include in the ID.</param>
    /// <returns>Run ID in format: YYYYMMDD-HHMMSS-slug-rand4</returns>
    string GenerateRunId(string? name = null);
}
