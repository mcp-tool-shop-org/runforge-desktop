using System.Text.Json;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.Core.Tests.Services;

/// <summary>
/// Integration tests for the edit→save→execute workflow.
/// These tests use CLI --dry-run mode for fast, deterministic execution.
/// </summary>
public class EditSaveExecuteIntegrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _workspaceDir;
    private readonly RunRequestService _requestService;

    public EditSaveExecuteIntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "runforge-integration-tests", Guid.NewGuid().ToString());
        _workspaceDir = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(_workspaceDir);

        // Create minimal dataset for validation
        var dataDir = Path.Combine(_workspaceDir, "data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "test.csv"), "feature1,feature2,target\n1,2,0\n3,4,1\n");

        _requestService = new RunRequestService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests
        }
    }

    #region Test Helpers

    private string CreateRunDir(string runId)
    {
        var runDir = Path.Combine(_workspaceDir, ".ml", "runs", runId);
        Directory.CreateDirectory(runDir);
        return runDir;
    }

    private static RunRequest CreateValidRequest() => new()
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
            Family = "logistic_regression"
        },
        Device = new RunRequestDevice
        {
            Type = "cpu"
        },
        CreatedAt = "2026-02-01T12:00:00Z",
        CreatedBy = "runforge-desktop@test"
    };

    #endregion

    #region Edit Persists Tests

    [Fact]
    public async Task EditSave_ChangesModelFamily_PersistsChange()
    {
        // Arrange
        var runDir = CreateRunDir("test-edit-persist");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act - Load, edit, save
        var loadResult = await _requestService.LoadAsync(runDir);
        Assert.True(loadResult.IsSuccess);

        var editedRequest = loadResult.Value! with
        {
            Model = new RunRequestModel { Family = "linear_svc" }
        };
        var saveResult = await _requestService.SaveAsync(runDir, editedRequest);

        // Assert - Reload and verify change persisted
        Assert.True(saveResult.IsSuccess);
        var reloadResult = await _requestService.LoadAsync(runDir);
        Assert.True(reloadResult.IsSuccess);
        Assert.Equal("linear_svc", reloadResult.Value!.Model.Family);
    }

    [Fact]
    public async Task EditSave_ChangesPreset_PersistsChange()
    {
        // Arrange
        var runDir = CreateRunDir("test-edit-preset");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act
        var loadResult = await _requestService.LoadAsync(runDir);
        var editedRequest = loadResult.Value! with { Preset = "thorough" };
        await _requestService.SaveAsync(runDir, editedRequest);

        // Assert
        var reloadResult = await _requestService.LoadAsync(runDir);
        Assert.Equal("thorough", reloadResult.Value!.Preset);
    }

    [Fact]
    public async Task EditSave_ChangesDatasetPath_PersistsChange()
    {
        // Arrange
        var runDir = CreateRunDir("test-edit-dataset");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act
        var loadResult = await _requestService.LoadAsync(runDir);
        var editedRequest = loadResult.Value! with
        {
            Dataset = new RunRequestDataset
            {
                Path = "data/new_dataset.csv",
                LabelColumn = "label"
            }
        };
        await _requestService.SaveAsync(runDir, editedRequest);

        // Assert
        var reloadResult = await _requestService.LoadAsync(runDir);
        Assert.Equal("data/new_dataset.csv", reloadResult.Value!.Dataset.Path);
        Assert.Equal("label", reloadResult.Value!.Dataset.LabelColumn);
    }

    [Fact]
    public async Task EditSave_AddsHyperparameters_PersistsChange()
    {
        // Arrange
        var runDir = CreateRunDir("test-edit-hyperparams");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act
        var loadResult = await _requestService.LoadAsync(runDir);
        var editedRequest = loadResult.Value! with
        {
            Model = new RunRequestModel
            {
                Family = loadResult.Value!.Model.Family,
                Hyperparameters = new Dictionary<string, JsonElement>
                {
                    ["max_iter"] = JsonSerializer.SerializeToElement(500),
                    ["random_state"] = JsonSerializer.SerializeToElement(42)
                }
            }
        };
        await _requestService.SaveAsync(runDir, editedRequest);

        // Assert
        var reloadResult = await _requestService.LoadAsync(runDir);
        Assert.NotNull(reloadResult.Value!.Model.Hyperparameters);
        Assert.Equal(2, reloadResult.Value!.Model.Hyperparameters.Count);
    }

    #endregion

    #region Invalid Blocks Execution Tests

    [Fact]
    public async Task EditSave_ClearLabelColumn_FailsValidation()
    {
        // Arrange
        var runDir = CreateRunDir("test-invalid-label");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act - Try to save with empty label column
        var loadResult = await _requestService.LoadAsync(runDir);
        var invalidRequest = loadResult.Value! with
        {
            Dataset = new RunRequestDataset
            {
                Path = loadResult.Value!.Dataset.Path,
                LabelColumn = "" // Invalid - empty
            }
        };
        var saveResult = await _requestService.SaveAsync(runDir, invalidRequest);

        // Assert - Save should fail validation
        Assert.False(saveResult.IsSuccess);
        Assert.NotNull(saveResult.ValidationErrors);
        Assert.Contains(saveResult.ValidationErrors, e => e.Contains("label_column"));
    }

    [Fact]
    public async Task EditSave_ClearModelFamily_FailsValidation()
    {
        // Arrange
        var runDir = CreateRunDir("test-invalid-model");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act
        var loadResult = await _requestService.LoadAsync(runDir);
        var invalidRequest = loadResult.Value! with
        {
            Model = new RunRequestModel { Family = "" }
        };
        var saveResult = await _requestService.SaveAsync(runDir, invalidRequest);

        // Assert
        Assert.False(saveResult.IsSuccess);
        Assert.NotNull(saveResult.ValidationErrors);
    }

    [Fact]
    public async Task EditSave_ClearDatasetPath_FailsValidation()
    {
        // Arrange
        var runDir = CreateRunDir("test-invalid-path");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act
        var loadResult = await _requestService.LoadAsync(runDir);
        var invalidRequest = loadResult.Value! with
        {
            Dataset = new RunRequestDataset
            {
                Path = "",
                LabelColumn = loadResult.Value!.Dataset.LabelColumn
            }
        };
        var saveResult = await _requestService.SaveAsync(runDir, invalidRequest);

        // Assert
        Assert.False(saveResult.IsSuccess);
    }

    #endregion

    #region Unknown Fields Preservation Tests

    [Fact]
    public async Task EditSave_PreservesTopLevelUnknownFields()
    {
        // Arrange
        var runDir = CreateRunDir("test-preserve-top-level");
        Directory.CreateDirectory(runDir);

        // Write JSON with future fields
        var json = """
        {
          "version": 1,
          "preset": "balanced",
          "dataset": { "path": "data/test.csv", "label_column": "target" },
          "model": { "family": "logistic_regression" },
          "device": { "type": "cpu" },
          "created_at": "2026-02-01T12:00:00Z",
          "created_by": "test@1.0",
          "future_field_v2": "preserve_me",
          "another_future": { "nested": true }
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(runDir, "request.json"), json);

        // Act - Load, edit known field, save
        var loadResult = await _requestService.LoadAsync(runDir);
        Assert.True(loadResult.IsSuccess);

        var editedRequest = loadResult.Value! with { Preset = "thorough" };
        await _requestService.SaveAsync(runDir, editedRequest);

        // Assert - Unknown fields preserved
        var reloadResult = await _requestService.LoadAsync(runDir);
        Assert.True(reloadResult.IsSuccess);
        Assert.Equal("thorough", reloadResult.Value!.Preset); // Edit applied
        Assert.NotNull(reloadResult.Value!.ExtensionData);
        Assert.True(reloadResult.Value!.ExtensionData.ContainsKey("future_field_v2"));
        Assert.True(reloadResult.Value!.ExtensionData.ContainsKey("another_future"));
    }

    [Fact]
    public async Task EditSave_PreservesNestedUnknownFields()
    {
        // Arrange
        var runDir = CreateRunDir("test-preserve-nested");
        Directory.CreateDirectory(runDir);

        var json = """
        {
          "version": 1,
          "preset": "balanced",
          "dataset": {
            "path": "data/test.csv",
            "label_column": "target",
            "sampling_strategy": "balanced",
            "future_dataset_field": 123
          },
          "model": {
            "family": "logistic_regression",
            "future_model_config": { "key": "value" }
          },
          "device": {
            "type": "cpu",
            "cuda_device_id": 0
          },
          "created_at": "2026-02-01T12:00:00Z",
          "created_by": "test@1.0"
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(runDir, "request.json"), json);

        // Act
        var loadResult = await _requestService.LoadAsync(runDir);
        var editedRequest = loadResult.Value! with
        {
            Model = new RunRequestModel
            {
                Family = "random_forest",
                Hyperparameters = loadResult.Value!.Model.Hyperparameters,
                ExtensionData = loadResult.Value!.Model.ExtensionData // Preserve extension data
            }
        };
        await _requestService.SaveAsync(runDir, editedRequest);

        // Assert
        var reloadResult = await _requestService.LoadAsync(runDir);
        Assert.Equal("random_forest", reloadResult.Value!.Model.Family);
        Assert.NotNull(reloadResult.Value!.Model.ExtensionData);
        Assert.True(reloadResult.Value!.Model.ExtensionData.ContainsKey("future_model_config"));
        Assert.NotNull(reloadResult.Value!.Dataset.ExtensionData);
        Assert.True(reloadResult.Value!.Dataset.ExtensionData.ContainsKey("future_dataset_field"));
    }

    #endregion

    #region Multiple Edits Tests

    [Fact]
    public async Task EditSave_MultipleEdits_AllPersist()
    {
        // Arrange
        var runDir = CreateRunDir("test-multiple-edits");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act - Multiple edit cycles
        for (int i = 1; i <= 3; i++)
        {
            var loadResult = await _requestService.LoadAsync(runDir);
            var editedRequest = loadResult.Value! with { Name = $"Edit {i}" };
            await _requestService.SaveAsync(runDir, editedRequest);
        }

        // Assert
        var finalResult = await _requestService.LoadAsync(runDir);
        Assert.Equal("Edit 3", finalResult.Value!.Name);
    }

    [Fact]
    public async Task EditSave_RevertEdit_PersistsRevert()
    {
        // Arrange
        var runDir = CreateRunDir("test-revert");
        var originalRequest = CreateValidRequest() with { Name = "Original" };
        await _requestService.SaveAsync(runDir, originalRequest);

        // Act - Edit, then revert
        var loadResult = await _requestService.LoadAsync(runDir);
        var editedRequest = loadResult.Value! with { Name = "Changed" };
        await _requestService.SaveAsync(runDir, editedRequest);

        var revertResult = await _requestService.LoadAsync(runDir);
        var revertedRequest = revertResult.Value! with { Name = "Original" };
        await _requestService.SaveAsync(runDir, revertedRequest);

        // Assert
        var finalResult = await _requestService.LoadAsync(runDir);
        Assert.Equal("Original", finalResult.Value!.Name);
    }

    #endregion
}
