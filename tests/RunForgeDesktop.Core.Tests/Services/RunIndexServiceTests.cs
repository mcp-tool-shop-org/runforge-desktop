using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.Core.Tests.Services;

public class RunIndexServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly RunIndexService _service;

    public RunIndexServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"RunIndexServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new RunIndexService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void InitialState_HasNoLoadedIndex()
    {
        Assert.False(_service.HasLoadedIndex);
        Assert.Empty(_service.CurrentRuns);
    }

    [Fact]
    public async Task LoadIndex_NonexistentFile_ReturnsFailure()
    {
        // Arrange
        var workspace = Path.Combine(_tempDir, "nonexistent");
        Directory.CreateDirectory(workspace);

        // Act
        var result = await _service.LoadIndexAsync(workspace);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadIndex_ValidIndex_ReturnsRuns()
    {
        // Arrange
        var workspace = CreateWorkspaceWithRuns(3);

        // Act
        var result = await _service.LoadIndexAsync(workspace);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Runs.Count);
        Assert.False(result.FromCache);
    }

    [Fact]
    public async Task LoadIndex_SortsNewestFirst()
    {
        // Arrange
        var workspace = CreateWorkspaceWithRuns(3);

        // Act
        var result = await _service.LoadIndexAsync(workspace);

        // Assert
        Assert.True(result.IsSuccess);
        var timestamps = result.Runs.Select(r => r.ParsedCreatedAt).ToList();
        Assert.True(timestamps[0] >= timestamps[1]);
        Assert.True(timestamps[1] >= timestamps[2]);
    }

    [Fact]
    public async Task LoadIndex_SecondCall_ReturnsFromCache()
    {
        // Arrange
        var workspace = CreateWorkspaceWithRuns(2);

        // First load
        var result1 = await _service.LoadIndexAsync(workspace);
        Assert.False(result1.FromCache);

        // Act - Second load (should be cached)
        var result2 = await _service.LoadIndexAsync(workspace);

        // Assert
        Assert.True(result2.FromCache);
        Assert.Equal(result1.Runs.Count, result2.Runs.Count);
    }

    [Fact]
    public async Task LoadIndex_ForceRefresh_BypassesCache()
    {
        // Arrange
        var workspace = CreateWorkspaceWithRuns(2);

        // First load
        await _service.LoadIndexAsync(workspace);

        // Act - Force refresh
        var result = await _service.LoadIndexAsync(workspace, forceRefresh: true);

        // Assert
        Assert.False(result.FromCache);
    }

    [Fact]
    public async Task LoadIndex_RaisesIndexChangedEvent()
    {
        // Arrange
        var workspace = CreateWorkspaceWithRuns(2);
        RunIndexChangedEventArgs? capturedArgs = null;
        _service.IndexChanged += (_, args) => capturedArgs = args;

        // Act
        await _service.LoadIndexAsync(workspace);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal(2, capturedArgs.Runs.Count);
    }

    [Fact]
    public async Task FilterRuns_ByRunIdSubstring_FiltersCorrectly()
    {
        // Arrange
        var workspace = CreateWorkspaceWithRuns(3);
        await _service.LoadIndexAsync(workspace);

        // Act - Filter by run ID containing "run-1"
        var filtered = _service.FilterRuns(runIdSubstring: "run-1");

        // Assert
        Assert.Single(filtered);
    }

    [Fact]
    public async Task FilterRuns_ByStatus_FiltersCorrectly()
    {
        // Arrange
        var workspace = CreateWorkspaceWithMixedStatus();
        await _service.LoadIndexAsync(workspace);

        // Act
        var succeeded = _service.FilterRuns(statusFilter: RunStatusFilter.Succeeded);
        var failed = _service.FilterRuns(statusFilter: RunStatusFilter.Failed);

        // Assert
        Assert.Equal(2, succeeded.Count);
        Assert.Single(failed);
    }

    [Fact]
    public async Task GetRunById_ExistingRun_ReturnsRun()
    {
        // Arrange
        var workspace = CreateWorkspaceWithRuns(3);
        await _service.LoadIndexAsync(workspace);

        // Act
        var run = _service.GetRunById("20260201-100000-run-1-aaaa");

        // Assert
        Assert.NotNull(run);
        Assert.Equal("Run 1", run.Name);
    }

    [Fact]
    public async Task GetRunById_NonexistentRun_ReturnsNull()
    {
        // Arrange
        var workspace = CreateWorkspaceWithRuns(2);
        await _service.LoadIndexAsync(workspace);

        // Act
        var run = _service.GetRunById("nonexistent-run-id");

        // Assert
        Assert.Null(run);
    }

    [Fact]
    public void ClearCache_ClearsAllCaches()
    {
        // Arrange - just verify it doesn't throw
        _service.ClearCache();
        _service.ClearCache("some/path");

        // Assert - no exception means success
        Assert.True(true);
    }

    private string CreateWorkspaceWithRuns(int count)
    {
        var workspace = Path.Combine(_tempDir, $"workspace_{Guid.NewGuid():N}");
        var indexDir = Path.Combine(workspace, ".ml", "outputs");
        Directory.CreateDirectory(indexDir);

        var runs = new List<object>();
        for (var i = 0; i < count; i++)
        {
            runs.Add(new
            {
                run_id = $"20260201-{100000 + i:D6}-run-{i + 1}-aaaa",
                created_at = $"2026-02-01T{10 + i}:00:00-05:00",
                name = $"Run {i + 1}",
                preset_id = "std-train",
                status = "succeeded",
                run_dir = $".ml/runs/20260201-{100000 + i:D6}-run-{i + 1}-aaaa",
                summary = new
                {
                    duration_ms = 5000,
                    final_metrics = new Dictionary<string, double> { { "accuracy", 0.9 + i * 0.01 } },
                    device = "cuda"
                }
            });
        }

        var json = System.Text.Json.JsonSerializer.Serialize(runs);
        File.WriteAllText(Path.Combine(indexDir, "index.json"), json);

        return workspace;
    }

    private string CreateWorkspaceWithMixedStatus()
    {
        var workspace = Path.Combine(_tempDir, $"workspace_{Guid.NewGuid():N}");
        var indexDir = Path.Combine(workspace, ".ml", "outputs");
        Directory.CreateDirectory(indexDir);

        var runs = new List<object>
        {
            new
            {
                run_id = "20260201-100000-run-1-aaaa",
                created_at = "2026-02-01T10:00:00-05:00",
                name = "Succeeded Run 1",
                preset_id = "std-train",
                status = "succeeded",
                run_dir = ".ml/runs/20260201-100000-run-1-aaaa",
                summary = new { duration_ms = 5000, final_metrics = new Dictionary<string, double>(), device = "cuda" }
            },
            new
            {
                run_id = "20260201-110000-run-2-bbbb",
                created_at = "2026-02-01T11:00:00-05:00",
                name = "Failed Run",
                preset_id = "hq-train",
                status = "failed",
                run_dir = ".ml/runs/20260201-110000-run-2-bbbb",
                summary = new { duration_ms = 1000, final_metrics = new Dictionary<string, double>(), device = "cpu" }
            },
            new
            {
                run_id = "20260201-120000-run-3-cccc",
                created_at = "2026-02-01T12:00:00-05:00",
                name = "Succeeded Run 2",
                preset_id = "std-train",
                status = "succeeded",
                run_dir = ".ml/runs/20260201-120000-run-3-cccc",
                summary = new { duration_ms = 6000, final_metrics = new Dictionary<string, double>(), device = "cuda" }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(runs);
        File.WriteAllText(Path.Combine(indexDir, "index.json"), json);

        return workspace;
    }
}
