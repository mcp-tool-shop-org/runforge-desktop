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
}
