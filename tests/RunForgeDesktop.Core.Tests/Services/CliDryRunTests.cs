using System.Diagnostics;
using System.Text.Json;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.Core.Tests.Services;

/// <summary>
/// Integration tests for CLI dry-run mode.
/// These tests require Python and runforge-cli to be installed.
/// Skip if Python is not available.
/// </summary>
[Collection("CliTests")] // Prevent parallel execution with other CLI tests
public class CliDryRunTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _workspaceDir;
    private readonly RunRequestService _requestService;
    private readonly string? _pythonPath;
    private readonly bool _skipTests;

    public CliDryRunTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "runforge-cli-tests", Guid.NewGuid().ToString());
        _workspaceDir = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(_workspaceDir);

        // Create minimal dataset for validation
        var dataDir = Path.Combine(_workspaceDir, "data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "test.csv"), "feature1,feature2,target\n1,2,0\n3,4,1\n5,6,0\n7,8,1\n");

        _requestService = new RunRequestService();

        // Try to find Python
        _pythonPath = FindPython();
        _skipTests = string.IsNullOrEmpty(_pythonPath) || !IsCliInstalled();
    }

    private static string? FindPython()
    {
        var candidates = new[] { "python", "python3", "py" };
        foreach (var candidate in candidates)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    process.WaitForExit(5000);
                    if (process.ExitCode == 0)
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
                // Try next candidate
            }
        }
        return null;
    }

    private bool IsCliInstalled()
    {
        if (string.IsNullOrEmpty(_pythonPath)) return false;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = "-m runforge_cli --version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit(5000);
                return process.ExitCode == 0;
            }
        }
        catch
        {
            // CLI not installed
        }
        return false;
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

    private async Task<(int ExitCode, string Output)> RunCliDryRunAsync(string runDir)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonPath!,
            Arguments = $"-m runforge_cli run --run-dir \"{runDir}\" --workspace \"{_workspaceDir}\" --dry-run",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _workspaceDir
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output + error);
    }

    #endregion

    #region Dry-Run Basic Tests

    [Fact]
    public async Task DryRun_ValidRequest_Succeeds()
    {
        if (_skipTests)
        {
            // Skip test if Python/CLI not available
            return;
        }

        // Arrange
        var runDir = CreateRunDir("test-dryrun-success");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act
        var (exitCode, output) = await RunCliDryRunAsync(runDir);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("[DRY-RUN", output);
    }

    [Fact]
    public async Task DryRun_WritesResultJson()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var runDir = CreateRunDir("test-dryrun-result");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act
        await RunCliDryRunAsync(runDir);

        // Assert
        var resultPath = Path.Combine(runDir, "result.json");
        Assert.True(File.Exists(resultPath));

        var resultJson = await File.ReadAllTextAsync(resultPath);
        using var doc = JsonDocument.Parse(resultJson);
        Assert.Equal("succeeded", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DryRun_WritesEffectiveConfig()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var runDir = CreateRunDir("test-dryrun-effective");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act
        await RunCliDryRunAsync(runDir);

        // Assert
        var resultPath = Path.Combine(runDir, "result.json");
        var resultJson = await File.ReadAllTextAsync(resultPath);
        using var doc = JsonDocument.Parse(resultJson);

        var effectiveConfig = doc.RootElement.GetProperty("effective_config");
        Assert.Equal("logistic_regression", effectiveConfig.GetProperty("model").GetProperty("family").GetString());
        Assert.Equal("cpu", effectiveConfig.GetProperty("device").GetProperty("type").GetString());
        Assert.Equal("balanced", effectiveConfig.GetProperty("preset").GetString());
    }

    [Fact]
    public async Task DryRun_WritesLogsWithStageTokens()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var runDir = CreateRunDir("test-dryrun-logs");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act
        await RunCliDryRunAsync(runDir);

        // Assert
        var logsPath = Path.Combine(runDir, "logs.txt");
        Assert.True(File.Exists(logsPath));

        var logs = await File.ReadAllTextAsync(logsPath);
        Assert.Contains("[RF:STAGE=STARTING]", logs);
        Assert.Contains("[RF:STAGE=LOADING_DATASET]", logs);
        Assert.Contains("[RF:STAGE=COMPLETED]", logs);
    }

    #endregion

    #region Edit → Execute Uses Saved Config

    [Fact]
    public async Task DryRun_AfterEdit_UsesNewModelFamily()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange - Create and save initial request
        var runDir = CreateRunDir("test-dryrun-edit-model");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act - Edit to change model family, then run
        var loadResult = await _requestService.LoadAsync(runDir);
        var editedRequest = loadResult.Value! with
        {
            Model = new RunRequestModel { Family = "linear_svc" }
        };
        await _requestService.SaveAsync(runDir, editedRequest);

        await RunCliDryRunAsync(runDir);

        // Assert - Result should reflect the edited model family
        var resultPath = Path.Combine(runDir, "result.json");
        var resultJson = await File.ReadAllTextAsync(resultPath);
        using var doc = JsonDocument.Parse(resultJson);

        var effectiveConfig = doc.RootElement.GetProperty("effective_config");
        Assert.Equal("linear_svc", effectiveConfig.GetProperty("model").GetProperty("family").GetString());
    }

    [Fact]
    public async Task DryRun_AfterEdit_UsesNewPreset()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var runDir = CreateRunDir("test-dryrun-edit-preset");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act
        var loadResult = await _requestService.LoadAsync(runDir);
        var editedRequest = loadResult.Value! with { Preset = "thorough" };
        await _requestService.SaveAsync(runDir, editedRequest);

        await RunCliDryRunAsync(runDir);

        // Assert
        var resultPath = Path.Combine(runDir, "result.json");
        var resultJson = await File.ReadAllTextAsync(resultPath);
        using var doc = JsonDocument.Parse(resultJson);

        var effectiveConfig = doc.RootElement.GetProperty("effective_config");
        Assert.Equal("thorough", effectiveConfig.GetProperty("preset").GetString());
    }

    [Fact]
    public async Task DryRun_AfterEdit_UsesNewDeviceType()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var runDir = CreateRunDir("test-dryrun-edit-device");
        var request = CreateValidRequest();
        await _requestService.SaveAsync(runDir, request);

        // Act
        var loadResult = await _requestService.LoadAsync(runDir);
        var editedRequest = loadResult.Value! with
        {
            Device = new RunRequestDevice { Type = "gpu" }
        };
        await _requestService.SaveAsync(runDir, editedRequest);

        await RunCliDryRunAsync(runDir);

        // Assert
        var resultPath = Path.Combine(runDir, "result.json");
        var resultJson = await File.ReadAllTextAsync(resultPath);
        using var doc = JsonDocument.Parse(resultJson);

        var effectiveConfig = doc.RootElement.GetProperty("effective_config");
        Assert.Equal("gpu", effectiveConfig.GetProperty("device").GetProperty("type").GetString());
    }

    #endregion

    #region Validation Error Tests

    [Fact]
    public async Task DryRun_MissingDataset_Fails()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange - Create request pointing to non-existent dataset
        var runDir = CreateRunDir("test-dryrun-missing-dataset");
        var request = CreateValidRequest() with
        {
            Dataset = new RunRequestDataset
            {
                Path = "data/nonexistent.csv",
                LabelColumn = "target"
            }
        };
        await _requestService.SaveAsync(runDir, request);

        // Act
        var (exitCode, output) = await RunCliDryRunAsync(runDir);

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("not found", output.ToLower());
    }

    [Fact]
    public async Task DryRun_InvalidLabelColumn_Fails()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var runDir = CreateRunDir("test-dryrun-invalid-label");
        var request = CreateValidRequest() with
        {
            Dataset = new RunRequestDataset
            {
                Path = "data/test.csv",
                LabelColumn = "nonexistent_column"
            }
        };
        await _requestService.SaveAsync(runDir, request);

        // Act
        var (exitCode, output) = await RunCliDryRunAsync(runDir);

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("not found", output.ToLower());
    }

    [Fact]
    public async Task DryRun_InvalidModelFamily_Fails()
    {
        if (_skipTests)
        {
            return;
        }

        // Arrange
        var runDir = CreateRunDir("test-dryrun-invalid-model");
        var request = CreateValidRequest() with
        {
            Model = new RunRequestModel { Family = "unsupported_model" }
        };
        await _requestService.SaveAsync(runDir, request);

        // Act
        var (exitCode, output) = await RunCliDryRunAsync(runDir);

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("unsupported", output.ToLower());
    }

    #endregion
}
