using System.Text.RegularExpressions;

namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Represents a milestone in the training pipeline.
/// </summary>
public enum MilestoneType
{
    /// <summary>Run is starting.</summary>
    Starting,

    /// <summary>Loading dataset.</summary>
    LoadingDataset,

    /// <summary>Training in progress.</summary>
    Training,

    /// <summary>Evaluating model.</summary>
    Evaluating,

    /// <summary>Writing output artifacts.</summary>
    WritingArtifacts,

    /// <summary>Run completed.</summary>
    Completed,

    /// <summary>Run failed.</summary>
    Failed
}

/// <summary>
/// A milestone in the run timeline.
/// </summary>
public sealed class RunMilestone
{
    /// <summary>Type of milestone.</summary>
    public required MilestoneType Type { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Whether this milestone has been reached.</summary>
    public bool IsReached { get; set; }

    /// <summary>When this milestone was first reached (UTC).</summary>
    public DateTime? ReachedAtUtc { get; set; }

    /// <summary>Log line that triggered this milestone (if any).</summary>
    public string? TriggerLine { get; set; }

    /// <summary>Whether this is the current active milestone.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Registry of milestone patterns for log detection.
/// </summary>
public static class MilestonePatterns
{
    /// <summary>
    /// Creates the default milestone list.
    /// </summary>
    public static List<RunMilestone> CreateDefaultMilestones() =>
    [
        new() { Type = MilestoneType.Starting, Name = "Starting" },
        new() { Type = MilestoneType.LoadingDataset, Name = "Loading Dataset" },
        new() { Type = MilestoneType.Training, Name = "Training" },
        new() { Type = MilestoneType.Evaluating, Name = "Evaluating" },
        new() { Type = MilestoneType.WritingArtifacts, Name = "Writing Artifacts" },
        new() { Type = MilestoneType.Completed, Name = "Completed" },
    ];

    // Compiled regex patterns for efficient matching
    private static readonly Regex StartingPattern = new(
        @"starting|initializ|begin|run\s+id",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LoadingDatasetPattern = new(
        @"load(ing)?\s+(dataset|data)|reading\s+data|dataset:|loading\s+csv|loaded\s+\d+\s+(rows|samples)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TrainingPattern = new(
        @"epoch\s+\d+|training\s+started|training\s+model|fit\(|fitting|train(ing)?\s+(started|begin)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EvaluatingPattern = new(
        @"evaluat|validation|scor(e|ing)|predict(ing)?|test(ing)?\s+accuracy|confusion\s+matrix",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WritingArtifactsPattern = new(
        @"saving|saved|wrote|writing|artifact|model\s+saved|export(ing)?|metrics\.json|result\.json",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CompletedPattern = new(
        @"complet(e|ed)|finished|done|success(ful)?|training\s+complete",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FailedPattern = new(
        @"fail(ed)?|error|exception|abort(ed)?|crash|fatal",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Detects which milestone (if any) a log line indicates.
    /// Returns null if no milestone detected.
    /// </summary>
    public static MilestoneType? DetectMilestone(string logLine)
    {
        if (string.IsNullOrWhiteSpace(logLine))
            return null;

        // Check patterns in reverse order of pipeline (most specific first)
        // This ensures we don't trigger "Starting" on every line

        if (FailedPattern.IsMatch(logLine))
            return MilestoneType.Failed;

        if (CompletedPattern.IsMatch(logLine))
            return MilestoneType.Completed;

        if (WritingArtifactsPattern.IsMatch(logLine))
            return MilestoneType.WritingArtifacts;

        if (EvaluatingPattern.IsMatch(logLine))
            return MilestoneType.Evaluating;

        if (TrainingPattern.IsMatch(logLine))
            return MilestoneType.Training;

        if (LoadingDatasetPattern.IsMatch(logLine))
            return MilestoneType.LoadingDataset;

        if (StartingPattern.IsMatch(logLine))
            return MilestoneType.Starting;

        return null;
    }

    /// <summary>
    /// Extracts epoch progress if present in the log line.
    /// Returns (current, total) or null if not found.
    /// </summary>
    public static (int Current, int Total)? ExtractEpochProgress(string logLine)
    {
        // Match patterns like "Epoch 3/10" or "epoch 3 of 10" or "Epoch: 3/10"
        var match = Regex.Match(
            logLine,
            @"epoch\s*:?\s*(\d+)\s*[/of]+\s*(\d+)",
            RegexOptions.IgnoreCase);

        if (match.Success &&
            int.TryParse(match.Groups[1].Value, out var current) &&
            int.TryParse(match.Groups[2].Value, out var total))
        {
            return (current, total);
        }

        // Match simple "Epoch X" pattern
        match = Regex.Match(logLine, @"epoch\s*:?\s*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out current))
        {
            return (current, 0); // Total unknown
        }

        return null;
    }
}
