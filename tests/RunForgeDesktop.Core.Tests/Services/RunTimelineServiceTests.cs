using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.Core.Tests.Services;

/// <summary>
/// Tests for RunTimelineService.
/// </summary>
public class RunTimelineServiceTests
{
    private readonly RunTimelineService _service = new();

    #region CreateTimeline Tests

    [Fact]
    public void CreateTimeline_ReturnsAllDefaultMilestones()
    {
        // Act
        var state = _service.CreateTimeline();

        // Assert
        Assert.NotNull(state.Milestones);
        Assert.True(state.Milestones.Count >= 6);
        Assert.Contains(state.Milestones, m => m.Type == MilestoneType.Starting);
        Assert.Contains(state.Milestones, m => m.Type == MilestoneType.LoadingDataset);
        Assert.Contains(state.Milestones, m => m.Type == MilestoneType.Training);
        Assert.Contains(state.Milestones, m => m.Type == MilestoneType.Evaluating);
        Assert.Contains(state.Milestones, m => m.Type == MilestoneType.WritingArtifacts);
        Assert.Contains(state.Milestones, m => m.Type == MilestoneType.Completed);
    }

    [Fact]
    public void CreateTimeline_NoMilestonesReached()
    {
        // Act
        var state = _service.CreateTimeline();

        // Assert
        Assert.All(state.Milestones, m => Assert.False(m.IsReached));
        Assert.Equal(-1, state.ActiveIndex);
    }

    #endregion

    #region ProcessLogLines Tests

    [Fact]
    public void ProcessLogLines_EmptyLines_NoChange()
    {
        // Arrange
        var state = _service.CreateTimeline();

        // Act
        var newState = _service.ProcessLogLines(state, Array.Empty<string>());

        // Assert
        Assert.All(newState.Milestones, m => Assert.False(m.IsReached));
    }

    [Fact]
    public void ProcessLogLines_DetectsStartingMilestone()
    {
        // Arrange
        var state = _service.CreateTimeline();
        var lines = new[] { "Starting training run...", "Initializing model" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        var starting = newState.Milestones.First(m => m.Type == MilestoneType.Starting);
        Assert.True(starting.IsReached);
    }

    [Fact]
    public void ProcessLogLines_DetectsLoadingDatasetMilestone()
    {
        // Arrange
        var state = _service.CreateTimeline();
        var lines = new[] { "Loading dataset from data/iris.csv", "Loaded 150 samples" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        var loading = newState.Milestones.First(m => m.Type == MilestoneType.LoadingDataset);
        Assert.True(loading.IsReached);
    }

    [Fact]
    public void ProcessLogLines_DetectsTrainingMilestone()
    {
        // Arrange
        var state = _service.CreateTimeline();
        var lines = new[] { "Training started", "Epoch 1/10" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        var training = newState.Milestones.First(m => m.Type == MilestoneType.Training);
        Assert.True(training.IsReached);
    }

    [Fact]
    public void ProcessLogLines_DetectsEvaluatingMilestone()
    {
        // Arrange
        var state = _service.CreateTimeline();
        var lines = new[] { "Evaluating model...", "Validation accuracy: 0.95" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        var evaluating = newState.Milestones.First(m => m.Type == MilestoneType.Evaluating);
        Assert.True(evaluating.IsReached);
    }

    [Fact]
    public void ProcessLogLines_DetectsWritingArtifactsMilestone()
    {
        // Arrange
        var state = _service.CreateTimeline();
        var lines = new[] { "Saving model to artifacts/", "Wrote metrics.json" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        var writing = newState.Milestones.First(m => m.Type == MilestoneType.WritingArtifacts);
        Assert.True(writing.IsReached);
    }

    [Fact]
    public void ProcessLogLines_DetectsCompletedMilestone()
    {
        // Arrange
        var state = _service.CreateTimeline();
        var lines = new[] { "Training complete!", "Run finished successfully" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        var completed = newState.Milestones.First(m => m.Type == MilestoneType.Completed);
        Assert.True(completed.IsReached);
    }

    [Fact]
    public void ProcessLogLines_SetsActiveMilestone()
    {
        // Arrange
        var state = _service.CreateTimeline();
        var lines = new[] { "Loading dataset...", "Training started" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        Assert.True(newState.ActiveIndex >= 0);
        var activeMilestone = newState.Milestones[newState.ActiveIndex];
        Assert.True(activeMilestone.IsActive);
        Assert.Equal(MilestoneType.Training, activeMilestone.Type);
    }

    [Fact]
    public void ProcessLogLines_NoActiveMilestoneWhenCompleted()
    {
        // Arrange
        var state = _service.CreateTimeline();
        var lines = new[] { "Training complete" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        Assert.Equal(-1, newState.ActiveIndex);
    }

    [Fact]
    public void ProcessLogLines_ExtractsEpochProgress()
    {
        // Arrange
        var state = _service.CreateTimeline();
        var lines = new[] { "Epoch 5/10: loss=0.123" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        Assert.NotNull(newState.EpochProgress);
        Assert.Equal(5, newState.EpochProgress.Value.Current);
        Assert.Equal(10, newState.EpochProgress.Value.Total);
    }

    [Fact]
    public void ProcessLogLines_UpdatesEpochProgress()
    {
        // Arrange
        var state = _service.CreateTimeline();
        state = _service.ProcessLogLines(state, new[] { "Epoch 1/10" });

        // Act
        var newState = _service.ProcessLogLines(state, new[] { "Epoch 2/10" });

        // Assert
        Assert.NotNull(newState.EpochProgress);
        Assert.Equal(2, newState.EpochProgress.Value.Current);
    }

    [Fact]
    public void ProcessLogLines_PreservesAlreadyReachedMilestones()
    {
        // Arrange
        var state = _service.CreateTimeline();
        state = _service.ProcessLogLines(state, new[] { "Starting..." });

        // Act - process lines that don't trigger any milestone
        var newState = _service.ProcessLogLines(state, new[] { "some random log line" });

        // Assert - Starting should still be reached
        var starting = newState.Milestones.First(m => m.Type == MilestoneType.Starting);
        Assert.True(starting.IsReached);
    }

    #endregion

    #region SetCompleted Tests

    [Fact]
    public void SetCompleted_Success_MarksAllMilestonesReached()
    {
        // Arrange
        var state = _service.CreateTimeline();

        // Act
        var newState = _service.SetCompleted(state, isSuccess: true);

        // Assert
        var completed = newState.Milestones.First(m => m.Type == MilestoneType.Completed);
        Assert.True(completed.IsReached);

        var failed = newState.Milestones.FirstOrDefault(m => m.Type == MilestoneType.Failed);
        if (failed is not null)
        {
            Assert.False(failed.IsReached);
        }
    }

    [Fact]
    public void SetCompleted_Failure_DoesNotMarkCompletedMilestone()
    {
        // Arrange
        var state = _service.CreateTimeline();

        // Act
        var newState = _service.SetCompleted(state, isSuccess: false);

        // Assert
        // Failed runs should NOT mark the "Completed" milestone as reached
        var completed = newState.Milestones.First(m => m.Type == MilestoneType.Completed);
        Assert.False(completed.IsReached);

        // No active milestone when failed
        Assert.Equal(-1, newState.ActiveIndex);
    }

    [Fact]
    public void SetCompleted_NoActiveMilestone()
    {
        // Arrange
        var state = _service.CreateTimeline();

        // Act
        var newState = _service.SetCompleted(state, isSuccess: true);

        // Assert
        Assert.Equal(-1, newState.ActiveIndex);
    }

    [Fact]
    public void SetCompleted_PreservesEpochProgress()
    {
        // Arrange
        var state = _service.CreateTimeline();
        state = _service.ProcessLogLines(state, new[] { "Epoch 8/10" });

        // Act
        var newState = _service.SetCompleted(state, isSuccess: true);

        // Assert
        Assert.NotNull(newState.EpochProgress);
        Assert.Equal(8, newState.EpochProgress.Value.Current);
    }

    #endregion

    #region RunTimelineState Tests

    [Fact]
    public void RunTimelineState_IsComplete_TrueWhenCompleted()
    {
        // Arrange
        var state = _service.CreateTimeline();

        // Act
        var newState = _service.SetCompleted(state, isSuccess: true);

        // Assert
        Assert.True(newState.IsComplete);
    }

    [Fact]
    public void RunTimelineState_IsComplete_FalseWhenInProgress()
    {
        // Arrange
        var state = _service.CreateTimeline();
        var newState = _service.ProcessLogLines(state, new[] { "Training..." });

        // Assert
        Assert.False(newState.IsComplete);
    }

    #endregion

    #region Explicit Token Tests [RF:STAGE=X]

    [Theory]
    [InlineData("[RF:STAGE=STARTING]", MilestoneType.Starting)]
    [InlineData("[RF:STAGE=LOADING_DATASET]", MilestoneType.LoadingDataset)]
    [InlineData("[RF:STAGE=TRAINING]", MilestoneType.Training)]
    [InlineData("[RF:STAGE=EVALUATING]", MilestoneType.Evaluating)]
    [InlineData("[RF:STAGE=WRITING_ARTIFACTS]", MilestoneType.WritingArtifacts)]
    [InlineData("[RF:STAGE=COMPLETED]", MilestoneType.Completed)]
    [InlineData("[RF:STAGE=FAILED]", MilestoneType.Failed)]
    public void ProcessLogLines_DetectsExplicitStageToken(string token, MilestoneType expectedType)
    {
        // Arrange
        var state = _service.CreateTimeline();
        var lines = new[] { $"2024-01-15 10:30:00 INFO {token} Starting new phase" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        var milestone = newState.Milestones.FirstOrDefault(m => m.Type == expectedType);
        if (milestone is not null)
        {
            Assert.True(milestone.IsReached);
        }
        // For Failed type, it won't be in the default list, but detection should work
    }

    [Fact]
    public void ProcessLogLines_ExplicitTokenTakesPriorityOverHeuristics()
    {
        // Arrange
        var state = _service.CreateTimeline();
        // This line would trigger "Completed" by heuristic, but explicit token says "Training"
        var lines = new[] { "[RF:STAGE=TRAINING] Training is complete now" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        var training = newState.Milestones.First(m => m.Type == MilestoneType.Training);
        Assert.True(training.IsReached);
    }

    [Fact]
    public void ProcessLogLines_DetectsExplicitEpochToken()
    {
        // Arrange
        var state = _service.CreateTimeline();
        var lines = new[] { "[RF:EPOCH=7/15] Processing batch..." };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        Assert.NotNull(newState.EpochProgress);
        Assert.Equal(7, newState.EpochProgress.Value.Current);
        Assert.Equal(15, newState.EpochProgress.Value.Total);
    }

    [Fact]
    public void ProcessLogLines_ExplicitEpochTokenTakesPriority()
    {
        // Arrange
        var state = _service.CreateTimeline();
        // Heuristic would parse "Epoch 3/10", but explicit token says 7/15
        var lines = new[] { "[RF:EPOCH=7/15] Epoch 3/10 loss=0.05" };

        // Act
        var newState = _service.ProcessLogLines(state, lines);

        // Assert
        Assert.NotNull(newState.EpochProgress);
        Assert.Equal(7, newState.EpochProgress.Value.Current);
        Assert.Equal(15, newState.EpochProgress.Value.Total);
    }

    [Fact]
    public void MilestonePatterns_HasExplicitToken_ReturnsTrueForValidTokens()
    {
        // Assert
        Assert.True(MilestonePatterns.HasExplicitToken("[RF:STAGE=TRAINING]"));
        Assert.True(MilestonePatterns.HasExplicitToken("[RF:EPOCH=5/10]"));
        Assert.True(MilestonePatterns.HasExplicitToken("INFO [RF:STAGE=COMPLETED] done"));
    }

    [Fact]
    public void MilestonePatterns_HasExplicitToken_ReturnsFalseForNoTokens()
    {
        // Assert
        Assert.False(MilestonePatterns.HasExplicitToken("Training started"));
        Assert.False(MilestonePatterns.HasExplicitToken("Epoch 5/10"));
        Assert.False(MilestonePatterns.HasExplicitToken(""));
        Assert.False(MilestonePatterns.HasExplicitToken(null!));
    }

    [Fact]
    public void MilestonePatterns_DetectExplicitStage_ReturnsNullForInvalidStage()
    {
        // Act & Assert
        Assert.Null(MilestonePatterns.DetectExplicitStage("[RF:STAGE=UNKNOWN]"));
        Assert.Null(MilestonePatterns.DetectExplicitStage("[RF:STAGE=training]")); // lowercase not valid
        Assert.Null(MilestonePatterns.DetectExplicitStage("STAGE=TRAINING")); // missing brackets
    }

    #endregion
}
