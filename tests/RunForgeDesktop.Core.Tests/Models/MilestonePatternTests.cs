using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests.Models;

/// <summary>
/// Tests for MilestonePatterns detection.
/// </summary>
public class MilestonePatternTests
{
    #region DetectMilestone Tests

    [Theory]
    [InlineData("Starting training run", MilestoneType.Starting)]
    [InlineData("Initializing model parameters", MilestoneType.Starting)]
    [InlineData("Run ID: 20260201-120000-test-abcd", MilestoneType.Starting)]
    [InlineData("Beginning execution", MilestoneType.Starting)]
    public void DetectMilestone_Starting_DetectsCorrectly(string logLine, MilestoneType expected)
    {
        // Act
        var result = MilestonePatterns.DetectMilestone(logLine);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Loading dataset from data/iris.csv", MilestoneType.LoadingDataset)]
    [InlineData("Reading data files...", MilestoneType.LoadingDataset)]
    [InlineData("Dataset: /path/to/data.csv", MilestoneType.LoadingDataset)]
    [InlineData("Loading CSV file", MilestoneType.LoadingDataset)]
    [InlineData("Loaded 150 rows from dataset", MilestoneType.LoadingDataset)]
    [InlineData("Loaded 1000 samples", MilestoneType.LoadingDataset)]
    public void DetectMilestone_LoadingDataset_DetectsCorrectly(string logLine, MilestoneType expected)
    {
        // Act
        var result = MilestonePatterns.DetectMilestone(logLine);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Epoch 1/10", MilestoneType.Training)]
    [InlineData("Training started", MilestoneType.Training)]
    [InlineData("Training model with parameters", MilestoneType.Training)]
    [InlineData("fit() called with 150 samples", MilestoneType.Training)]
    [InlineData("Fitting model...", MilestoneType.Training)]
    [InlineData("Train started", MilestoneType.Training)]
    [InlineData("Training begin", MilestoneType.Training)]
    public void DetectMilestone_Training_DetectsCorrectly(string logLine, MilestoneType expected)
    {
        // Act
        var result = MilestonePatterns.DetectMilestone(logLine);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Evaluating model performance", MilestoneType.Evaluating)]
    [InlineData("Validation accuracy: 0.95", MilestoneType.Evaluating)]
    [InlineData("Computing test score", MilestoneType.Evaluating)]
    [InlineData("Scoring predictions", MilestoneType.Evaluating)]
    [InlineData("Predicting on test set", MilestoneType.Evaluating)]
    [InlineData("Testing accuracy: 94%", MilestoneType.Evaluating)]
    [InlineData("Confusion matrix computed", MilestoneType.Evaluating)]
    public void DetectMilestone_Evaluating_DetectsCorrectly(string logLine, MilestoneType expected)
    {
        // Act
        var result = MilestonePatterns.DetectMilestone(logLine);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Saving model to disk", MilestoneType.WritingArtifacts)]
    [InlineData("Model saved to artifacts/model.pkl", MilestoneType.WritingArtifacts)]
    [InlineData("Wrote metrics.json", MilestoneType.WritingArtifacts)]
    [InlineData("Writing output files", MilestoneType.WritingArtifacts)]
    [InlineData("Artifact generated: confusion_matrix.png", MilestoneType.WritingArtifacts)]
    [InlineData("Exporting results", MilestoneType.WritingArtifacts)]
    [InlineData("result.json saved", MilestoneType.WritingArtifacts)]
    public void DetectMilestone_WritingArtifacts_DetectsCorrectly(string logLine, MilestoneType expected)
    {
        // Act
        var result = MilestonePatterns.DetectMilestone(logLine);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Training complete!", MilestoneType.Completed)]
    [InlineData("Run finished successfully", MilestoneType.Completed)]
    [InlineData("Done.", MilestoneType.Completed)]
    [InlineData("Completed in 5.2 seconds", MilestoneType.Completed)]
    [InlineData("Successfully trained model", MilestoneType.Completed)]
    public void DetectMilestone_Completed_DetectsCorrectly(string logLine, MilestoneType expected)
    {
        // Act
        var result = MilestonePatterns.DetectMilestone(logLine);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Error: Out of memory", MilestoneType.Failed)]
    [InlineData("Training failed with exception", MilestoneType.Failed)]
    [InlineData("Fatal error occurred", MilestoneType.Failed)]
    [InlineData("Process crashed unexpectedly", MilestoneType.Failed)]
    [InlineData("Run aborted by user", MilestoneType.Failed)]
    public void DetectMilestone_Failed_DetectsCorrectly(string logLine, MilestoneType expected)
    {
        // Act
        var result = MilestonePatterns.DetectMilestone(logLine);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DetectMilestone_EmptyOrWhitespace_ReturnsNull(string? logLine)
    {
        // Act
        var result = MilestonePatterns.DetectMilestone(logLine!);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("Processing feature X")]
    [InlineData("Feature importance calculated")]
    [InlineData("Memory usage: 512MB")]
    [InlineData("Using 4 CPU cores")]
    public void DetectMilestone_UnrelatedLines_ReturnsNull(string logLine)
    {
        // Act
        var result = MilestonePatterns.DetectMilestone(logLine);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region ExtractEpochProgress Tests

    [Theory]
    [InlineData("Epoch 1/10", 1, 10)]
    [InlineData("Epoch 5/20: loss=0.123", 5, 20)]
    [InlineData("epoch: 3/100", 3, 100)]
    [InlineData("EPOCH 15/50 - accuracy: 0.95", 15, 50)]
    public void ExtractEpochProgress_WithTotal_ExtractsCorrectly(string logLine, int expectedCurrent, int expectedTotal)
    {
        // Act
        var result = MilestonePatterns.ExtractEpochProgress(logLine);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedCurrent, result.Value.Current);
        Assert.Equal(expectedTotal, result.Value.Total);
    }

    [Theory]
    [InlineData("Epoch 1 of 10", 1, 10)]
    [InlineData("Epoch 5 of 20", 5, 20)]
    public void ExtractEpochProgress_WithOfSyntax_ExtractsCorrectly(string logLine, int expectedCurrent, int expectedTotal)
    {
        // Act
        var result = MilestonePatterns.ExtractEpochProgress(logLine);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedCurrent, result.Value.Current);
        Assert.Equal(expectedTotal, result.Value.Total);
    }

    [Theory]
    [InlineData("Epoch 5", 5, 0)]
    [InlineData("epoch: 10", 10, 0)]
    public void ExtractEpochProgress_WithoutTotal_ExtractsCurrentOnly(string logLine, int expectedCurrent, int expectedTotal)
    {
        // Act
        var result = MilestonePatterns.ExtractEpochProgress(logLine);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedCurrent, result.Value.Current);
        Assert.Equal(expectedTotal, result.Value.Total);
    }

    [Theory]
    [InlineData("Training model")]
    [InlineData("Accuracy: 0.95")]
    [InlineData("Processing batch")]
    public void ExtractEpochProgress_NoEpoch_ReturnsNull(string logLine)
    {
        // Act
        var result = MilestonePatterns.ExtractEpochProgress(logLine);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CreateDefaultMilestones Tests

    [Fact]
    public void CreateDefaultMilestones_ReturnsExpectedMilestones()
    {
        // Act
        var milestones = MilestonePatterns.CreateDefaultMilestones();

        // Assert
        Assert.True(milestones.Count >= 6);
        Assert.Equal(MilestoneType.Starting, milestones[0].Type);
        Assert.Equal(MilestoneType.LoadingDataset, milestones[1].Type);
        Assert.Equal(MilestoneType.Training, milestones[2].Type);
        Assert.Equal(MilestoneType.Evaluating, milestones[3].Type);
        Assert.Equal(MilestoneType.WritingArtifacts, milestones[4].Type);
        Assert.Equal(MilestoneType.Completed, milestones[5].Type);
    }

    [Fact]
    public void CreateDefaultMilestones_AllHaveNames()
    {
        // Act
        var milestones = MilestonePatterns.CreateDefaultMilestones();

        // Assert
        Assert.All(milestones, m => Assert.False(string.IsNullOrEmpty(m.Name)));
    }

    [Fact]
    public void CreateDefaultMilestones_NoneReached()
    {
        // Act
        var milestones = MilestonePatterns.CreateDefaultMilestones();

        // Assert
        Assert.All(milestones, m => Assert.False(m.IsReached));
    }

    #endregion
}
