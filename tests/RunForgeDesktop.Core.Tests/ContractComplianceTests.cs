using System.Text.Json;
using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests;

/// <summary>
/// Contract compliance tests that verify Desktop and VS Code share the same schema.
/// These tests ensure the V1 Contract (docs/V1_CONTRACT.md) is properly implemented.
/// </summary>
public class ContractComplianceTests
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

    #region Contract Compliance Tests

    /// <summary>
    /// V1 Contract: Required fields must be present and parseable.
    /// </summary>
    [Fact]
    public void MinimalTestVector_MapsAllRequiredFields()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("request.v1.min.json"));

        // Act
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert - all required fields are present
        Assert.NotNull(request);
        Assert.Equal(1, request.Version);
        Assert.Equal("balanced", request.Preset);
        Assert.Equal("data/iris.csv", request.Dataset.Path);
        Assert.Equal("label", request.Dataset.LabelColumn);
        Assert.Equal("logistic_regression", request.Model.Family);
        Assert.Equal("cpu", request.Device.Type);
        Assert.Equal("2026-02-01T12:00:00Z", request.CreatedAt);
        Assert.Equal("runforge-vscode@0.3.6", request.CreatedBy);

        // Required fields validation passes
        Assert.True(request.IsValid);
    }

    /// <summary>
    /// V1 Contract: Full test vector with all optional fields.
    /// </summary>
    [Fact]
    public void FullTestVector_MapsAllOptionalFields()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("request.v1.full.json"));

        // Act
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert - optional fields are present
        Assert.NotNull(request);
        Assert.Equal("https://runforge.dev/schemas/request.v1.json", request.Schema);
        Assert.Equal("20260201-120000-abc12345", request.RerunFrom);
        Assert.Equal("Customer Churn - High Quality", request.Name);
        Assert.NotNull(request.Tags);
        Assert.Equal(3, request.Tags.Count);
        Assert.Contains("production", request.Tags);
        Assert.Contains("v2", request.Tags);
        Assert.Contains("quarterly-review", request.Tags);
        Assert.Equal("Rerun with increased estimators after stakeholder feedback.", request.Notes);
    }

    /// <summary>
    /// V1 Contract: Unknown fields must be preserved on round-trip.
    /// </summary>
    [Fact]
    public void UnknownFields_SurviveRoundTripSerialization()
    {
        // Arrange
        var originalJson = File.ReadAllText(GetTestVectorPath("request.v1.unknown-fields.json"));

        // Act - deserialize then reserialize
        var request = JsonSerializer.Deserialize<RunRequest>(originalJson, JsonOptions.Default);
        var serialized = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        var roundTripped = JsonDocument.Parse(serialized);

        // Assert - top-level unknown fields preserved
        Assert.True(roundTripped.RootElement.TryGetProperty("future_top_level_field", out var futureTopLevel));
        Assert.True(futureTopLevel.TryGetProperty("nested", out var nested));
        Assert.True(nested.GetBoolean());
        Assert.True(futureTopLevel.TryGetProperty("values", out var values));
        Assert.Equal(3, values.GetArrayLength());

        Assert.True(roundTripped.RootElement.TryGetProperty("another_future_field", out var anotherFuture));
        Assert.Equal("forward compatibility test", anotherFuture.GetString());

        // Assert - nested unknown fields preserved
        var dataset = roundTripped.RootElement.GetProperty("dataset");
        Assert.True(dataset.TryGetProperty("future_dataset_field", out var datasetFuture));
        Assert.Equal("this field does not exist in v1 schema", datasetFuture.GetString());

        var model = roundTripped.RootElement.GetProperty("model");
        Assert.True(model.TryGetProperty("future_model_field", out var modelFuture));
        Assert.Equal(42, modelFuture.GetInt32());
    }

    /// <summary>
    /// V1 Contract: created_by format must be client@version.
    /// </summary>
    [Fact]
    public void CreatedBy_FollowsClientAtVersionFormat()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("request.v1.min.json"));
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert - format is client@version
        Assert.NotNull(request);
        Assert.Contains("@", request.CreatedBy);
        var parts = request.CreatedBy.Split('@');
        Assert.Equal(2, parts.Length);
        Assert.False(string.IsNullOrEmpty(parts[0])); // client name
        Assert.False(string.IsNullOrEmpty(parts[1])); // version
    }

    /// <summary>
    /// V1 Contract: version must be an integer >= 1.
    /// </summary>
    [Fact]
    public void Version_IsPositiveInteger()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("request.v1.min.json"));
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(request);
        Assert.True(request.Version >= 1);
    }

    /// <summary>
    /// V1 Contract: preset must be one of the defined values.
    /// </summary>
    [Theory]
    [InlineData("request.v1.min.json", "balanced")]
    [InlineData("request.v1.full.json", "thorough")]
    public void Preset_HasValidValue(string filename, string expectedPreset)
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath(filename));
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(expectedPreset, request.Preset);
        Assert.Contains(request.Preset, new[] { "fast", "balanced", "thorough", "custom" });
    }

    /// <summary>
    /// V1 Contract: model.family must be a known value.
    /// </summary>
    [Theory]
    [InlineData("request.v1.min.json", "logistic_regression")]
    [InlineData("request.v1.full.json", "random_forest")]
    public void ModelFamily_HasValidValue(string filename, string expectedFamily)
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath(filename));
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(expectedFamily, request.Model.Family);
        Assert.Contains(request.Model.Family, new[] { "logistic_regression", "random_forest", "linear_svc" });
    }

    /// <summary>
    /// V1 Contract: device.type must be cpu or gpu.
    /// </summary>
    [Theory]
    [InlineData("request.v1.min.json", "cpu")]
    [InlineData("request.v1.full.json", "gpu")]
    public void DeviceType_HasValidValue(string filename, string expectedType)
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath(filename));
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(expectedType, request.Device.Type);
        Assert.Contains(request.Device.Type, new[] { "cpu", "gpu" });
    }

    /// <summary>
    /// V1 Contract: created_at must be ISO-8601 parseable.
    /// </summary>
    [Theory]
    [InlineData("request.v1.min.json")]
    [InlineData("request.v1.full.json")]
    public void CreatedAt_IsParseable(string filename)
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath(filename));
        var request = JsonSerializer.Deserialize<RunRequest>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(request);
        Assert.NotNull(request.ParsedCreatedAt);
        Assert.Equal(2026, request.ParsedCreatedAt.Value.Year);
    }

    #endregion

    #region Result V1 Contract Compliance Tests

    /// <summary>
    /// Result V1 Contract: Minimal test vector contains only required fields.
    /// </summary>
    [Fact]
    public void Result_MinimalTestVector_MapsRequiredFields()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("result.v1.minimal.json"));

        // Act
        var result = JsonSerializer.Deserialize<RunResult>(json, JsonOptions.Default);

        // Assert - all required fields are present
        Assert.NotNull(result);
        Assert.Equal(1, result.Version);
        Assert.Equal("succeeded", result.Status);
        Assert.Equal(12345, result.DurationMs);

        // Optional fields are null
        Assert.Null(result.Summary);
        Assert.Null(result.EffectiveConfig);
        Assert.Null(result.Artifacts);
        Assert.Null(result.Error);
    }

    /// <summary>
    /// Result V1 Contract: Succeeded test vector with all optional fields.
    /// </summary>
    [Fact]
    public void Result_SucceededTestVector_MapsAllFields()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("result.v1.succeeded.json"));

        // Act
        var result = JsonSerializer.Deserialize<RunResult>(json, JsonOptions.Default);

        // Assert - required fields
        Assert.NotNull(result);
        Assert.Equal(1, result.Version);
        Assert.Equal("succeeded", result.Status);
        Assert.Equal(300000, result.DurationMs);
        Assert.True(result.IsSucceeded);

        // Summary
        Assert.NotNull(result.Summary);
        Assert.NotNull(result.Summary.PrimaryMetric);
        Assert.Equal("accuracy", result.Summary.PrimaryMetric.Name);
        Assert.Equal(0.8532, result.Summary.PrimaryMetric.Value);
        Assert.NotNull(result.Summary.Metrics);
        Assert.Equal(5, result.Summary.Metrics.Count);
        Assert.Contains("f1_score", result.Summary.Metrics.Keys);

        // Effective config
        Assert.NotNull(result.EffectiveConfig);
        Assert.Equal("balanced", result.EffectiveConfig.Preset);
        Assert.NotNull(result.EffectiveConfig.Model);
        Assert.Equal("logistic_regression", result.EffectiveConfig.Model.Family);
        Assert.NotNull(result.EffectiveConfig.Device);
        Assert.Equal("cpu", result.EffectiveConfig.Device.Type);

        // Artifacts
        Assert.NotNull(result.Artifacts);
        Assert.Equal(4, result.Artifacts.Count);
        Assert.Contains(result.Artifacts, a => a.Path == "model.pkl" && a.Type == "model");
        Assert.Contains(result.Artifacts, a => a.Path == "metrics.json" && a.Type == "metrics");

        // No error
        Assert.Null(result.Error);
    }

    /// <summary>
    /// Result V1 Contract: Failed test vector with error object.
    /// </summary>
    [Fact]
    public void Result_FailedTestVector_MapsErrorObject()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("result.v1.failed.json"));

        // Act
        var result = JsonSerializer.Deserialize<RunResult>(json, JsonOptions.Default);

        // Assert - required fields
        Assert.NotNull(result);
        Assert.Equal(1, result.Version);
        Assert.Equal("failed", result.Status);
        Assert.Equal(5000, result.DurationMs);
        Assert.False(result.IsSucceeded);

        // Error object
        Assert.NotNull(result.Error);
        Assert.Contains("Unable to parse CSV file", result.Error.Message);
        Assert.Equal("ValueError", result.Error.Type);
        Assert.NotNull(result.Error.Traceback);
        Assert.Contains("Traceback", result.Error.Traceback);

        // No summary for failed run
        Assert.Null(result.Summary);

        // Empty artifacts
        Assert.NotNull(result.Artifacts);
        Assert.Empty(result.Artifacts);
    }

    /// <summary>
    /// Result V1 Contract: Status values are valid.
    /// </summary>
    [Theory]
    [InlineData("result.v1.succeeded.json", "succeeded")]
    [InlineData("result.v1.failed.json", "failed")]
    public void Result_Status_HasValidValue(string filename, string expectedStatus)
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath(filename));
        var result = JsonSerializer.Deserialize<RunResult>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Contains(result.Status, new[] { "succeeded", "failed", "cancelled" });
    }

    /// <summary>
    /// Result V1 Contract: Duration formatting.
    /// </summary>
    [Fact]
    public void Result_Duration_IsTimeSpan()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("result.v1.succeeded.json"));
        var result = JsonSerializer.Deserialize<RunResult>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromMilliseconds(300000), result.Duration);
        Assert.Equal(5, result.Duration.TotalMinutes);
    }

    /// <summary>
    /// Result V1 Contract: Unknown fields are ignored gracefully.
    /// </summary>
    [Fact]
    public void Result_UnknownFields_AreIgnoredGracefully()
    {
        // Arrange - add unknown fields to a valid result
        var json = """
        {
          "version": 1,
          "status": "succeeded",
          "duration_ms": 1000,
          "future_field": "should be ignored",
          "nested_future": { "value": 42 },
          "summary": {
            "metrics": { "accuracy": 0.9 },
            "future_summary_field": true
          }
        }
        """;

        // Act - should not throw
        var result = JsonSerializer.Deserialize<RunResult>(json, JsonOptions.Default);

        // Assert - known fields are parsed
        Assert.NotNull(result);
        Assert.Equal(1, result.Version);
        Assert.Equal("succeeded", result.Status);
        Assert.Equal(1000, result.DurationMs);
        Assert.NotNull(result.Summary);
        Assert.NotNull(result.Summary.Metrics);
        Assert.Equal(0.9, result.Summary.Metrics["accuracy"]);
    }

    /// <summary>
    /// Result V1 Contract: Artifact types are valid.
    /// </summary>
    [Fact]
    public void Result_ArtifactTypes_AreValid()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("result.v1.succeeded.json"));
        var result = JsonSerializer.Deserialize<RunResult>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Artifacts);

        var validTypes = new[] { "model", "metrics", "feature_importance", "linear_coefficients", "encoder", "log", "checkpoint", "other" };
        foreach (var artifact in result.Artifacts)
        {
            Assert.Contains(artifact.Type, validTypes);
        }
    }

    /// <summary>
    /// Result V1 Contract: Artifact bytes are positive.
    /// </summary>
    [Fact]
    public void Result_ArtifactBytes_ArePositive()
    {
        // Arrange
        var json = File.ReadAllText(GetTestVectorPath("result.v1.succeeded.json"));
        var result = JsonSerializer.Deserialize<RunResult>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Artifacts);
        Assert.All(result.Artifacts, a => Assert.True(a.Bytes > 0));
    }

    /// <summary>
    /// Result V1 Contract: Version mismatch should not cause parse failure.
    /// </summary>
    [Fact]
    public void Result_FutureVersion_ParsesWithWarning()
    {
        // Arrange - version 2 from future
        var json = """
        {
          "version": 2,
          "status": "succeeded",
          "duration_ms": 1000,
          "new_v2_field": "future field"
        }
        """;

        // Act - should not throw
        var result = JsonSerializer.Deserialize<RunResult>(json, JsonOptions.Default);

        // Assert - should still parse
        Assert.NotNull(result);
        Assert.Equal(2, result.Version);
        Assert.Equal("succeeded", result.Status);
    }

    #endregion
}
