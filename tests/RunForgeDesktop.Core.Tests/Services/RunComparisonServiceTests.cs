using System.Text.Json;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.Core.Tests.Services;

public class RunComparisonServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly RunRequestService _requestService;
    private readonly RunRequestComparer _requestComparer;
    private readonly RunDetailService _detailService;
    private readonly RunComparisonService _comparisonService;

    public RunComparisonServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"runforge_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _requestService = new RunRequestService();
        _requestComparer = new RunRequestComparer(_requestService);
        _detailService = new RunDetailService();
        _comparisonService = new RunComparisonService(_requestService, _requestComparer, _detailService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string CreateRunDirectory(string runId)
    {
        var runDir = Path.Combine(_tempDir, ".ml", "runs", runId);
        Directory.CreateDirectory(runDir);
        return runDir;
    }

    private void WriteRequest(string runDir, RunRequest request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(runDir, "request.json"), json);
    }

    private void WriteResult(string runDir, RunResult result)
    {
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(runDir, "result.json"), json);
    }

    private static RunRequest CreateBaseRequest(string? rerunFrom = null) => new()
    {
        Version = 1,
        Preset = "balanced",
        Dataset = new RunRequestDataset
        {
            Path = "data/train.csv",
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
        CreatedBy = "test@1.0",
        RerunFrom = rerunFrom
    };

    private static RunResult CreateSuccessResult(double accuracy = 0.85) => new()
    {
        Version = 1,
        Status = "succeeded",
        DurationMs = 5000,
        Summary = new ResultSummary
        {
            PrimaryMetric = new PrimaryMetric { Name = "accuracy", Value = accuracy },
            Metrics = new Dictionary<string, double>
            {
                ["accuracy"] = accuracy,
                ["f1_score"] = accuracy - 0.02,
                ["precision"] = accuracy + 0.01,
                ["recall"] = accuracy - 0.01
            }
        },
        EffectiveConfig = new EffectiveConfig
        {
            Preset = "balanced",
            Model = new EffectiveModelConfig { Family = "logistic_regression" },
            Device = new EffectiveDeviceConfig { Type = "cpu" },
            Dataset = new EffectiveDatasetConfig { Path = "data/train.csv", LabelColumn = "target" }
        },
        Artifacts = new List<ArtifactInfo>
        {
            new() { Path = "model.pkl", Type = "model", Bytes = 1024 },
            new() { Path = "metrics.json", Type = "metrics", Bytes = 256 }
        }
    };

    #region CompareWithParentAsync Tests

    [Fact]
    public async Task CompareWithParentAsync_NoRerunFrom_ReturnsNotARerunError()
    {
        // Arrange
        var childDir = CreateRunDirectory("child-001");
        WriteRequest(childDir, CreateBaseRequest(rerunFrom: null));

        // Act
        var result = await _comparisonService.CompareWithParentAsync(
            _tempDir,
            ".ml/runs/child-001");

        // Assert
        Assert.False(result.IsComplete);
        Assert.Contains("not a rerun", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompareWithParentAsync_ParentNotFound_ReturnsError()
    {
        // Arrange
        var childDir = CreateRunDirectory("child-001");
        WriteRequest(childDir, CreateBaseRequest(rerunFrom: "parent-missing"));
        WriteResult(childDir, CreateSuccessResult());

        // Act
        var result = await _comparisonService.CompareWithParentAsync(
            _tempDir,
            ".ml/runs/child-001");

        // Assert
        Assert.False(result.IsComplete);
        Assert.Equal("parent-missing", result.ParentRunId);
        Assert.Equal("child-001", result.ChildRunId);
    }

    [Fact]
    public async Task CompareWithParentAsync_BothRunsExist_ReturnsComparison()
    {
        // Arrange
        var parentDir = CreateRunDirectory("parent-001");
        var childDir = CreateRunDirectory("child-001");

        WriteRequest(parentDir, CreateBaseRequest());
        WriteResult(parentDir, CreateSuccessResult(accuracy: 0.80));

        WriteRequest(childDir, CreateBaseRequest(rerunFrom: "parent-001") with
        {
            Preset = "thorough"
        });
        WriteResult(childDir, CreateSuccessResult(accuracy: 0.85));

        // Act
        var result = await _comparisonService.CompareWithParentAsync(
            _tempDir,
            ".ml/runs/child-001");

        // Assert
        Assert.True(result.IsComplete);
        Assert.Equal("parent-001", result.ParentRunId);
        Assert.Equal("child-001", result.ChildRunId);
        Assert.NotNull(result.Results);
    }

    #endregion

    #region CompareAsync Tests

    [Fact]
    public async Task CompareAsync_BothSucceeded_ReturnsCompleteComparison()
    {
        // Arrange
        var parentDir = CreateRunDirectory("parent-001");
        var childDir = CreateRunDirectory("child-001");

        WriteRequest(parentDir, CreateBaseRequest());
        WriteResult(parentDir, CreateSuccessResult(accuracy: 0.80));

        WriteRequest(childDir, CreateBaseRequest(rerunFrom: "parent-001"));
        WriteResult(childDir, CreateSuccessResult(accuracy: 0.85));

        // Act
        var result = await _comparisonService.CompareAsync(
            _tempDir,
            ".ml/runs/parent-001",
            ".ml/runs/child-001");

        // Assert
        Assert.True(result.IsComplete);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CompareAsync_ResultsComparison_CalculatesMetricDeltas()
    {
        // Arrange
        var parentDir = CreateRunDirectory("parent-001");
        var childDir = CreateRunDirectory("child-001");

        WriteRequest(parentDir, CreateBaseRequest());
        WriteResult(parentDir, CreateSuccessResult(accuracy: 0.80));

        WriteRequest(childDir, CreateBaseRequest(rerunFrom: "parent-001"));
        WriteResult(childDir, CreateSuccessResult(accuracy: 0.90));

        // Act
        var result = await _comparisonService.CompareAsync(
            _tempDir,
            ".ml/runs/parent-001",
            ".ml/runs/child-001");

        // Assert
        Assert.NotNull(result.Results);
        Assert.NotNull(result.Results.PrimaryMetric);
        Assert.Equal("accuracy", result.Results.PrimaryMetric.Name);
        Assert.True(result.Results.PrimaryMetric.IsPrimary);

        // 0.90 - 0.80 = 0.10 delta
        Assert.NotNull(result.Results.PrimaryMetric.Delta);
        Assert.True(result.Results.PrimaryMetric.Delta > 0);
        Assert.Equal("improved", result.Results.PrimaryMetric.Severity);
    }

    [Fact]
    public async Task CompareAsync_ResultsComparison_CalculatesDurationDelta()
    {
        // Arrange
        var parentDir = CreateRunDirectory("parent-001");
        var childDir = CreateRunDirectory("child-001");

        WriteRequest(parentDir, CreateBaseRequest());
        WriteResult(parentDir, CreateSuccessResult() with { DurationMs = 10000 });

        WriteRequest(childDir, CreateBaseRequest(rerunFrom: "parent-001"));
        WriteResult(childDir, CreateSuccessResult() with { DurationMs = 8000 });

        // Act
        var result = await _comparisonService.CompareAsync(
            _tempDir,
            ".ml/runs/parent-001",
            ".ml/runs/child-001");

        // Assert
        Assert.NotNull(result.Results);
        Assert.Equal(10000, result.Results.ParentDurationMs);
        Assert.Equal(8000, result.Results.ChildDurationMs);
        Assert.Equal(-2000, result.Results.DurationDeltaMs); // Child is faster
    }

    [Fact]
    public async Task CompareAsync_RequestDifferences_DetectsPresetChange()
    {
        // Arrange
        var parentDir = CreateRunDirectory("parent-001");
        var childDir = CreateRunDirectory("child-001");

        WriteRequest(parentDir, CreateBaseRequest() with { Preset = "balanced" });
        WriteResult(parentDir, CreateSuccessResult());

        WriteRequest(childDir, CreateBaseRequest(rerunFrom: "parent-001") with { Preset = "thorough" });
        WriteResult(childDir, CreateSuccessResult());

        // Act
        var result = await _comparisonService.CompareAsync(
            _tempDir,
            ".ml/runs/parent-001",
            ".ml/runs/child-001");

        // Assert
        Assert.True(result.HasRequestDifferences);
        Assert.Contains(result.RequestDifferences, d => d.Field == "preset");
    }

    [Fact]
    public async Task CompareAsync_EffectiveConfigDifferences_DetectsModelChange()
    {
        // Arrange
        var parentDir = CreateRunDirectory("parent-001");
        var childDir = CreateRunDirectory("child-001");

        WriteRequest(parentDir, CreateBaseRequest());
        WriteResult(parentDir, CreateSuccessResult() with
        {
            EffectiveConfig = new EffectiveConfig
            {
                Preset = "balanced",
                Model = new EffectiveModelConfig { Family = "logistic_regression" },
                Device = new EffectiveDeviceConfig { Type = "cpu" }
            }
        });

        WriteRequest(childDir, CreateBaseRequest(rerunFrom: "parent-001"));
        WriteResult(childDir, CreateSuccessResult() with
        {
            EffectiveConfig = new EffectiveConfig
            {
                Preset = "balanced",
                Model = new EffectiveModelConfig { Family = "random_forest" },
                Device = new EffectiveDeviceConfig { Type = "cpu" }
            }
        });

        // Act
        var result = await _comparisonService.CompareAsync(
            _tempDir,
            ".ml/runs/parent-001",
            ".ml/runs/child-001");

        // Assert
        Assert.True(result.HasEffectiveConfigDifferences);
        var modelDiff = result.EffectiveConfigDifferences.FirstOrDefault(d => d.Field == "model.family");
        Assert.NotNull(modelDiff);
        Assert.Equal("logistic_regression", modelDiff.ParentValue);
        Assert.Equal("random_forest", modelDiff.CurrentValue);
    }

    [Fact]
    public async Task CompareAsync_ArtifactComparison_DetectsAddedArtifacts()
    {
        // Arrange
        var parentDir = CreateRunDirectory("parent-001");
        var childDir = CreateRunDirectory("child-001");

        WriteRequest(parentDir, CreateBaseRequest());
        WriteResult(parentDir, CreateSuccessResult() with
        {
            Artifacts = new List<ArtifactInfo>
            {
                new() { Path = "model.pkl", Type = "model", Bytes = 1024 }
            }
        });

        WriteRequest(childDir, CreateBaseRequest(rerunFrom: "parent-001"));
        WriteResult(childDir, CreateSuccessResult() with
        {
            Artifacts = new List<ArtifactInfo>
            {
                new() { Path = "model.pkl", Type = "model", Bytes = 1024 },
                new() { Path = "encoder.pkl", Type = "encoder", Bytes = 512 }
            }
        });

        // Act
        var result = await _comparisonService.CompareAsync(
            _tempDir,
            ".ml/runs/parent-001",
            ".ml/runs/child-001");

        // Assert
        Assert.NotNull(result.Artifacts);
        Assert.True(result.Artifacts.HasChanges);
        Assert.Single(result.Artifacts.AddedInChild);
        Assert.Equal("encoder.pkl", result.Artifacts.AddedInChild[0].Path);
    }

    [Fact]
    public async Task CompareAsync_ArtifactComparison_DetectsRemovedArtifacts()
    {
        // Arrange
        var parentDir = CreateRunDirectory("parent-001");
        var childDir = CreateRunDirectory("child-001");

        WriteRequest(parentDir, CreateBaseRequest());
        WriteResult(parentDir, CreateSuccessResult() with
        {
            Artifacts = new List<ArtifactInfo>
            {
                new() { Path = "model.pkl", Type = "model", Bytes = 1024 },
                new() { Path = "debug.log", Type = "log", Bytes = 2048 }
            }
        });

        WriteRequest(childDir, CreateBaseRequest(rerunFrom: "parent-001"));
        WriteResult(childDir, CreateSuccessResult() with
        {
            Artifacts = new List<ArtifactInfo>
            {
                new() { Path = "model.pkl", Type = "model", Bytes = 1024 }
            }
        });

        // Act
        var result = await _comparisonService.CompareAsync(
            _tempDir,
            ".ml/runs/parent-001",
            ".ml/runs/child-001");

        // Assert
        Assert.NotNull(result.Artifacts);
        Assert.True(result.Artifacts.HasChanges);
        Assert.Single(result.Artifacts.RemovedFromParent);
        Assert.Equal("debug.log", result.Artifacts.RemovedFromParent[0].Path);
    }

    [Fact]
    public async Task CompareAsync_ArtifactComparison_IdentifiesCommonArtifacts()
    {
        // Arrange
        var parentDir = CreateRunDirectory("parent-001");
        var childDir = CreateRunDirectory("child-001");

        WriteRequest(parentDir, CreateBaseRequest());
        WriteResult(parentDir, CreateSuccessResult() with
        {
            Artifacts = new List<ArtifactInfo>
            {
                new() { Path = "model.pkl", Type = "model", Bytes = 1024 },
                new() { Path = "metrics.json", Type = "metrics", Bytes = 256 }
            }
        });

        WriteRequest(childDir, CreateBaseRequest(rerunFrom: "parent-001"));
        WriteResult(childDir, CreateSuccessResult() with
        {
            Artifacts = new List<ArtifactInfo>
            {
                new() { Path = "model.pkl", Type = "model", Bytes = 2048 },
                new() { Path = "metrics.json", Type = "metrics", Bytes = 512 }
            }
        });

        // Act
        var result = await _comparisonService.CompareAsync(
            _tempDir,
            ".ml/runs/parent-001",
            ".ml/runs/child-001");

        // Assert
        Assert.NotNull(result.Artifacts);
        Assert.False(result.Artifacts.HasChanges); // No added/removed
        Assert.Equal(2, result.Artifacts.Common.Count);

        var modelPair = result.Artifacts.Common.FirstOrDefault(p => p.Child.Path == "model.pkl");
        Assert.NotNull(modelPair);
        Assert.Equal(1024, modelPair.SizeDelta); // 2048 - 1024
        Assert.True(modelPair.SizeChanged);
    }

    #endregion

    #region MetricDelta Tests

    [Fact]
    public void MetricDelta_Severity_Improved_WhenPositiveDelta()
    {
        var delta = new MetricDelta
        {
            Name = "accuracy",
            ParentValue = 0.80,
            ChildValue = 0.90
        };

        Assert.Equal("improved", delta.Severity);
    }

    [Fact]
    public void MetricDelta_Severity_Degraded_WhenNegativeDelta()
    {
        var delta = new MetricDelta
        {
            Name = "accuracy",
            ParentValue = 0.90,
            ChildValue = 0.80
        };

        Assert.Equal("degraded", delta.Severity);
    }

    [Fact]
    public void MetricDelta_Severity_Unchanged_WhenNoDelta()
    {
        var delta = new MetricDelta
        {
            Name = "accuracy",
            ParentValue = 0.85,
            ChildValue = 0.85
        };

        Assert.Equal("unchanged", delta.Severity);
    }

    [Fact]
    public void MetricDelta_Severity_Unknown_WhenMissingValue()
    {
        var delta = new MetricDelta
        {
            Name = "accuracy",
            ParentValue = null,
            ChildValue = 0.85
        };

        Assert.Equal("unknown", delta.Severity);
    }

    [Fact]
    public void MetricDelta_FormattedValues_Percentage()
    {
        var delta = new MetricDelta
        {
            Name = "accuracy",
            ParentValue = 0.80,
            ChildValue = 0.85
        };

        Assert.Equal("80.00%", delta.ParentValueFormatted);
        Assert.Equal("85.00%", delta.ChildValueFormatted);
        Assert.Equal("+5.00%", delta.DeltaFormatted);
    }

    [Fact]
    public void MetricDelta_DisplayName_MapsMetricNames()
    {
        Assert.Equal("Accuracy", new MetricDelta { Name = "accuracy", ParentValue = 0, ChildValue = 0 }.DisplayName);
        Assert.Equal("F1 Score", new MetricDelta { Name = "f1_score", ParentValue = 0, ChildValue = 0 }.DisplayName);
        Assert.Equal("Precision", new MetricDelta { Name = "precision", ParentValue = 0, ChildValue = 0 }.DisplayName);
        Assert.Equal("Recall", new MetricDelta { Name = "recall", ParentValue = 0, ChildValue = 0 }.DisplayName);
        Assert.Equal("custom_metric", new MetricDelta { Name = "custom_metric", ParentValue = 0, ChildValue = 0 }.DisplayName);
    }

    #endregion

    #region ResultsComparison Tests

    [Fact]
    public void ResultsComparison_BothSucceeded_True()
    {
        var results = new ResultsComparison
        {
            ParentStatus = "succeeded",
            ChildStatus = "succeeded",
            ParentDurationMs = 5000,
            ChildDurationMs = 4000
        };

        Assert.True(results.BothSucceeded);
    }

    [Fact]
    public void ResultsComparison_BothSucceeded_FalseWhenParentFailed()
    {
        var results = new ResultsComparison
        {
            ParentStatus = "failed",
            ChildStatus = "succeeded",
            ParentDurationMs = 5000,
            ChildDurationMs = 4000
        };

        Assert.False(results.BothSucceeded);
    }

    [Fact]
    public void ResultsComparison_DurationDeltaPercent_Calculation()
    {
        var results = new ResultsComparison
        {
            ParentStatus = "succeeded",
            ChildStatus = "succeeded",
            ParentDurationMs = 10000,
            ChildDurationMs = 8000
        };

        Assert.Equal(-20.0, results.DurationDeltaPercent);
    }

    [Fact]
    public void ResultsComparison_FormattedDuration_Seconds()
    {
        var results = new ResultsComparison
        {
            ParentStatus = "succeeded",
            ChildStatus = "succeeded",
            ParentDurationMs = 5000,
            ChildDurationMs = 8000
        };

        Assert.Equal("5.0s", results.ParentDurationFormatted);
        Assert.Equal("8.0s", results.ChildDurationFormatted);
        Assert.Equal("+3.0s", results.DurationDeltaFormatted);
    }

    [Fact]
    public void ResultsComparison_FormattedDuration_Minutes()
    {
        var results = new ResultsComparison
        {
            ParentStatus = "succeeded",
            ChildStatus = "succeeded",
            ParentDurationMs = 120000, // 2 min
            ChildDurationMs = 180000  // 3 min
        };

        Assert.Equal("2.0m", results.ParentDurationFormatted);
        Assert.Equal("3.0m", results.ChildDurationFormatted);
    }

    #endregion

    #region ArtifactComparison Tests

    [Fact]
    public void ArtifactComparison_ParentCount_IncludesCommonAndRemoved()
    {
        var comparison = new ArtifactComparison
        {
            Common = new List<ArtifactPair>
            {
                new() { Parent = new ArtifactInfo { Path = "a", Type = "t", Bytes = 0 }, Child = new ArtifactInfo { Path = "a", Type = "t", Bytes = 0 } },
                new() { Parent = new ArtifactInfo { Path = "b", Type = "t", Bytes = 0 }, Child = new ArtifactInfo { Path = "b", Type = "t", Bytes = 0 } }
            },
            RemovedFromParent = new List<ArtifactInfo>
            {
                new() { Path = "c", Type = "t", Bytes = 0 }
            },
            AddedInChild = new List<ArtifactInfo>()
        };

        Assert.Equal(3, comparison.ParentCount);
    }

    [Fact]
    public void ArtifactComparison_ChildCount_IncludesCommonAndAdded()
    {
        var comparison = new ArtifactComparison
        {
            Common = new List<ArtifactPair>
            {
                new() { Parent = new ArtifactInfo { Path = "a", Type = "t", Bytes = 0 }, Child = new ArtifactInfo { Path = "a", Type = "t", Bytes = 0 } }
            },
            AddedInChild = new List<ArtifactInfo>
            {
                new() { Path = "d", Type = "t", Bytes = 0 },
                new() { Path = "e", Type = "t", Bytes = 0 }
            },
            RemovedFromParent = new List<ArtifactInfo>()
        };

        Assert.Equal(3, comparison.ChildCount);
    }

    #endregion

    #region ArtifactPair Tests

    [Fact]
    public void ArtifactPair_SizeDelta_Calculation()
    {
        var pair = new ArtifactPair
        {
            Parent = new ArtifactInfo { Path = "model.pkl", Type = "model", Bytes = 1000 },
            Child = new ArtifactInfo { Path = "model.pkl", Type = "model", Bytes = 1500 }
        };

        Assert.Equal(500, pair.SizeDelta);
        Assert.True(pair.SizeChanged);
    }

    [Fact]
    public void ArtifactPair_SizeChanged_FalseWhenSmallDifference()
    {
        var pair = new ArtifactPair
        {
            Parent = new ArtifactInfo { Path = "model.pkl", Type = "model", Bytes = 1000 },
            Child = new ArtifactInfo { Path = "model.pkl", Type = "model", Bytes = 1050 }
        };

        Assert.False(pair.SizeChanged); // Less than 100 bytes
    }

    #endregion
}
