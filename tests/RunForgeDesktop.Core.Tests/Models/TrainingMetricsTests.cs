using System.Text.Json;
using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests.Models;

public class TrainingMetricsTests
{
    [Fact]
    public void Deserialize_StrictSchema_ParsesExactly3Keys()
    {
        // Arrange - Phase 2.1 strict schema: exactly 3 keys
        var json = """
        {
            "accuracy": 0.95,
            "num_samples": 1000,
            "num_features": 10
        }
        """;

        // Act
        var metrics = JsonSerializer.Deserialize<TrainingMetrics>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(0.95, metrics.Accuracy);
        Assert.Equal(1000, metrics.NumSamples);
        Assert.Equal(10, metrics.NumFeatures);
    }

    [Fact]
    public void AccuracyPercent_FormatsCorrectly()
    {
        // Arrange
        var json = """
        {
            "accuracy": 0.8765,
            "num_samples": 500,
            "num_features": 5
        }
        """;

        // Act
        var metrics = JsonSerializer.Deserialize<TrainingMetrics>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(metrics);
        // Note: Exact format depends on culture, but should contain percentage
        Assert.Contains("87", metrics.AccuracyPercent);
    }

    [Fact]
    public void Deserialize_EdgeCase_ZeroValues()
    {
        // Arrange
        var json = """
        {
            "accuracy": 0.0,
            "num_samples": 0,
            "num_features": 0
        }
        """;

        // Act
        var metrics = JsonSerializer.Deserialize<TrainingMetrics>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(0.0, metrics.Accuracy);
        Assert.Equal(0, metrics.NumSamples);
        Assert.Equal(0, metrics.NumFeatures);
    }

    [Fact]
    public void Deserialize_PerfectAccuracy_HandlesOne()
    {
        // Arrange
        var json = """
        {
            "accuracy": 1.0,
            "num_samples": 100,
            "num_features": 3
        }
        """;

        // Act
        var metrics = JsonSerializer.Deserialize<TrainingMetrics>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(1.0, metrics.Accuracy);
    }
}
