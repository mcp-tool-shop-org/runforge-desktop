using System.Text.Json;
using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.Core.Tests.Services;

/// <summary>
/// Tests for RunRequestService atomic load/save operations.
/// </summary>
public class RunRequestServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly RunRequestService _service;

    public RunRequestServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "runforge-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        _service = new RunRequestService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests
        }
    }

    #region Test Helpers

    private static RunRequest CreateValidRequest() => new()
    {
        Version = 1,
        Preset = "balanced",
        Dataset = new RunRequestDataset
        {
            Path = "data/test.csv",
            LabelColumn = "target"
        },
        Model = new RunRequestModel
        {
            Family = "logistic_regression"
        },
        Device = new RunRequestDevice
        {
            Type = "cpu"
        },
        CreatedAt = "2026-02-01T12:00:00Z",
        CreatedBy = "runforge-desktop@0.2.0"
    };

    private string GetRunDir(string runId) =>
        Path.Combine(_testDir, ".runforge", "runs", runId);

    #endregion

    #region Save Tests

    [Fact]
    public async Task SaveAsync_ValidRequest_CreatesFile()
    {
        // Arrange
        var runDir = GetRunDir("test-run-1");
        var request = CreateValidRequest();

        // Act
        var result = await _service.SaveAsync(runDir, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.FilePath);
        Assert.True(File.Exists(result.FilePath));
    }

    [Fact]
    public async Task SaveAsync_ValidRequest_WritesValidJson()
    {
        // Arrange
        var runDir = GetRunDir("test-run-2");
        var request = CreateValidRequest();

        // Act
        await _service.SaveAsync(runDir, request);

        // Assert
        var json = await File.ReadAllTextAsync(Path.Combine(runDir, "request.json"));
        var parsed = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        Assert.NotNull(parsed);
        Assert.Equal(request.Version, parsed.Version);
        Assert.Equal(request.Preset, parsed.Preset);
        Assert.Equal(request.Dataset.Path, parsed.Dataset.Path);
    }

    [Fact]
    public async Task SaveAsync_InvalidRequest_ReturnsValidationErrors()
    {
        // Arrange
        var runDir = GetRunDir("test-run-invalid");
        var request = new RunRequest
        {
            Version = 0, // Invalid
            Preset = "", // Invalid
            Dataset = new RunRequestDataset
            {
                Path = "",
                LabelColumn = ""
            },
            Model = new RunRequestModel
            {
                Family = ""
            },
            Device = new RunRequestDevice
            {
                Type = ""
            },
            CreatedAt = "",
            CreatedBy = ""
        };

        // Act
        var result = await _service.SaveAsync(runDir, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors.Count > 0);
        Assert.False(File.Exists(Path.Combine(runDir, "request.json")));
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfNeeded()
    {
        // Arrange
        var runDir = GetRunDir("nested/deep/run");
        var request = CreateValidRequest();

        // Act
        var result = await _service.SaveAsync(runDir, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(Directory.Exists(runDir));
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingFile()
    {
        // Arrange
        var runDir = GetRunDir("test-run-overwrite");
        var request1 = CreateValidRequest() with { Name = "First" };
        var request2 = CreateValidRequest() with { Name = "Second" };

        // Act
        await _service.SaveAsync(runDir, request1);
        var result = await _service.SaveAsync(runDir, request2);

        // Assert
        Assert.True(result.IsSuccess);
        var loaded = _service.Load(runDir);
        Assert.Equal("Second", loaded.Value?.Name);
    }

    [Fact]
    public void Save_Sync_ValidRequest_CreatesFile()
    {
        // Arrange
        var runDir = GetRunDir("test-run-sync");
        var request = CreateValidRequest();

        // Act
        var result = _service.Save(runDir, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(Path.Combine(runDir, "request.json")));
    }

    #endregion

    #region Load Tests

    [Fact]
    public async Task LoadAsync_ExistingFile_ReturnsRequest()
    {
        // Arrange
        var runDir = GetRunDir("test-run-load");
        var request = CreateValidRequest() with { Name = "Test Load" };
        await _service.SaveAsync(runDir, request);

        // Act
        var result = await _service.LoadAsync(runDir);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Test Load", result.Value.Name);
        Assert.Equal(1, result.Value.Version);
    }

    [Fact]
    public async Task LoadAsync_NonExistentFile_ReturnsNotFound()
    {
        // Arrange
        var runDir = GetRunDir("nonexistent-run");

        // Act
        var result = await _service.LoadAsync(runDir);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ArtifactErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task LoadAsync_MalformedJson_ReturnsMalformedError()
    {
        // Arrange
        var runDir = GetRunDir("test-run-malformed");
        Directory.CreateDirectory(runDir);
        await File.WriteAllTextAsync(Path.Combine(runDir, "request.json"), "{ invalid json }");

        // Act
        var result = await _service.LoadAsync(runDir);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ArtifactErrorType.MalformedJson, result.Error.Type);
    }

    [Fact]
    public void Load_Sync_ExistingFile_ReturnsRequest()
    {
        // Arrange
        var runDir = GetRunDir("test-run-load-sync");
        var request = CreateValidRequest();
        _service.Save(runDir, request);

        // Act
        var result = _service.Load(runDir);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public async Task RoundTrip_PreservesAllFields()
    {
        // Arrange
        var runDir = GetRunDir("test-run-roundtrip");
        var request = new RunRequest
        {
            Version = 1,
            Preset = "thorough",
            Dataset = new RunRequestDataset
            {
                Path = "data/customers.csv",
                LabelColumn = "churn"
            },
            Model = new RunRequestModel
            {
                Family = "random_forest",
                Hyperparameters = new Dictionary<string, JsonElement>
                {
                    ["n_estimators"] = JsonSerializer.SerializeToElement(200),
                    ["max_depth"] = JsonSerializer.SerializeToElement(10)
                }
            },
            Device = new RunRequestDevice
            {
                Type = "gpu",
                GpuReason = null
            },
            CreatedAt = "2026-02-01T14:30:00Z",
            CreatedBy = "runforge-vscode@0.3.6",
            Schema = "https://runforge.dev/schemas/request.v1.json",
            RerunFrom = "20260201-120000-abc12345",
            Name = "Customer Churn Model",
            Tags = new List<string> { "production", "v2" },
            Notes = "Testing round-trip preservation"
        };

        // Act
        await _service.SaveAsync(runDir, request);
        var result = await _service.LoadAsync(runDir);

        // Assert
        Assert.True(result.IsSuccess);
        var loaded = result.Value!;

        Assert.Equal(request.Version, loaded.Version);
        Assert.Equal(request.Preset, loaded.Preset);
        Assert.Equal(request.Dataset.Path, loaded.Dataset.Path);
        Assert.Equal(request.Dataset.LabelColumn, loaded.Dataset.LabelColumn);
        Assert.Equal(request.Model.Family, loaded.Model.Family);
        Assert.NotNull(loaded.Model.Hyperparameters);
        Assert.Equal(2, loaded.Model.Hyperparameters.Count);
        Assert.Equal(request.Device.Type, loaded.Device.Type);
        Assert.Equal(request.CreatedAt, loaded.CreatedAt);
        Assert.Equal(request.CreatedBy, loaded.CreatedBy);
        Assert.Equal(request.Schema, loaded.Schema);
        Assert.Equal(request.RerunFrom, loaded.RerunFrom);
        Assert.Equal(request.Name, loaded.Name);
        Assert.NotNull(loaded.Tags);
        Assert.Equal(2, loaded.Tags.Count);
        Assert.Contains("production", loaded.Tags);
        Assert.Equal(request.Notes, loaded.Notes);
    }

    [Fact]
    public async Task RoundTrip_PreservesUnknownFields()
    {
        // Arrange
        var runDir = GetRunDir("test-run-unknown-fields");

        // Write JSON with unknown fields directly
        Directory.CreateDirectory(runDir);
        var json = """
        {
          "version": 1,
          "preset": "balanced",
          "dataset": {
            "path": "data.csv",
            "label_column": "label",
            "future_field": "should be preserved"
          },
          "model": {
            "family": "logistic_regression",
            "future_nested": { "key": "value" }
          },
          "device": {
            "type": "cpu"
          },
          "created_at": "2026-02-01T12:00:00Z",
          "created_by": "test@1.0",
          "top_level_future": 42
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(runDir, "request.json"), json);

        // Act
        var loadResult = await _service.LoadAsync(runDir);
        Assert.True(loadResult.IsSuccess);

        var saveResult = await _service.SaveAsync(runDir, loadResult.Value!);
        Assert.True(saveResult.IsSuccess);

        // Assert - reload and check unknown fields
        var reloadResult = await _service.LoadAsync(runDir);
        Assert.True(reloadResult.IsSuccess);
        var request = reloadResult.Value!;

        Assert.NotNull(request.ExtensionData);
        Assert.True(request.ExtensionData.ContainsKey("top_level_future"));

        Assert.NotNull(request.Dataset.ExtensionData);
        Assert.True(request.Dataset.ExtensionData.ContainsKey("future_field"));

        Assert.NotNull(request.Model.ExtensionData);
        Assert.True(request.Model.ExtensionData.ContainsKey("future_nested"));
    }

    #endregion

    #region Atomic Write Tests

    [Fact]
    public async Task SaveAsync_NoTempFileLeftBehind()
    {
        // Arrange
        var runDir = GetRunDir("test-run-no-temp");
        var request = CreateValidRequest();

        // Act
        await _service.SaveAsync(runDir, request);

        // Assert
        var files = Directory.GetFiles(runDir);
        Assert.Single(files);
        Assert.EndsWith("request.json", files[0]);
        Assert.DoesNotContain(files, f => f.EndsWith(".tmp"));
    }

    [Fact]
    public async Task SaveAsync_ValidationFailure_NoFileCreated()
    {
        // Arrange
        var runDir = GetRunDir("test-run-validation-fail");
        var invalidRequest = new RunRequest
        {
            Version = 0,
            Preset = "",
            Dataset = new RunRequestDataset { Path = "", LabelColumn = "" },
            Model = new RunRequestModel { Family = "" },
            Device = new RunRequestDevice { Type = "" },
            CreatedAt = "",
            CreatedBy = ""
        };

        // Act
        var result = await _service.SaveAsync(runDir, invalidRequest);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(Directory.Exists(runDir) && Directory.GetFiles(runDir).Length > 0);
    }

    #endregion
}
