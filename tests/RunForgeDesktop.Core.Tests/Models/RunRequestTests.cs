using System.Text.Json;
using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests.Models;

/// <summary>
/// Tests for RunRequest model per V1 Contract (docs/V1_CONTRACT.md).
/// </summary>
public class RunRequestTests
{
    #region Test Vector Paths

    private static string GetTestVectorPath(string filename) =>
        Path.Combine(GetRepoRoot(), "docs", "test-vectors", filename);

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "RunForgeDesktop.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root");
    }

    #endregion

    #region Minimal Schema Tests

    [Fact]
    public void Deserialize_MinimalTestVector_ParsesCorrectly()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("request.v1.min.json"));

        // Act
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(1, request.Version);
        Assert.Equal("balanced", request.Preset);
        Assert.Equal("data/iris.csv", request.Dataset.Path);
        Assert.Equal("label", request.Dataset.LabelColumn);
        Assert.Equal("logistic_regression", request.Model.Family);
        Assert.Equal("cpu", request.Device.Type);
        Assert.Equal("2026-02-01T12:00:00Z", request.CreatedAt);
        Assert.Equal("runforge-vscode@0.3.6", request.CreatedBy);
    }

    [Fact]
    public void Deserialize_MinimalTestVector_IsValid()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("request.v1.min.json"));

        // Act
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(request);
        Assert.True(request.IsValid);
        Assert.Empty(request.Validate());
    }

    #endregion

    #region Full Schema Tests

    [Fact]
    public void Deserialize_FullTestVector_ParsesAllFields()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("request.v1.full.json"));

        // Act
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(1, request.Version);
        Assert.Equal("thorough", request.Preset);
        Assert.Equal("data/training/customers.csv", request.Dataset.Path);
        Assert.Equal("churn", request.Dataset.LabelColumn);
        Assert.Equal("random_forest", request.Model.Family);
        Assert.NotNull(request.Model.Hyperparameters);
        Assert.Equal(3, request.Model.Hyperparameters.Count);
        Assert.Equal("gpu", request.Device.Type);
        Assert.Null(request.Device.GpuReason);
        Assert.Equal("2026-02-01T14:30:00Z", request.CreatedAt);
        Assert.Equal("runforge-vscode@0.3.6", request.CreatedBy);
        Assert.Equal("20260201-120000-abc12345", request.RerunFrom);
        Assert.Equal("Customer Churn - High Quality", request.Name);
        Assert.NotNull(request.Tags);
        Assert.Equal(3, request.Tags.Count);
        Assert.Contains("production", request.Tags);
        Assert.Equal("Rerun with increased estimators after stakeholder feedback.", request.Notes);
        Assert.Equal("https://runforge.dev/schemas/request.v1.json", request.Schema);
    }

    [Fact]
    public void Deserialize_FullTestVector_IsValid()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("request.v1.full.json"));

        // Act
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(request);
        Assert.True(request.IsValid);
    }

    #endregion

    #region Forward Compatibility Tests

    [Fact]
    public void Deserialize_UnknownFieldsTestVector_PreservesExtensionData()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("request.v1.unknown-fields.json"));

        // Act
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(request);
        Assert.NotNull(request.ExtensionData);
        Assert.True(request.ExtensionData.ContainsKey("future_top_level_field"));
        Assert.True(request.ExtensionData.ContainsKey("another_future_field"));
    }

    [Fact]
    public void Deserialize_UnknownFieldsTestVector_PreservesNestedExtensionData()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("request.v1.unknown-fields.json"));

        // Act
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(request);
        Assert.NotNull(request.Dataset.ExtensionData);
        Assert.True(request.Dataset.ExtensionData.ContainsKey("future_dataset_field"));
        Assert.NotNull(request.Model.ExtensionData);
        Assert.True(request.Model.ExtensionData.ContainsKey("future_model_field"));
    }

    [Fact]
    public void RoundTrip_UnknownFieldsTestVector_PreservesAllFields()
    {
        // Arrange
        var originalJson = File.ReadAllText(GetTestVectorPath("request.v1.unknown-fields.json"));
        var request = JsonSerializer.Deserialize<RunRequest>(originalJson, JsonOptions.Default);

        // Act
        var serialized = JsonSerializer.Serialize(request, JsonOptions.Default);
        var reparsed = JsonDocument.Parse(serialized);
        var originalDoc = JsonDocument.Parse(originalJson);

        // Assert - all original fields should be present
        Assert.True(reparsed.RootElement.TryGetProperty("future_top_level_field", out var futureField));
        Assert.True(futureField.TryGetProperty("nested", out var nested));
        Assert.True(nested.GetBoolean());

        Assert.True(reparsed.RootElement.TryGetProperty("another_future_field", out var another));
        Assert.Equal("forward compatibility test", another.GetString());

        // Nested unknown fields
        var dataset = reparsed.RootElement.GetProperty("dataset");
        Assert.True(dataset.TryGetProperty("future_dataset_field", out _));

        var model = reparsed.RootElement.GetProperty("model");
        Assert.True(model.TryGetProperty("future_model_field", out var modelField));
        Assert.Equal(42, modelField.GetInt32());
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Validate_MissingVersion_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "version": 0,
            "preset": "balanced",
            "dataset": { "path": "data.csv", "label_column": "label" },
            "model": { "family": "logistic_regression" },
            "device": { "type": "cpu" },
            "created_at": "2026-02-01T12:00:00Z",
            "created_by": "test@1.0"
        }
        """;
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Act
        var errors = request!.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("version"));
    }

    [Fact]
    public void Validate_MissingPreset_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "version": 1,
            "preset": "",
            "dataset": { "path": "data.csv", "label_column": "label" },
            "model": { "family": "logistic_regression" },
            "device": { "type": "cpu" },
            "created_at": "2026-02-01T12:00:00Z",
            "created_by": "test@1.0"
        }
        """;
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Act
        var errors = request!.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("preset"));
    }

    [Fact]
    public void Validate_MissingDatasetPath_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "version": 1,
            "preset": "balanced",
            "dataset": { "path": "", "label_column": "label" },
            "model": { "family": "logistic_regression" },
            "device": { "type": "cpu" },
            "created_at": "2026-02-01T12:00:00Z",
            "created_by": "test@1.0"
        }
        """;
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Act
        var errors = request!.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("dataset.path"));
    }

    [Fact]
    public void Validate_MissingModelFamily_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "version": 1,
            "preset": "balanced",
            "dataset": { "path": "data.csv", "label_column": "label" },
            "model": { "family": "" },
            "device": { "type": "cpu" },
            "created_at": "2026-02-01T12:00:00Z",
            "created_by": "test@1.0"
        }
        """;
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Act
        var errors = request!.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("model.family"));
    }

    [Fact]
    public void Validate_MissingCreatedBy_ReturnsError()
    {
        // Arrange
        var json = """
        {
            "version": 1,
            "preset": "balanced",
            "dataset": { "path": "data.csv", "label_column": "label" },
            "model": { "family": "logistic_regression" },
            "device": { "type": "cpu" },
            "created_at": "2026-02-01T12:00:00Z",
            "created_by": ""
        }
        """;
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Act
        var errors = request!.Validate();

        // Assert
        Assert.Contains(errors, e => e.Contains("created_by"));
    }

    #endregion

    #region ParsedCreatedAt Tests

    [Fact]
    public void ParsedCreatedAt_ValidTimestamp_ReturnsDateTimeOffset()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("request.v1.min.json"));
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Act
        var parsed = request!.ParsedCreatedAt;

        // Assert
        Assert.NotNull(parsed);
        Assert.Equal(2026, parsed.Value.Year);
        Assert.Equal(2, parsed.Value.Month);
        Assert.Equal(1, parsed.Value.Day);
        Assert.Equal(12, parsed.Value.Hour);
        Assert.Equal(0, parsed.Value.Minute);
        Assert.Equal(0, parsed.Value.Second);
    }

    [Fact]
    public void ParsedCreatedAt_InvalidTimestamp_ReturnsNull()
    {
        // Arrange
        var json = """
        {
            "version": 1,
            "preset": "balanced",
            "dataset": { "path": "data.csv", "label_column": "label" },
            "model": { "family": "logistic_regression" },
            "device": { "type": "cpu" },
            "created_at": "not-a-timestamp",
            "created_by": "test@1.0"
        }
        """;
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Act
        var parsed = request!.ParsedCreatedAt;

        // Assert
        Assert.Null(parsed);
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void Serialize_ValidRequest_ProducesValidJson()
    {
        // Arrange
        var request = new RunRequest
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
                Family = "random_forest"
            },
            Device = new RunRequestDevice
            {
                Type = "cpu"
            },
            CreatedAt = "2026-02-01T15:00:00Z",
            CreatedBy = "runforge-desktop@0.2.0"
        };

        // Act
        var json = JsonSerializer.Serialize(request, JsonOptions.Default);
        var reparsed = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(reparsed);
        Assert.Equal(request.Version, reparsed.Version);
        Assert.Equal(request.Preset, reparsed.Preset);
        Assert.Equal(request.Dataset.Path, reparsed.Dataset.Path);
        Assert.Equal(request.Model.Family, reparsed.Model.Family);
        Assert.Equal(request.Device.Type, reparsed.Device.Type);
        Assert.Equal(request.CreatedAt, reparsed.CreatedAt);
        Assert.Equal(request.CreatedBy, reparsed.CreatedBy);
    }

    #endregion
}
