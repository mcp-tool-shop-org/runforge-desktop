using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of interpretability artifact loading.
/// </summary>
public sealed class InterpretabilityService : IInterpretabilityService
{
    private const string InterpretabilityIndexFileName = "interpretability.index.v1.json";
    private const string ArtifactsDir = "artifacts";

    /// <inheritdoc />
    public async Task<InterpretabilityLoadResult> LoadIndexAsync(
        string workspacePath,
        string runDir,
        CancellationToken cancellationToken = default)
    {
        workspacePath = Path.GetFullPath(workspacePath);

        // Normalize run directory path
        var normalizedRunDir = runDir.Replace('/', Path.DirectorySeparatorChar);
        var fullRunDir = Path.Combine(workspacePath, normalizedRunDir);

        if (!Directory.Exists(fullRunDir))
        {
            return InterpretabilityLoadResult.Failure($"Run directory not found: {runDir}");
        }

        // Try to load from artifacts directory first
        var indexPath = Path.Combine(fullRunDir, ArtifactsDir, InterpretabilityIndexFileName);
        if (!File.Exists(indexPath))
        {
            // Also try root of run directory
            indexPath = Path.Combine(fullRunDir, InterpretabilityIndexFileName);
        }

        if (!File.Exists(indexPath))
        {
            return InterpretabilityLoadResult.NotFoundResult();
        }

        var loadResult = await ArtifactLoader.LoadAsync<InterpretabilityIndexV1>(indexPath, cancellationToken);

        if (!loadResult.IsSuccess || loadResult.Value is null)
        {
            var errorMsg = loadResult.Error?.Message ?? "Unknown error loading index";
            if (loadResult.Error?.InnerDetails is not null)
            {
                errorMsg += $": {loadResult.Error.InnerDetails}";
            }
            return InterpretabilityLoadResult.Failure(errorMsg);
        }

        return InterpretabilityLoadResult.Success(loadResult.Value);
    }

    /// <inheritdoc />
    public async Task<T?> LoadArtifactAsync<T>(
        string workspacePath,
        string runDir,
        string artifactPath,
        CancellationToken cancellationToken = default) where T : class
    {
        workspacePath = Path.GetFullPath(workspacePath);

        // Normalize paths
        var normalizedRunDir = runDir.Replace('/', Path.DirectorySeparatorChar);
        var normalizedArtifactPath = artifactPath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(workspacePath, normalizedRunDir, normalizedArtifactPath);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        var loadResult = await ArtifactLoader.LoadAsync<T>(fullPath, cancellationToken);
        return loadResult.IsSuccess ? loadResult.Value : null;
    }
}
