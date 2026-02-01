using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.Core.Tests.Services;

public class WorkspaceServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsDir;
    private readonly WorkspaceService _service;

    public WorkspaceServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"RunForgeDesktopTests_{Guid.NewGuid():N}");
        _settingsDir = Path.Combine(_tempDir, "settings");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_settingsDir);
        _service = new WorkspaceService(_settingsDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void InitialState_HasNoWorkspace()
    {
        Assert.Null(_service.CurrentWorkspacePath);
        Assert.Null(_service.CurrentDiscoveryResult);
        Assert.False(_service.HasValidWorkspace);
    }

    [Fact]
    public async Task SetWorkspace_NonexistentDirectory_ReturnsFailure()
    {
        // Arrange
        var fakePath = Path.Combine(_tempDir, "nonexistent");

        // Act
        var result = await _service.SetWorkspaceAsync(fakePath);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Directory does not exist", result.ErrorMessage);
        Assert.Equal(WorkspaceDiscoveryMethod.None, result.Method);
    }

    [Fact]
    public async Task SetWorkspace_EmptyDirectory_ReturnsFailure()
    {
        // Arrange
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        // Act
        var result = await _service.SetWorkspaceAsync(emptyDir);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("No RunForge workspace found", result.ErrorMessage);
    }

    [Fact]
    public async Task SetWorkspace_WithValidIndexFile_ReturnsSuccess()
    {
        // Arrange
        var workspace = CreateWorkspaceWithIndex();

        // Act
        var result = await _service.SetWorkspaceAsync(workspace);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(WorkspaceDiscoveryMethod.IndexFile, result.Method);
        Assert.NotNull(result.IndexPath);
        Assert.True(_service.HasValidWorkspace);
    }

    [Fact]
    public async Task SetWorkspace_WithRunsDirectoryNoIndex_ReturnsSuccessWithFallback()
    {
        // Arrange
        var workspace = CreateWorkspaceWithRunsOnly();

        // Act
        var result = await _service.SetWorkspaceAsync(workspace);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(WorkspaceDiscoveryMethod.RunsDirectory, result.Method);
        Assert.Null(result.IndexPath);  // No index file
    }

    [Fact]
    public async Task SetWorkspace_EmptyMlDirectory_ReturnsFailure()
    {
        // Arrange
        var workspace = Path.Combine(_tempDir, "empty_ml");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(Path.Combine(workspace, ".ml"));

        // Act
        var result = await _service.SetWorkspaceAsync(workspace);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty or incomplete", result.ErrorMessage);
    }

    [Fact]
    public async Task SetWorkspace_CorruptIndexFile_ReturnsFailure()
    {
        // Arrange
        var workspace = Path.Combine(_tempDir, "corrupt_index");
        var indexDir = Path.Combine(workspace, ".ml", "outputs");
        Directory.CreateDirectory(indexDir);
        File.WriteAllText(Path.Combine(indexDir, "index.json"), "not valid json at all");

        // Act
        var result = await _service.SetWorkspaceAsync(workspace);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("does not appear to be valid JSON", result.ErrorMessage);
    }

    [Fact]
    public async Task SetWorkspace_RaisesWorkspaceChangedEvent()
    {
        // Arrange
        var workspace = CreateWorkspaceWithIndex();
        WorkspaceChangedEventArgs? capturedArgs = null;
        _service.WorkspaceChanged += (_, args) => capturedArgs = args;

        // Act
        await _service.SetWorkspaceAsync(workspace);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Null(capturedArgs.PreviousPath);
        Assert.Equal(Path.GetFullPath(workspace), capturedArgs.NewPath);
        Assert.True(capturedArgs.DiscoveryResult?.IsValid);
    }

    [Fact]
    public async Task ClearWorkspace_ResetsState()
    {
        // Arrange
        var workspace = CreateWorkspaceWithIndex();
        await _service.SetWorkspaceAsync(workspace);
        Assert.True(_service.HasValidWorkspace);

        // Act
        _service.ClearWorkspace();

        // Assert
        Assert.Null(_service.CurrentWorkspacePath);
        Assert.Null(_service.CurrentDiscoveryResult);
        Assert.False(_service.HasValidWorkspace);
    }

    [Fact]
    public async Task SaveAndLoadLastWorkspace_RoundTrips()
    {
        // Arrange
        var workspace = CreateWorkspaceWithIndex();
        await _service.SetWorkspaceAsync(workspace);

        // Act
        await _service.SaveLastWorkspaceAsync();
        var newService = new WorkspaceService(_settingsDir);
        var loadedPath = await newService.LoadLastWorkspaceAsync();

        // Assert
        Assert.Equal(Path.GetFullPath(workspace), loadedPath);
    }

    [Fact]
    public async Task LoadLastWorkspace_NoSavedPath_ReturnsNull()
    {
        // Act
        var loadedPath = await _service.LoadLastWorkspaceAsync();

        // Assert
        Assert.Null(loadedPath);
    }

    private string CreateWorkspaceWithIndex()
    {
        var workspace = Path.Combine(_tempDir, $"workspace_{Guid.NewGuid():N}");
        var indexDir = Path.Combine(workspace, ".ml", "outputs");
        Directory.CreateDirectory(indexDir);

        var indexContent = "[]";  // Empty array is valid JSON
        File.WriteAllText(Path.Combine(indexDir, "index.json"), indexContent);

        return workspace;
    }

    private string CreateWorkspaceWithRunsOnly()
    {
        var workspace = Path.Combine(_tempDir, $"workspace_{Guid.NewGuid():N}");
        var runsDir = Path.Combine(workspace, ".ml", "runs", "20260201-142355-test-a3f9");
        Directory.CreateDirectory(runsDir);

        // Add a minimal run.json to simulate a run
        File.WriteAllText(Path.Combine(runsDir, "run.json"), "{}");

        return workspace;
    }
}
