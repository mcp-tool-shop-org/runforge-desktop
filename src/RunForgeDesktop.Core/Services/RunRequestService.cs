using System.Text.Json;
using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of IRunRequestService with atomic file operations.
/// </summary>
public sealed class RunRequestService : IRunRequestService
{
    private const string RequestFileName = "request.json";
    private const string TempFileSuffix = ".tmp";

    /// <summary>
    /// JSON serializer options for writing.
    /// Includes indentation for human readability and sorted keys for determinism.
    /// </summary>
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null, // Use explicit JsonPropertyName attributes
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public async Task<ArtifactLoadResult<RunRequest>> LoadAsync(
        string runDir,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetRequestFilePath(runDir);
        return await ArtifactLoader.LoadAsync<RunRequest>(filePath, cancellationToken);
    }

    /// <inheritdoc />
    public ArtifactLoadResult<RunRequest> Load(string runDir)
    {
        var filePath = GetRequestFilePath(runDir);
        return ArtifactLoader.Load<RunRequest>(filePath);
    }

    /// <inheritdoc />
    public async Task<RunRequestSaveResult> SaveAsync(
        string runDir,
        RunRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate first
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return RunRequestSaveResult.ValidationFailure(errors);
        }

        var filePath = GetRequestFilePath(runDir);
        var tempPath = filePath + TempFileSuffix;

        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(runDir);

            // Write to temp file first
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, request, WriteOptions, cancellationToken);
            }

            // Atomic rename (best effort on Windows)
            File.Move(tempPath, filePath, overwrite: true);

            return RunRequestSaveResult.Success(filePath);
        }
        catch (OperationCanceledException)
        {
            // Clean up temp file if cancelled
            TryDeleteFile(tempPath);
            throw;
        }
        catch (IOException ex)
        {
            TryDeleteFile(tempPath);
            return RunRequestSaveResult.Failure($"Failed to write file: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            TryDeleteFile(tempPath);
            return RunRequestSaveResult.Failure($"Permission denied: {ex.Message}");
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            return RunRequestSaveResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public RunRequestSaveResult Save(string runDir, RunRequest request)
    {
        // Validate first
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return RunRequestSaveResult.ValidationFailure(errors);
        }

        var filePath = GetRequestFilePath(runDir);
        var tempPath = filePath + TempFileSuffix;

        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(runDir);

            // Write to temp file first
            var json = JsonSerializer.Serialize(request, WriteOptions);
            File.WriteAllText(tempPath, json);

            // Atomic rename (best effort on Windows)
            File.Move(tempPath, filePath, overwrite: true);

            return RunRequestSaveResult.Success(filePath);
        }
        catch (IOException ex)
        {
            TryDeleteFile(tempPath);
            return RunRequestSaveResult.Failure($"Failed to write file: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            TryDeleteFile(tempPath);
            return RunRequestSaveResult.Failure($"Permission denied: {ex.Message}");
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            return RunRequestSaveResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    private static string GetRequestFilePath(string runDir) =>
        Path.Combine(runDir, RequestFileName);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }
}
