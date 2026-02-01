using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of run detail loading.
/// </summary>
public sealed class RunDetailService : IRunDetailService
{
    /// <inheritdoc />
    public async Task<RunDetailLoadResult> LoadRunDetailAsync(
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
            return RunDetailLoadResult.Failure($"Run directory not found: {runDir}");
        }

        // Load artifacts (any may be missing, that's OK)
        var requestTask = LoadArtifactAsync<RunRequest>(fullRunDir, "request.json", cancellationToken);
        var resultTask = LoadArtifactAsync<RunResult>(fullRunDir, "result.json", cancellationToken);
        var metricsTask = LoadArtifactAsync<TrainingMetrics>(fullRunDir, "metrics.json", cancellationToken);

        await Task.WhenAll(requestTask, resultTask, metricsTask);

        var request = await requestTask;
        var result = await resultTask;
        var metrics = await metricsTask;

        // At least one artifact should exist for this to be considered successful
        if (request is null && result is null && metrics is null)
        {
            return RunDetailLoadResult.Failure("No run artifacts found (request.json, result.json, or metrics.json)");
        }

        return RunDetailLoadResult.Success(request, result, metrics);
    }

    private static async Task<T?> LoadArtifactAsync<T>(
        string runDir,
        string fileName,
        CancellationToken cancellationToken) where T : class
    {
        var filePath = Path.Combine(runDir, fileName);
        var loadResult = await ArtifactLoader.LoadAsync<T>(filePath, cancellationToken);
        return loadResult.IsSuccess ? loadResult.Value : null;
    }
}
