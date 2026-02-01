namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Constants for RunForge workspace paths.
/// </summary>
public static class WorkspacePaths
{
    /// <summary>
    /// Root directory for ML artifacts.
    /// </summary>
    public const string MlRoot = ".ml";

    /// <summary>
    /// Outputs directory within ML root.
    /// </summary>
    public const string OutputsDir = ".ml/outputs";

    /// <summary>
    /// Runs directory within ML root.
    /// </summary>
    public const string RunsDir = ".ml/runs";

    /// <summary>
    /// Index file path within outputs directory.
    /// </summary>
    public const string IndexFile = ".ml/outputs/index.json";
}

/// <summary>
/// Implementation of workspace selection and discovery.
/// </summary>
public sealed class WorkspaceService : IWorkspaceService
{
    private readonly string _settingsPath;
    private string? _currentWorkspacePath;
    private WorkspaceDiscoveryResult? _currentDiscoveryResult;

    /// <summary>
    /// Creates a new WorkspaceService with the specified settings directory.
    /// </summary>
    /// <param name="settingsDirectory">Directory to store settings (e.g., LocalApplicationData).</param>
    public WorkspaceService(string? settingsDirectory = null)
    {
        var appData = settingsDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDir = Path.Combine(appData, "RunForgeDesktop");
        Directory.CreateDirectory(appDir);
        _settingsPath = Path.Combine(appDir, "last_workspace.txt");
    }

    /// <inheritdoc />
    public string? CurrentWorkspacePath => _currentWorkspacePath;

    /// <inheritdoc />
    public WorkspaceDiscoveryResult? CurrentDiscoveryResult => _currentDiscoveryResult;

    /// <inheritdoc />
    public bool HasValidWorkspace => _currentDiscoveryResult?.IsValid == true;

    /// <inheritdoc />
    public event EventHandler<WorkspaceChangedEventArgs>? WorkspaceChanged;

    /// <inheritdoc />
    public Task<WorkspaceDiscoveryResult> SetWorkspaceAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        var previousPath = _currentWorkspacePath;

        // Validate the path exists
        if (!Directory.Exists(workspacePath))
        {
            var result = WorkspaceDiscoveryResult.Failure(workspacePath, "Directory does not exist");
            UpdateWorkspace(workspacePath, result, previousPath);
            return Task.FromResult(result);
        }

        // Try to discover RunForge artifacts
        var discoveryResult = DiscoverRunForge(workspacePath);
        UpdateWorkspace(workspacePath, discoveryResult, previousPath);

        return Task.FromResult(discoveryResult);
    }

    /// <inheritdoc />
    public void ClearWorkspace()
    {
        var previousPath = _currentWorkspacePath;
        _currentWorkspacePath = null;
        _currentDiscoveryResult = null;

        WorkspaceChanged?.Invoke(this, new WorkspaceChangedEventArgs
        {
            PreviousPath = previousPath,
            NewPath = null,
            DiscoveryResult = null
        });
    }

    /// <inheritdoc />
    public async Task SaveLastWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        if (_currentWorkspacePath is not null)
        {
            await File.WriteAllTextAsync(_settingsPath, _currentWorkspacePath, cancellationToken);
        }
        else if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }

    /// <inheritdoc />
    public async Task<string?> LoadLastWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        var path = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
        return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
    }

    private void UpdateWorkspace(string workspacePath, WorkspaceDiscoveryResult result, string? previousPath)
    {
        _currentWorkspacePath = workspacePath;
        _currentDiscoveryResult = result;

        WorkspaceChanged?.Invoke(this, new WorkspaceChangedEventArgs
        {
            PreviousPath = previousPath,
            NewPath = workspacePath,
            DiscoveryResult = result
        });
    }

    /// <summary>
    /// Discovers RunForge artifacts in the workspace.
    /// Does NOT create any directories or files.
    /// </summary>
    private static WorkspaceDiscoveryResult DiscoverRunForge(string workspacePath)
    {
        // Normalize path
        workspacePath = Path.GetFullPath(workspacePath);

        // 1. Preferred: Check for .ml/outputs/index.json
        var indexPath = Path.Combine(workspacePath, WorkspacePaths.IndexFile.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(indexPath))
        {
            // Verify it's readable JSON (basic validation)
            try
            {
                var content = File.ReadAllText(indexPath);
                if (content.TrimStart().StartsWith('[') || content.TrimStart().StartsWith('{'))
                {
                    return WorkspaceDiscoveryResult.Success(workspacePath, indexPath, WorkspaceDiscoveryMethod.IndexFile);
                }
                else
                {
                    return WorkspaceDiscoveryResult.Failure(workspacePath,
                        "Index file exists but does not appear to be valid JSON");
                }
            }
            catch (Exception ex)
            {
                return WorkspaceDiscoveryResult.Failure(workspacePath,
                    $"Index file exists but could not be read: {ex.Message}");
            }
        }

        // 2. Fallback: Check for .ml/runs directory with run subdirectories
        var runsDir = Path.Combine(workspacePath, WorkspacePaths.RunsDir.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(runsDir))
        {
            var runDirs = Directory.GetDirectories(runsDir);
            if (runDirs.Length > 0)
            {
                // Found runs directory with contents - valid but no index
                return WorkspaceDiscoveryResult.Success(workspacePath, null, WorkspaceDiscoveryMethod.RunsDirectory);
            }
        }

        // 3. Check if .ml directory exists but is empty/incomplete
        var mlRoot = Path.Combine(workspacePath, WorkspacePaths.MlRoot);
        if (Directory.Exists(mlRoot))
        {
            return WorkspaceDiscoveryResult.Failure(workspacePath,
                "Found .ml directory but no index file or runs. This workspace may be empty or incomplete.");
        }

        // 4. No RunForge artifacts found
        return WorkspaceDiscoveryResult.Failure(workspacePath,
            "No RunForge workspace found. Expected .ml/outputs/index.json or .ml/runs/ directory.");
    }
}
