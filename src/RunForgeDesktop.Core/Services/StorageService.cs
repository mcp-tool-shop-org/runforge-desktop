using System.Text.Json;
using RunForgeDesktop.Core.Json;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of storage calculation service.
/// </summary>
public sealed class StorageService : IStorageService
{
    /// <inheritdoc />
    public async Task<WorkspaceStorageSummary> CalculateStorageAsync(
        string workspacePath,
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        var runsPath = Path.Combine(workspacePath, ".ml", "runs");
        if (!Directory.Exists(runsPath))
        {
            return new WorkspaceStorageSummary
            {
                TotalBytes = 0,
                TotalLogsBytes = 0,
                TotalArtifactsBytes = 0,
                RunCount = 0,
                TopRunsBySize = Array.Empty<RunStorageInfo>()
            };
        }

        var allRuns = new List<RunStorageInfo>();
        long totalBytes = 0;
        long totalLogsBytes = 0;
        long totalArtifactsBytes = 0;

        var runDirs = Directory.GetDirectories(runsPath);

        foreach (var runPath in runDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var runId = Path.GetFileName(runPath);
            var runDir = $".ml/runs/{runId}";

            var info = await GetRunStorageInternalAsync(workspacePath, runPath, runId, runDir, cancellationToken);
            if (info is not null)
            {
                allRuns.Add(info);
                totalBytes += info.TotalBytes;
                totalLogsBytes += info.LogsBytes;
                totalArtifactsBytes += info.ArtifactsBytes;
            }
        }

        // Sort by size descending and take top N
        var topRuns = allRuns
            .OrderByDescending(r => r.TotalBytes)
            .Take(topN)
            .ToList();

        return new WorkspaceStorageSummary
        {
            TotalBytes = totalBytes,
            TotalLogsBytes = totalLogsBytes,
            TotalArtifactsBytes = totalArtifactsBytes,
            RunCount = allRuns.Count,
            TopRunsBySize = topRuns
        };
    }

    /// <inheritdoc />
    public async Task<RunStorageInfo?> GetRunStorageAsync(
        string workspacePath,
        string runDir,
        CancellationToken cancellationToken = default)
    {
        var runPath = Path.Combine(workspacePath, runDir.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(runPath))
        {
            return null;
        }

        var runId = Path.GetFileName(runPath);
        return await GetRunStorageInternalAsync(workspacePath, runPath, runId, runDir, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> DeleteRunAsync(
        string workspacePath,
        string runDir,
        CancellationToken cancellationToken = default)
    {
        var runPath = Path.Combine(workspacePath, runDir.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(runPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            Directory.Delete(runPath, recursive: true);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private async Task<RunStorageInfo?> GetRunStorageInternalAsync(
        string workspacePath,
        string runPath,
        string runId,
        string runDir,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(runPath))
        {
            return null;
        }

        // Calculate folder size
        long totalBytes = 0;
        long logsBytes = 0;
        long artifactsBytes = 0;
        DateTime lastModified = DateTime.MinValue;

        try
        {
            var files = Directory.GetFiles(runPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileInfo = new FileInfo(file);
                    var size = fileInfo.Length;
                    totalBytes += size;

                    if (fileInfo.LastWriteTimeUtc > lastModified)
                    {
                        lastModified = fileInfo.LastWriteTimeUtc;
                    }

                    // Check if it's logs.txt
                    if (fileInfo.Name.Equals("logs.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        logsBytes += size;
                    }
                    else
                    {
                        artifactsBytes += size;
                    }
                }
                catch
                {
                    // Skip inaccessible files
                }
            }
        }
        catch
        {
            // If we can't enumerate files, return null
            return null;
        }

        // Try to read run name from request.json
        var name = runId;
        var requestPath = Path.Combine(runPath, "request.json");
        if (File.Exists(requestPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(requestPath, cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("name", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String)
                {
                    var parsedName = nameElement.GetString();
                    if (!string.IsNullOrEmpty(parsedName))
                    {
                        name = parsedName;
                    }
                }
            }
            catch
            {
                // Use runId as name
            }
        }

        // Try to read status from result.json
        string? status = null;
        var resultPath = Path.Combine(runPath, "result.json");
        if (File.Exists(resultPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(resultPath, cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("status", out var statusElement) &&
                    statusElement.ValueKind == JsonValueKind.String)
                {
                    status = statusElement.GetString();
                }
            }
            catch
            {
                // No status
            }
        }

        return new RunStorageInfo
        {
            RunId = runId,
            Name = name,
            RunDir = runDir,
            TotalBytes = totalBytes,
            LogsBytes = logsBytes,
            ArtifactsBytes = artifactsBytes,
            LastModifiedUtc = lastModified,
            Status = status
        };
    }
}
