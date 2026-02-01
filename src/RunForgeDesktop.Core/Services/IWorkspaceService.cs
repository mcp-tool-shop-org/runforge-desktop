namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Result of a workspace discovery operation.
/// </summary>
public sealed record WorkspaceDiscoveryResult
{
    /// <summary>
    /// Whether a valid RunForge workspace was found.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// The workspace root path.
    /// </summary>
    public required string WorkspacePath { get; init; }

    /// <summary>
    /// Path to the index file, if found.
    /// </summary>
    public string? IndexPath { get; init; }

    /// <summary>
    /// Discovery method used.
    /// </summary>
    public required WorkspaceDiscoveryMethod Method { get; init; }

    /// <summary>
    /// Error message if discovery failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful discovery result.
    /// </summary>
    public static WorkspaceDiscoveryResult Success(string workspacePath, string? indexPath, WorkspaceDiscoveryMethod method) =>
        new()
        {
            IsValid = true,
            WorkspacePath = workspacePath,
            IndexPath = indexPath,
            Method = method
        };

    /// <summary>
    /// Creates a failed discovery result.
    /// </summary>
    public static WorkspaceDiscoveryResult Failure(string workspacePath, string errorMessage) =>
        new()
        {
            IsValid = false,
            WorkspacePath = workspacePath,
            Method = WorkspaceDiscoveryMethod.None,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// How the workspace was discovered.
/// </summary>
public enum WorkspaceDiscoveryMethod
{
    /// <summary>
    /// No valid workspace found.
    /// </summary>
    None,

    /// <summary>
    /// Found via .ml/outputs/index.json (preferred).
    /// </summary>
    IndexFile,

    /// <summary>
    /// Found via .ml/runs directory convention.
    /// </summary>
    RunsDirectory
}

/// <summary>
/// Service for managing workspace selection and discovery.
/// </summary>
public interface IWorkspaceService
{
    /// <summary>
    /// Gets the currently selected workspace path, or null if none selected.
    /// </summary>
    string? CurrentWorkspacePath { get; }

    /// <summary>
    /// Gets the discovery result for the current workspace.
    /// </summary>
    WorkspaceDiscoveryResult? CurrentDiscoveryResult { get; }

    /// <summary>
    /// Gets whether a valid workspace is currently selected.
    /// </summary>
    bool HasValidWorkspace { get; }

    /// <summary>
    /// Event raised when the workspace changes.
    /// </summary>
    event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;

    /// <summary>
    /// Sets the workspace to the specified path and discovers RunForge artifacts.
    /// Does NOT create any directories or files.
    /// </summary>
    /// <param name="workspacePath">The workspace folder path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovery result.</returns>
    Task<WorkspaceDiscoveryResult> SetWorkspaceAsync(string workspacePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the current workspace selection.
    /// </summary>
    void ClearWorkspace();

    /// <summary>
    /// Saves the workspace path for persistence (e.g., to settings).
    /// </summary>
    Task SaveLastWorkspaceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the last used workspace path from persistence.
    /// </summary>
    /// <returns>The last workspace path, or null if none saved.</returns>
    Task<string?> LoadLastWorkspaceAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Event args for workspace changes.
/// </summary>
public sealed class WorkspaceChangedEventArgs : EventArgs
{
    /// <summary>
    /// The previous workspace path.
    /// </summary>
    public string? PreviousPath { get; init; }

    /// <summary>
    /// The new workspace path.
    /// </summary>
    public string? NewPath { get; init; }

    /// <summary>
    /// The discovery result for the new workspace.
    /// </summary>
    public WorkspaceDiscoveryResult? DiscoveryResult { get; init; }
}
