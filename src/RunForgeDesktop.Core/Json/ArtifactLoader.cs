using System.Text.Json;

namespace RunForgeDesktop.Core.Json;

/// <summary>
/// Result of an artifact load operation.
/// </summary>
/// <typeparam name="T">The artifact type.</typeparam>
public sealed record ArtifactLoadResult<T> where T : class
{
    /// <summary>
    /// The loaded artifact, or null if loading failed.
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// Whether the load succeeded.
    /// </summary>
    public bool IsSuccess => Value is not null && Error is null;

    /// <summary>
    /// Error details if loading failed.
    /// </summary>
    public ArtifactError? Error { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ArtifactLoadResult<T> Success(T value) => new() { Value = value };

    /// <summary>
    /// Creates a failed result with an error.
    /// </summary>
    public static ArtifactLoadResult<T> Failure(ArtifactError error) => new() { Error = error };
}

/// <summary>
/// Describes an artifact loading error.
/// </summary>
public sealed record ArtifactError
{
    /// <summary>
    /// Error type code.
    /// </summary>
    public required ArtifactErrorType Type { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// File path that caused the error.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Inner exception details, if any.
    /// </summary>
    public string? InnerDetails { get; init; }
}

/// <summary>
/// Types of artifact loading errors.
/// </summary>
public enum ArtifactErrorType
{
    /// <summary>
    /// The artifact file does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// The artifact file could not be read (permissions, locked, etc.).
    /// </summary>
    ReadError,

    /// <summary>
    /// The artifact JSON is malformed.
    /// </summary>
    MalformedJson,

    /// <summary>
    /// The artifact schema validation failed.
    /// </summary>
    SchemaValidation,

    /// <summary>
    /// An unexpected error occurred.
    /// </summary>
    Unknown
}

/// <summary>
/// Utility for loading RunForge artifacts from disk.
/// Read-only operations only.
/// </summary>
public static class ArtifactLoader
{
    /// <summary>
    /// Loads a JSON artifact from disk.
    /// </summary>
    /// <typeparam name="T">The artifact type to deserialize to.</typeparam>
    /// <param name="filePath">Absolute path to the artifact file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Load result with artifact or error details.</returns>
    public static async Task<ArtifactLoadResult<T>> LoadAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default) where T : class
    {
        if (!File.Exists(filePath))
        {
            return ArtifactLoadResult<T>.Failure(new ArtifactError
            {
                Type = ArtifactErrorType.NotFound,
                Message = "Artifact file not found",
                FilePath = filePath
            });
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var artifact = await JsonSerializer.DeserializeAsync<T>(
                stream,
                JsonOptions.Default,
                cancellationToken);

            if (artifact is null)
            {
                return ArtifactLoadResult<T>.Failure(new ArtifactError
                {
                    Type = ArtifactErrorType.MalformedJson,
                    Message = "JSON deserialized to null",
                    FilePath = filePath
                });
            }

            return ArtifactLoadResult<T>.Success(artifact);
        }
        catch (JsonException ex)
        {
            return ArtifactLoadResult<T>.Failure(new ArtifactError
            {
                Type = ArtifactErrorType.MalformedJson,
                Message = "Failed to parse JSON",
                FilePath = filePath,
                InnerDetails = ex.Message
            });
        }
        catch (IOException ex)
        {
            return ArtifactLoadResult<T>.Failure(new ArtifactError
            {
                Type = ArtifactErrorType.ReadError,
                Message = "Failed to read file",
                FilePath = filePath,
                InnerDetails = ex.Message
            });
        }
        catch (Exception ex)
        {
            return ArtifactLoadResult<T>.Failure(new ArtifactError
            {
                Type = ArtifactErrorType.Unknown,
                Message = "Unexpected error loading artifact",
                FilePath = filePath,
                InnerDetails = ex.Message
            });
        }
    }

    /// <summary>
    /// Loads a JSON artifact synchronously.
    /// Use LoadAsync when possible for better UI responsiveness.
    /// </summary>
    public static ArtifactLoadResult<T> Load<T>(string filePath) where T : class
    {
        if (!File.Exists(filePath))
        {
            return ArtifactLoadResult<T>.Failure(new ArtifactError
            {
                Type = ArtifactErrorType.NotFound,
                Message = "Artifact file not found",
                FilePath = filePath
            });
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var artifact = JsonSerializer.Deserialize<T>(json, JsonOptions.Default);

            if (artifact is null)
            {
                return ArtifactLoadResult<T>.Failure(new ArtifactError
                {
                    Type = ArtifactErrorType.MalformedJson,
                    Message = "JSON deserialized to null",
                    FilePath = filePath
                });
            }

            return ArtifactLoadResult<T>.Success(artifact);
        }
        catch (JsonException ex)
        {
            return ArtifactLoadResult<T>.Failure(new ArtifactError
            {
                Type = ArtifactErrorType.MalformedJson,
                Message = "Failed to parse JSON",
                FilePath = filePath,
                InnerDetails = ex.Message
            });
        }
        catch (IOException ex)
        {
            return ArtifactLoadResult<T>.Failure(new ArtifactError
            {
                Type = ArtifactErrorType.ReadError,
                Message = "Failed to read file",
                FilePath = filePath,
                InnerDetails = ex.Message
            });
        }
        catch (Exception ex)
        {
            return ArtifactLoadResult<T>.Failure(new ArtifactError
            {
                Type = ArtifactErrorType.Unknown,
                Message = "Unexpected error loading artifact",
                FilePath = filePath,
                InnerDetails = ex.Message
            });
        }
    }
}
