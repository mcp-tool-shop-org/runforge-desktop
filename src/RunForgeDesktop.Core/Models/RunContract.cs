using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Run status values. Written to run.json by the runner.
/// </summary>
public enum RunStatus
{
    Pending,
    Running,
    Failed,
    Completed
}

/// <summary>
/// Device type for training.
/// </summary>
public enum DeviceType
{
    CPU,
    GPU
}

/// <summary>
/// run.json schema - the contract between runner and UI.
/// </summary>
public class RunManifest
{
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RunStatus Status { get; set; } = RunStatus.Pending;

    [JsonPropertyName("device")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DeviceType Device { get; set; } = DeviceType.CPU;

    [JsonPropertyName("preset")]
    public string Preset { get; set; } = "";

    [JsonPropertyName("dataset_path")]
    public string DatasetPath { get; set; } = "";

    [JsonPropertyName("output_path")]
    public string OutputPath { get; set; } = "";

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("started_at")]
    public long? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public long? CompletedAt { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("total_epochs")]
    public int TotalEpochs { get; set; } = 50;

    [JsonPropertyName("pid")]
    public int? ProcessId { get; set; }
}

/// <summary>
/// Single line in metrics.jsonl - append-only metrics log.
/// </summary>
public class MetricsEntry
{
    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("epoch")]
    public int Epoch { get; set; }

    [JsonPropertyName("loss")]
    public double Loss { get; set; }

    [JsonPropertyName("lr")]
    public double LearningRate { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }
}

/// <summary>
/// Constants for the run folder contract.
/// </summary>
public static class RunContract
{
    public const string ManifestFileName = "run.json";
    public const string MetricsFileName = "metrics.jsonl";
    public const string StdoutFileName = "stdout.log";
    public const string StderrFileName = "stderr.log";

    /// <summary>
    /// Get the run folder path for a given run ID.
    /// </summary>
    public static string GetRunFolder(string workspacePath, string runId)
        => Path.Combine(workspacePath, ".ml", "runs", runId);

    /// <summary>
    /// Get the manifest file path.
    /// </summary>
    public static string GetManifestPath(string runFolder)
        => Path.Combine(runFolder, ManifestFileName);

    /// <summary>
    /// Get the metrics file path.
    /// </summary>
    public static string GetMetricsPath(string runFolder)
        => Path.Combine(runFolder, MetricsFileName);

    /// <summary>
    /// Get the stdout log path.
    /// </summary>
    public static string GetStdoutPath(string runFolder)
        => Path.Combine(runFolder, StdoutFileName);

    /// <summary>
    /// Get the stderr log path.
    /// </summary>
    public static string GetStderrPath(string runFolder)
        => Path.Combine(runFolder, StderrFileName);

    /// <summary>
    /// Generate a new run ID with timestamp prefix.
    /// </summary>
    public static string GenerateRunId(string name)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var shortHash = Guid.NewGuid().ToString("N")[..4];
        return $"{timestamp}-{safeName}-{shortHash}";
    }
}
