using System.Collections.Concurrent;
using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of run index loading with caching.
/// </summary>
public sealed class RunIndexService : IRunIndexService
{
    private readonly ConcurrentDictionary<string, CachedIndex> _cache = new();
    private IReadOnlyList<RunIndexEntry> _currentRuns = [];
    private string? _currentWorkspacePath;

    /// <inheritdoc />
    public bool HasLoadedIndex => _currentRuns.Count > 0;

    /// <inheritdoc />
    public IReadOnlyList<RunIndexEntry> CurrentRuns => _currentRuns;

    /// <inheritdoc />
    public event EventHandler<RunIndexChangedEventArgs>? IndexChanged;

    /// <inheritdoc />
    public async Task<RunIndexLoadResult> LoadIndexAsync(
        string workspacePath,
        string? indexPath = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        workspacePath = Path.GetFullPath(workspacePath);
        indexPath ??= Path.Combine(workspacePath, WorkspacePaths.IndexFile.Replace('/', Path.DirectorySeparatorChar));

        // Check cache first (unless forcing refresh)
        if (!forceRefresh && TryGetFromCache(workspacePath, indexPath, out var cachedRuns))
        {
            UpdateCurrentRuns(cachedRuns, workspacePath);
            return RunIndexLoadResult.Success(cachedRuns, indexPath, fromCache: true);
        }

        // Load from disk
        var loadResult = await ArtifactLoader.LoadAsync<List<RunIndexEntry>>(indexPath, cancellationToken);

        if (!loadResult.IsSuccess || loadResult.Value is null)
        {
            var errorMsg = loadResult.Error?.Message ?? "Unknown error loading index";
            if (loadResult.Error?.InnerDetails is not null)
            {
                errorMsg += $": {loadResult.Error.InnerDetails}";
            }
            return RunIndexLoadResult.Failure(errorMsg);
        }

        // Sort newest first (do not mutate original list, create sorted copy)
        var sortedRuns = loadResult.Value
            .OrderByDescending(r => r.ParsedCreatedAt ?? DateTimeOffset.MinValue)
            .ToList()
            .AsReadOnly();

        // Cache the result
        UpdateCache(workspacePath, indexPath, sortedRuns);
        UpdateCurrentRuns(sortedRuns, workspacePath);

        return RunIndexLoadResult.Success(sortedRuns, indexPath, fromCache: false);
    }

    /// <inheritdoc />
    public void ClearCache(string? workspacePath = null)
    {
        if (workspacePath is null)
        {
            _cache.Clear();
        }
        else
        {
            var normalizedPath = Path.GetFullPath(workspacePath);
            _cache.TryRemove(normalizedPath, out _);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RunIndexEntry> FilterRuns(
        string? runIdSubstring = null,
        string? modelFamily = null,
        RunStatusFilter statusFilter = RunStatusFilter.All)
    {
        IEnumerable<RunIndexEntry> filtered = _currentRuns;

        // Filter by run ID substring
        if (!string.IsNullOrWhiteSpace(runIdSubstring))
        {
            filtered = filtered.Where(r =>
                r.RunId.Contains(runIdSubstring, StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains(runIdSubstring, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by model family
        if (!string.IsNullOrWhiteSpace(modelFamily))
        {
            // Model family might be in preset_id or summary
            filtered = filtered.Where(r =>
                r.PresetId.Equals(modelFamily, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by status
        filtered = statusFilter switch
        {
            RunStatusFilter.Succeeded => filtered.Where(r => r.IsSucceeded),
            RunStatusFilter.Failed => filtered.Where(r => !r.IsSucceeded),
            _ => filtered
        };

        return filtered.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public RunIndexEntry? GetRunById(string runId)
    {
        return _currentRuns.FirstOrDefault(r =>
            r.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryGetFromCache(string workspacePath, string indexPath, out IReadOnlyList<RunIndexEntry> runs)
    {
        if (_cache.TryGetValue(workspacePath, out var cached))
        {
            // Check if the index file has been modified since we cached it
            if (File.Exists(indexPath))
            {
                var lastWriteTime = File.GetLastWriteTimeUtc(indexPath);
                if (lastWriteTime <= cached.CachedAt)
                {
                    runs = cached.Runs;
                    return true;
                }
            }
        }

        runs = [];
        return false;
    }

    private void UpdateCache(string workspacePath, string indexPath, IReadOnlyList<RunIndexEntry> runs)
    {
        var cached = new CachedIndex
        {
            Runs = runs,
            IndexPath = indexPath,
            CachedAt = DateTime.UtcNow
        };

        _cache.AddOrUpdate(workspacePath, cached, (_, _) => cached);
    }

    private void UpdateCurrentRuns(IReadOnlyList<RunIndexEntry> runs, string workspacePath)
    {
        _currentRuns = runs;
        _currentWorkspacePath = workspacePath;

        IndexChanged?.Invoke(this, new RunIndexChangedEventArgs
        {
            Runs = runs,
            WorkspacePath = workspacePath
        });
    }

    private sealed record CachedIndex
    {
        public required IReadOnlyList<RunIndexEntry> Runs { get; init; }
        public required string IndexPath { get; init; }
        public required DateTime CachedAt { get; init; }
    }
}
