using System.Text.Json;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.Core.Tests.Services;

public class RunRequestComparerTests
{
    private readonly RunRequestComparer _comparer;
    private readonly RunRequestService _requestService;

    public RunRequestComparerTests()
    {
        _requestService = new RunRequestService();
        _comparer = new RunRequestComparer(_requestService);
    }

    private static RunRequest CreateBaseRequest() => new()
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
        CreatedBy = "test@1.0"
    };

    [Fact]
    public void Compare_IdenticalRequests_ReturnsNoDifferences()
    {
        // Arrange
        var request1 = CreateBaseRequest();
        var request2 = CreateBaseRequest();

        // Act
        var differences = _comparer.Compare(request1, request2);

        // Assert
        Assert.Empty(differences);
    }

    [Fact]
    public void Compare_DifferentPreset_ReturnsOneDifference()
    {
        // Arrange
        var parent = CreateBaseRequest();
        var current = CreateBaseRequest() with { Preset = "thorough" };

        // Act
        var differences = _comparer.Compare(parent, current);

        // Assert
        Assert.Single(differences);
        Assert.Equal("preset", differences[0].Field);
        Assert.Equal("balanced", differences[0].ParentValue);
        Assert.Equal("thorough", differences[0].CurrentValue);
    }

    [Fact]
    public void Compare_DifferentModelFamily_ReturnsOneDifference()
    {
        // Arrange
        var parent = CreateBaseRequest();
        var current = CreateBaseRequest() with
        {
            Model = new RunRequestModel { Family = "random_forest" }
        };

        // Act
        var differences = _comparer.Compare(parent, current);

        // Assert
        Assert.Single(differences);
        Assert.Equal("model.family", differences[0].Field);
        Assert.Equal("logistic_regression", differences[0].ParentValue);
        Assert.Equal("random_forest", differences[0].CurrentValue);
    }

    [Fact]
    public void Compare_DifferentDeviceType_ReturnsOneDifference()
    {
        // Arrange
        var parent = CreateBaseRequest();
        var current = CreateBaseRequest() with
        {
            Device = new RunRequestDevice { Type = "gpu" }
        };

        // Act
        var differences = _comparer.Compare(parent, current);

        // Assert
        Assert.Single(differences);
        Assert.Equal("device.type", differences[0].Field);
        Assert.Equal("cpu", differences[0].ParentValue);
        Assert.Equal("gpu", differences[0].CurrentValue);
    }

    [Fact]
    public void Compare_DifferentDatasetPath_ReturnsOneDifference()
    {
        // Arrange
        var parent = CreateBaseRequest();
        var current = CreateBaseRequest() with
        {
            Dataset = new RunRequestDataset
            {
                Path = "data/new_train.csv",
                LabelColumn = "target"
            }
        };

        // Act
        var differences = _comparer.Compare(parent, current);

        // Assert
        Assert.Single(differences);
        Assert.Equal("dataset.path", differences[0].Field);
        Assert.Equal("data/train.csv", differences[0].ParentValue);
        Assert.Equal("data/new_train.csv", differences[0].CurrentValue);
    }

    [Fact]
    public void Compare_DifferentLabelColumn_ReturnsOneDifference()
    {
        // Arrange
        var parent = CreateBaseRequest();
        var current = CreateBaseRequest() with
        {
            Dataset = new RunRequestDataset
            {
                Path = "data/train.csv",
                LabelColumn = "class"
            }
        };

        // Act
        var differences = _comparer.Compare(parent, current);

        // Assert
        Assert.Single(differences);
        Assert.Equal("dataset.label_column", differences[0].Field);
        Assert.Equal("target", differences[0].ParentValue);
        Assert.Equal("class", differences[0].CurrentValue);
    }

    [Fact]
    public void Compare_MultipleFieldsDifferent_ReturnsAllDifferences()
    {
        // Arrange
        var parent = CreateBaseRequest();
        var current = CreateBaseRequest() with
        {
            Preset = "fast",
            Model = new RunRequestModel { Family = "linear_svc" },
            Device = new RunRequestDevice { Type = "gpu" }
        };

        // Act
        var differences = _comparer.Compare(parent, current);

        // Assert
        Assert.Equal(3, differences.Count);
        Assert.Contains(differences, d => d.Field == "preset");
        Assert.Contains(differences, d => d.Field == "model.family");
        Assert.Contains(differences, d => d.Field == "device.type");
    }

    [Fact]
    public void Compare_DifferentName_ReturnsOneDifference()
    {
        // Arrange
        var parent = CreateBaseRequest() with { Name = "Original Run" };
        var current = CreateBaseRequest() with { Name = "Modified Run" };

        // Act
        var differences = _comparer.Compare(parent, current);

        // Assert
        Assert.Single(differences);
        Assert.Equal("name", differences[0].Field);
        Assert.Equal("Original Run", differences[0].ParentValue);
        Assert.Equal("Modified Run", differences[0].CurrentValue);
    }

    [Fact]
    public void Compare_NullVsNonNullName_ReturnsOneDifference()
    {
        // Arrange
        var parent = CreateBaseRequest();
        var current = CreateBaseRequest() with { Name = "New Name" };

        // Act
        var differences = _comparer.Compare(parent, current);

        // Assert
        Assert.Single(differences);
        Assert.Equal("name", differences[0].Field);
        Assert.Equal("(none)", differences[0].ParentValue);
        Assert.Equal("New Name", differences[0].CurrentValue);
    }

    [Fact]
    public void Compare_AddedHyperparameters_ReturnsOneDifference()
    {
        // Arrange
        var parent = CreateBaseRequest();
        var current = CreateBaseRequest() with
        {
            Model = new RunRequestModel
            {
                Family = "logistic_regression",
                Hyperparameters = new Dictionary<string, JsonElement>
                {
                    ["C"] = JsonSerializer.SerializeToElement(1.5)
                }
            }
        };

        // Act
        var differences = _comparer.Compare(parent, current);

        // Assert
        Assert.Single(differences);
        Assert.Equal("model.hyperparameters", differences[0].Field);
        Assert.Equal("(default)", differences[0].ParentValue);
        Assert.Contains("\"C\":1.5", differences[0].CurrentValue);
    }

    [Fact]
    public void DiffItem_DisplayName_MapsFieldsCorrectly()
    {
        var testCases = new Dictionary<string, string>
        {
            ["preset"] = "Preset",
            ["dataset.path"] = "Dataset Path",
            ["dataset.label_column"] = "Label Column",
            ["model.family"] = "Model Family",
            ["model.hyperparameters"] = "Hyperparameters",
            ["device.type"] = "Device Type",
            ["name"] = "Run Name",
            ["notes"] = "Notes"
        };

        foreach (var (field, expected) in testCases)
        {
            var item = new DiffItem
            {
                Field = field,
                ParentValue = "a",
                CurrentValue = "b"
            };

            Assert.Equal(expected, item.DisplayName);
        }
    }

    [Fact]
    public void RunRequestDiffResult_NoParent_HasCorrectDefaults()
    {
        var result = RunRequestDiffResult.NoParent;

        Assert.False(result.HasParent);
        Assert.Empty(result.Differences);
        Assert.False(result.HasDifferences);
        Assert.Null(result.ParentRunId);
        Assert.Null(result.ParentRequest);
        Assert.Null(result.CurrentRequest);
    }

    [Fact]
    public void RunRequestDiffResult_WithDifferences_HasDifferencesIsTrue()
    {
        var result = new RunRequestDiffResult
        {
            HasParent = true,
            ParentRunId = "run-001",
            Differences = new List<DiffItem>
            {
                new() { Field = "preset", ParentValue = "a", CurrentValue = "b" }
            }
        };

        Assert.True(result.HasDifferences);
    }
}
