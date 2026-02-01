using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RunForgeDesktop.Core.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of run creation service.
/// </summary>
public sealed partial class RunCreationService : IRunCreationService
{
    private readonly IRunRequestService _requestService;

    public RunCreationService(IRunRequestService requestService)
    {
        _requestService = requestService;
    }

    /// <inheritdoc />
    public async Task<string> CloneForRerunAsync(
        string workspacePath,
        string sourceRunDir,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        // Load source request
        var sourceRunPath = Path.Combine(
            workspacePath,
            sourceRunDir.Replace('/', Path.DirectorySeparatorChar));

        var loadResult = await _requestService.LoadAsync(sourceRunPath, cancellationToken);
        if (!loadResult.IsSuccess || loadResult.Value is null)
        {
            throw new InvalidOperationException($"Source run request not found: {sourceRunDir}");
        }
        var sourceRequest = loadResult.Value;

        // Generate new run ID
        var runName = name ?? sourceRequest.Name ?? ExtractNameFromRunDir(sourceRunDir);
        var runId = GenerateRunId(runName);
        var newRunDir = $".ml/runs/{runId}";

        // Create new request with rerun_from set
        var newRequest = CloneRequest(sourceRequest, runId, runName);

        // Create run directory
        var fullRunPath = Path.Combine(
            workspacePath,
            newRunDir.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(fullRunPath);

        // Write request.json
        await WriteRequestAsync(fullRunPath, newRequest, cancellationToken);

        return newRunDir;
    }

    /// <inheritdoc />
    public async Task<string> CreateRunAsync(
        string workspacePath,
        RunRequest request,
        CancellationToken cancellationToken = default)
    {
        // Generate run ID
        var runId = GenerateRunId(request.Name);
        var newRunDir = $".ml/runs/{runId}";

        // Create run directory
        var fullRunPath = Path.Combine(
            workspacePath,
            newRunDir.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(fullRunPath);

        // Write request.json
        await WriteRequestAsync(fullRunPath, request, cancellationToken);

        return newRunDir;
    }

    /// <inheritdoc />
    public async Task UpdateRequestAsync(
        string workspacePath,
        string runDir,
        RunRequest request,
        CancellationToken cancellationToken = default)
    {
        var fullRunPath = Path.Combine(
            workspacePath,
            runDir.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(fullRunPath))
        {
            throw new DirectoryNotFoundException($"Run directory not found: {fullRunPath}");
        }

        await WriteRequestAsync(fullRunPath, request, cancellationToken);
    }

    /// <inheritdoc />
    public string GenerateRunId(string? name = null)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var slug = CreateSlug(name ?? "run");
        var rand = GenerateRandomHex(4);

        return $"{timestamp}-{slug}-{rand}";
    }

    private static RunRequest CloneRequest(RunRequest source, string runId, string? name)
    {
        // Extract source run ID from its directory
        var sourceRunId = ExtractRunIdFromRunDir(source.CreatedBy) ?? "unknown";

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        return source with
        {
            CreatedAt = now,
            CreatedBy = "runforge-desktop@0.3.0",
            RerunFrom = ExtractRunIdFromRunDir(source.CreatedBy) is not null
                ? source.RerunFrom  // Keep original if already a rerun
                : ExtractRunIdFromRequest(source), // Otherwise, use source run
            Name = name,
        };
    }

    private static string? ExtractRunIdFromRunDir(string runDir)
    {
        // Extract run ID from path like ".ml/runs/20260201-120000-name-a1b2"
        var parts = runDir.Split('/');
        return parts.Length > 0 ? parts[^1] : null;
    }

    private static string? ExtractRunIdFromRequest(RunRequest request)
    {
        // Try to get run ID from created_by or other fields
        return null; // Will be set from the source run dir
    }

    private static string ExtractNameFromRunDir(string runDir)
    {
        // Extract name from run ID like "20260201-120000-my-run-a1b2"
        var runId = runDir.Split('/')[^1];
        var parts = runId.Split('-');

        // Skip timestamp (first 2 parts) and random (last part)
        if (parts.Length > 3)
        {
            return string.Join("-", parts[2..^1]);
        }

        return "rerun";
    }

    private static string CreateSlug(string name)
    {
        // Convert to lowercase and replace non-alphanumeric with dashes
        var slug = SlugRegex().Replace(name.ToLowerInvariant(), "-");

        // Remove leading/trailing dashes and collapse multiple dashes
        slug = MultipleDashRegex().Replace(slug, "-").Trim('-');

        // Limit length
        if (slug.Length > 20)
        {
            slug = slug[..20].TrimEnd('-');
        }

        return string.IsNullOrEmpty(slug) ? "run" : slug;
    }

    private static string GenerateRandomHex(int length)
    {
        var bytes = new byte[length / 2 + 1];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes)[..length].ToLowerInvariant();
    }

    private static async Task WriteRequestAsync(
        string runPath,
        RunRequest request,
        CancellationToken cancellationToken)
    {
        var requestPath = Path.Combine(runPath, "request.json");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(request, options);
        await File.WriteAllTextAsync(requestPath, json, cancellationToken);
    }

    [GeneratedRegex("[^a-z0-9-]")]
    private static partial Regex SlugRegex();

    [GeneratedRegex("-+")]
    private static partial Regex MultipleDashRegex();
}
