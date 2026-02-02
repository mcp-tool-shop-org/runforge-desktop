namespace RunForgeDesktop.Core.Models;

/// <summary>
/// Complete comparison result between parent and child runs.
/// </summary>
public sealed record RunComparisonResult
{
    /// <summary>
    /// Parent run ID.
    /// </summary>
    public required string ParentRunId { get; init; }

    /// <summary>
    /// Child run ID.
    /// </summary>
    public required string ChildRunId { get; init; }

    /// <summary>
    /// Whether both runs were successfully loaded.
    /// </summary>
    public required bool IsComplete { get; init; }

    /// <summary>
    /// Request diff (reuses existing DiffItem model).
    /// </summary>
    public IReadOnlyList<DiffItem> RequestDifferences { get; init; } = Array.Empty<DiffItem>();

    /// <summary>
    /// Effective config diff (comparing what was actually used).
    /// </summary>
    public IReadOnlyList<DiffItem> EffectiveConfigDifferences { get; init; } = Array.Empty<DiffItem>();

    /// <summary>
    /// Results comparison (status, duration, metrics).
    /// </summary>
    public ResultsComparison? Results { get; init; }

    /// <summary>
    /// Artifact comparison.
    /// </summary>
    public ArtifactComparison? Artifacts { get; init; }

    /// <summary>
    /// Error message if comparison failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether there are any request differences.
    /// </summary>
    public bool HasRequestDifferences => RequestDifferences.Count > 0;

    /// <summary>
    /// Whether there are any effective config differences.
    /// </summary>
    public bool HasEffectiveConfigDifferences => EffectiveConfigDifferences.Count > 0;
}

/// <summary>
/// Comparison of training results between parent and child.
/// </summary>
public sealed record ResultsComparison
{
    /// <summary>
    /// Parent run status.
    /// </summary>
    public required string ParentStatus { get; init; }

    /// <summary>
    /// Child run status.
    /// </summary>
    public required string ChildStatus { get; init; }

    /// <summary>
    /// Parent duration in milliseconds.
    /// </summary>
    public required long ParentDurationMs { get; init; }

    /// <summary>
    /// Child duration in milliseconds.
    /// </summary>
    public required long ChildDurationMs { get; init; }

    /// <summary>
    /// Duration delta (child - parent) in milliseconds.
    /// </summary>
    public long DurationDeltaMs => ChildDurationMs - ParentDurationMs;

    /// <summary>
    /// Duration delta as percentage change.
    /// </summary>
    public double? DurationDeltaPercent =>
        ParentDurationMs > 0
            ? (ChildDurationMs - ParentDurationMs) * 100.0 / ParentDurationMs
            : null;

    /// <summary>
    /// Primary metric comparison.
    /// </summary>
    public MetricDelta? PrimaryMetric { get; init; }

    /// <summary>
    /// All metric deltas.
    /// </summary>
    public IReadOnlyList<MetricDelta> MetricDeltas { get; init; } = Array.Empty<MetricDelta>();

    /// <summary>
    /// Whether both runs succeeded.
    /// </summary>
    public bool BothSucceeded => ParentStatus == "succeeded" && ChildStatus == "succeeded";

    /// <summary>
    /// Formatted parent duration.
    /// </summary>
    public string ParentDurationFormatted => FormatDuration(ParentDurationMs);

    /// <summary>
    /// Formatted child duration.
    /// </summary>
    public string ChildDurationFormatted => FormatDuration(ChildDurationMs);

    /// <summary>
    /// Formatted duration delta with sign.
    /// </summary>
    public string DurationDeltaFormatted
    {
        get
        {
            var sign = DurationDeltaMs >= 0 ? "+" : "";
            return $"{sign}{FormatDuration(DurationDeltaMs)}";
        }
    }

    private static string FormatDuration(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(Math.Abs(ms));
        if (ts.TotalSeconds < 60)
            return $"{ts.TotalSeconds:F1}s";
        if (ts.TotalMinutes < 60)
            return $"{ts.TotalMinutes:F1}m";
        return $"{ts.TotalHours:F1}h";
    }
}

/// <summary>
/// Delta for a single metric.
/// </summary>
public sealed record MetricDelta
{
    /// <summary>
    /// Metric name (e.g., "accuracy", "f1_score").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Parent value (null if not available).
    /// </summary>
    public double? ParentValue { get; init; }

    /// <summary>
    /// Child value (null if not available).
    /// </summary>
    public double? ChildValue { get; init; }

    /// <summary>
    /// Whether this is the primary metric.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// Delta (child - parent), null if either value missing.
    /// </summary>
    public double? Delta =>
        ParentValue.HasValue && ChildValue.HasValue
            ? ChildValue.Value - ParentValue.Value
            : null;

    /// <summary>
    /// Delta as percentage points (for metrics in 0-1 range).
    /// </summary>
    public double? DeltaPercentagePoints =>
        Delta.HasValue ? Delta.Value * 100 : null;

    /// <summary>
    /// Severity of the change: "improved", "degraded", "unchanged", or "unknown".
    /// </summary>
    public string Severity
    {
        get
        {
            if (!Delta.HasValue) return "unknown";
            if (Math.Abs(Delta.Value) < 0.0001) return "unchanged";
            // For most metrics, higher is better
            return Delta.Value > 0 ? "improved" : "degraded";
        }
    }

    /// <summary>
    /// Formatted parent value.
    /// </summary>
    public string ParentValueFormatted =>
        ParentValue.HasValue ? FormatMetric(ParentValue.Value) : "—";

    /// <summary>
    /// Formatted child value.
    /// </summary>
    public string ChildValueFormatted =>
        ChildValue.HasValue ? FormatMetric(ChildValue.Value) : "—";

    /// <summary>
    /// Formatted delta with sign.
    /// </summary>
    public string DeltaFormatted
    {
        get
        {
            if (!Delta.HasValue) return "—";
            var sign = Delta.Value >= 0 ? "+" : "";
            return $"{sign}{FormatMetric(Delta.Value)}";
        }
    }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string DisplayName => Name switch
    {
        "accuracy" => "Accuracy",
        "precision" => "Precision",
        "recall" => "Recall",
        "f1_score" => "F1 Score",
        "dry_run" => "Dry Run",
        _ => Name
    };

    private static string FormatMetric(double value)
    {
        // If looks like percentage (0-1 range), show as percent
        if (value is >= 0 and <= 1)
            return $"{value * 100:F2}%";
        return $"{value:F4}";
    }
}

/// <summary>
/// Comparison of artifacts between parent and child runs.
/// </summary>
public sealed record ArtifactComparison
{
    /// <summary>
    /// Artifacts present in both runs.
    /// </summary>
    public IReadOnlyList<ArtifactPair> Common { get; init; } = Array.Empty<ArtifactPair>();

    /// <summary>
    /// Artifacts only in child run.
    /// </summary>
    public IReadOnlyList<ArtifactInfo> AddedInChild { get; init; } = Array.Empty<ArtifactInfo>();

    /// <summary>
    /// Artifacts only in parent run.
    /// </summary>
    public IReadOnlyList<ArtifactInfo> RemovedFromParent { get; init; } = Array.Empty<ArtifactInfo>();

    /// <summary>
    /// Whether there are any artifact changes.
    /// </summary>
    public bool HasChanges => AddedInChild.Count > 0 || RemovedFromParent.Count > 0;

    /// <summary>
    /// Total artifact count in parent.
    /// </summary>
    public int ParentCount => Common.Count + RemovedFromParent.Count;

    /// <summary>
    /// Total artifact count in child.
    /// </summary>
    public int ChildCount => Common.Count + AddedInChild.Count;
}

/// <summary>
/// Pair of matching artifacts from parent and child.
/// </summary>
public sealed record ArtifactPair
{
    /// <summary>
    /// Artifact from parent run.
    /// </summary>
    public required ArtifactInfo Parent { get; init; }

    /// <summary>
    /// Artifact from child run.
    /// </summary>
    public required ArtifactInfo Child { get; init; }

    /// <summary>
    /// Size difference in bytes.
    /// </summary>
    public long SizeDelta => Child.Bytes - Parent.Bytes;

    /// <summary>
    /// Whether sizes differ significantly.
    /// </summary>
    public bool SizeChanged => Math.Abs(SizeDelta) > 100; // More than 100 bytes difference
}
