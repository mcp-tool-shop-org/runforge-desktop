using System.Text.Json;
using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests.Models;

public class InterpretabilityIndexV1Tests
{
    private const string ValidIndexJson = """
    {
        "schema_version": "1.0",
        "run_id": "20260201-142355-test-run-a3f9",
        "model_family": "logistic_regression",
        "artifacts": [
            {
                "type": "metrics.v1",
                "schema_version": "1.0",
                "path": "artifacts/metrics.v1.json",
                "available": true
            },
            {
                "type": "feature_importance.v1",
                "schema_version": "1.0",
                "path": "artifacts/feature_importance.v1.json",
                "available": false,
                "unavailable_reason": "not_supported_for_model"
            },
            {
                "type": "linear_coefficients.v1",
                "schema_version": "1.0",
                "path": "artifacts/linear_coefficients.v1.json",
                "available": true
            }
        ],
        "generated_at": "2026-02-01T14:23:55-05:00"
    }
    """;

    [Fact]
    public void Deserialize_ValidJson_ParsesCorrectly()
    {
        // Act
        var index = JsonSerializer.Deserialize<InterpretabilityIndexV1>(ValidIndexJson, JsonOptions.Default);

        // Assert
        Assert.NotNull(index);
        Assert.Equal("1.0", index.SchemaVersion);
        Assert.Equal("20260201-142355-test-run-a3f9", index.RunId);
        Assert.Equal("logistic_regression", index.ModelFamily);
        Assert.Equal(3, index.Artifacts.Count);
    }

    [Fact]
    public void HasArtifact_AvailableArtifact_ReturnsTrue()
    {
        // Arrange
        var index = JsonSerializer.Deserialize<InterpretabilityIndexV1>(ValidIndexJson, JsonOptions.Default);

        // Assert
        Assert.NotNull(index);
        Assert.True(index.HasArtifact("metrics.v1"));
        Assert.True(index.HasArtifact("linear_coefficients.v1"));
    }

    [Fact]
    public void HasArtifact_UnavailableArtifact_ReturnsFalse()
    {
        // Arrange
        var index = JsonSerializer.Deserialize<InterpretabilityIndexV1>(ValidIndexJson, JsonOptions.Default);

        // Assert
        Assert.NotNull(index);
        Assert.False(index.HasArtifact("feature_importance.v1"));
    }

    [Fact]
    public void HasArtifact_NonexistentArtifact_ReturnsFalse()
    {
        // Arrange
        var index = JsonSerializer.Deserialize<InterpretabilityIndexV1>(ValidIndexJson, JsonOptions.Default);

        // Assert
        Assert.NotNull(index);
        Assert.False(index.HasArtifact("nonexistent.v1"));
    }

    [Fact]
    public void GetArtifact_ExistingType_ReturnsEntry()
    {
        // Arrange
        var index = JsonSerializer.Deserialize<InterpretabilityIndexV1>(ValidIndexJson, JsonOptions.Default);

        // Act
        var artifact = index?.GetArtifact("feature_importance.v1");

        // Assert
        Assert.NotNull(artifact);
        Assert.False(artifact.Available);
        Assert.Equal("not_supported_for_model", artifact.UnavailableReason);
    }

    [Fact]
    public void GetArtifact_NonexistentType_ReturnsNull()
    {
        // Arrange
        var index = JsonSerializer.Deserialize<InterpretabilityIndexV1>(ValidIndexJson, JsonOptions.Default);

        // Act
        var artifact = index?.GetArtifact("nonexistent.v1");

        // Assert
        Assert.Null(artifact);
    }
}
