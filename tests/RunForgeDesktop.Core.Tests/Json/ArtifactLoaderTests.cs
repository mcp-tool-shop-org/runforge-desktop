using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests.Json;

public class ArtifactLoaderTests
{
    [Fact]
    public void Load_NonexistentFile_ReturnsNotFoundError()
    {
        // Arrange
        var fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent.json");

        // Act
        var result = ArtifactLoader.Load<TrainingMetrics>(fakePath);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ArtifactErrorType.NotFound, result.Error.Type);
        Assert.Equal(fakePath, result.Error.FilePath);
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedError()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "{ this is not valid json }");

            // Act
            var result = ArtifactLoader.Load<TrainingMetrics>(tempFile);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Error);
            Assert.Equal(ArtifactErrorType.MalformedJson, result.Error.Type);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_ValidJson_ReturnsSuccess()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var json = """
        {
            "accuracy": 0.95,
            "num_samples": 1000,
            "num_features": 10
        }
        """;

        try
        {
            File.WriteAllText(tempFile, json);

            // Act
            var result = ArtifactLoader.Load<TrainingMetrics>(tempFile);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(0.95, result.Value.Accuracy);
            Assert.Equal(1000, result.Value.NumSamples);
            Assert.Equal(10, result.Value.NumFeatures);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadAsync_ValidJson_ReturnsSuccess()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var json = """
        {
            "accuracy": 0.87,
            "num_samples": 500,
            "num_features": 5
        }
        """;

        try
        {
            await File.WriteAllTextAsync(tempFile, json);

            // Act
            var result = await ArtifactLoader.LoadAsync<TrainingMetrics>(tempFile);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(0.87, result.Value.Accuracy);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadAsync_NonexistentFile_ReturnsNotFoundError()
    {
        // Arrange
        var fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent.json");

        // Act
        var result = await ArtifactLoader.LoadAsync<TrainingMetrics>(fakePath);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ArtifactErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void Load_EmptyJson_ReturnsMalformedError()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "");

            // Act
            var result = ArtifactLoader.Load<TrainingMetrics>(tempFile);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Error);
            Assert.Equal(ArtifactErrorType.MalformedJson, result.Error.Type);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_NullJson_ReturnsMalformedError()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "null");

            // Act
            var result = ArtifactLoader.Load<TrainingMetrics>(tempFile);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Error);
            Assert.Equal(ArtifactErrorType.MalformedJson, result.Error.Type);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
