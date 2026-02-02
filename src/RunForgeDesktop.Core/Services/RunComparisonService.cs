using System.Text.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Service for comparing parent and child runs.
/// </summary>
public interface IRunComparisonService
{
    /// <summary>
    /// Compares a child run with its parent.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="childRunDir">Child run directory (workspace-relative).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete comparison result.</returns>
    Task<RunComparisonResult> CompareWithParentAsync(
        string workspacePath,
        string childRunDir,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares two runs by their directories.
    /// </summary>
    /// <param name="workspacePath">Workspace root path.</param>
    /// <param name="parentRunDir">Parent run directory (workspace-relative).</param>
    /// <param name="childRunDir">Child run directory (workspace-relative).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete comparison result.</returns>
    Task<RunComparisonResult> CompareAsync(
        string workspacePath,
        string parentRunDir,
        string childRunDir,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of run comparison.
/// </summary>
public sealed class RunComparisonService : IRunComparisonService
{
    private readonly IRunRequestService _requestService;
    private readonly IRunRequestComparer _requestComparer;
    private readonly IRunDetailService _detailService;

    public RunComparisonService(
        IRunRequestService requestService,
        IRunRequestComparer requestComparer,
        IRunDetailService detailService)
    {
        _requestService = requestService;
        _requestComparer = requestComparer;
        _detailService = detailService;
    }

    /// <inheritdoc />
    public async Task<RunComparisonResult> CompareWithParentAsync(
        string workspacePath,
        string childRunDir,
        CancellationToken cancellationToken = default)
    {
        // Load child request to get parent run ID
        var childAbsPath = Path.Combine(workspacePath, childRunDir.Replace('/', Path.DirectorySeparatorChar));
        var childRequestResult = await _requestService.LoadAsync(childAbsPath, cancellationToken);

        if (!childRequestResult.IsSuccess || childRequestResult.Value is null)
        {
            return new RunComparisonResult
            {
                ParentRunId = "unknown",
                ChildRunId = Path.GetFileName(childRunDir),
                IsComplete = false,
                ErrorMessage = "Could not load child request"
            };
        }

        var childRequest = childRequestResult.Value;
        if (string.IsNullOrEmpty(childRequest.RerunFrom))
        {
            return new RunComparisonResult
            {
                ParentRunId = "none",
                ChildRunId = Path.GetFileName(childRunDir),
                IsComplete = false,
                ErrorMessage = "This run is not a rerun (no parent)"
            };
        }

        var parentRunDir = $".ml/runs/{childRequest.RerunFrom}";
        return await CompareAsync(workspacePath, parentRunDir, childRunDir, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RunComparisonResult> CompareAsync(
        string workspacePath,
        string parentRunDir,
        string childRunDir,
        CancellationToken cancellationToken = default)
    {
        var parentRunId = Path.GetFileName(parentRunDir.TrimEnd('/'));
        var childRunId = Path.GetFileName(childRunDir.TrimEnd('/'));

        try
        {
            // Load both run details
            var parentDetail = await _detailService.LoadRunDetailAsync(workspacePath, parentRunDir);
            var childDetail = await _detailService.LoadRunDetailAsync(workspacePath, childRunDir);

            // Request differences (if both requests available)
            IReadOnlyList<DiffItem> requestDiffs = Array.Empty<DiffItem>();
            if (parentDetail.Request is not null && childDetail.Request is not null)
            {
                requestDiffs = _requestComparer.Compare(parentDetail.Request, childDetail.Request);
            }

            // Effective config differences
            IReadOnlyList<DiffItem> effectiveConfigDiffs = Array.Empty<DiffItem>();
            if (parentDetail.Result?.EffectiveConfig is not null &&
                childDetail.Result?.EffectiveConfig is not null)
            {
                effectiveConfigDiffs = CompareEffectiveConfig(
                    parentDetail.Result.EffectiveConfig,
                    childDetail.Result.EffectiveConfig);
            }

            // Results comparison
            ResultsComparison? resultsComparison = null;
            if (parentDetail.Result is not null && childDetail.Result is not null)
            {
                resultsComparison = CompareResults(parentDetail.Result, childDetail.Result);
            }

            // Artifact comparison
            ArtifactComparison? artifactComparison = null;
            if (parentDetail.Result?.Artifacts is not null || childDetail.Result?.Artifacts is not null)
            {
                artifactComparison = CompareArtifacts(
                    parentDetail.Result?.Artifacts ?? new List<ArtifactInfo>(),
                    childDetail.Result?.Artifacts ?? new List<ArtifactInfo>());
            }

            return new RunComparisonResult
            {
                ParentRunId = parentRunId,
                ChildRunId = childRunId,
                IsComplete = parentDetail.IsSuccess && childDetail.IsSuccess,
                RequestDifferences = requestDiffs,
                EffectiveConfigDifferences = effectiveConfigDiffs,
                Results = resultsComparison,
                Artifacts = artifactComparison,
                ErrorMessage = !parentDetail.IsSuccess ? parentDetail.ErrorMessage :
                               !childDetail.IsSuccess ? childDetail.ErrorMessage : null
            };
        }
        catch (Exception ex)
        {
            return new RunComparisonResult
            {
                ParentRunId = parentRunId,
                ChildRunId = childRunId,
                IsComplete = false,
                ErrorMessage = $"Comparison failed: {ex.Message}"
            };
        }
    }

    private static IReadOnlyList<DiffItem> CompareEffectiveConfig(
        EffectiveConfig parent,
        EffectiveConfig child)
    {
        var diffs = new List<DiffItem>();

        // Compare preset
        if (!string.Equals(parent.Preset, child.Preset, StringComparison.Ordinal))
        {
            diffs.Add(new DiffItem
            {
                Field = "preset",
                ParentValue = parent.Preset ?? "(none)",
                CurrentValue = child.Preset ?? "(none)"
            });
        }

        // Compare model.family
        if (!string.Equals(parent.Model?.Family, child.Model?.Family, StringComparison.Ordinal))
        {
            diffs.Add(new DiffItem
            {
                Field = "model.family",
                ParentValue = parent.Model?.Family ?? "(none)",
                CurrentValue = child.Model?.Family ?? "(none)"
            });
        }

        // Compare model.hyperparameters
        var parentHyper = SerializeHyperparameters(parent.Model?.Hyperparameters);
        var childHyper = SerializeHyperparameters(child.Model?.Hyperparameters);
        if (!string.Equals(parentHyper, childHyper, StringComparison.Ordinal))
        {
            diffs.Add(new DiffItem
            {
                Field = "model.hyperparameters",
                ParentValue = parentHyper,
                CurrentValue = childHyper
            });
        }

        // Compare device.type
        if (!string.Equals(parent.Device?.Type, child.Device?.Type, StringComparison.Ordinal))
        {
            diffs.Add(new DiffItem
            {
                Field = "device.type",
                ParentValue = parent.Device?.Type ?? "(none)",
                CurrentValue = child.Device?.Type ?? "(none)"
            });
        }

        // Compare dataset.path
        if (!string.Equals(parent.Dataset?.Path, child.Dataset?.Path, StringComparison.Ordinal))
        {
            diffs.Add(new DiffItem
            {
                Field = "dataset.path",
                ParentValue = parent.Dataset?.Path ?? "(none)",
                CurrentValue = child.Dataset?.Path ?? "(none)"
            });
        }

        // Compare dataset.label_column
        if (!string.Equals(parent.Dataset?.LabelColumn, child.Dataset?.LabelColumn, StringComparison.Ordinal))
        {
            diffs.Add(new DiffItem
            {
                Field = "dataset.label_column",
                ParentValue = parent.Dataset?.LabelColumn ?? "(none)",
                CurrentValue = child.Dataset?.LabelColumn ?? "(none)"
            });
        }

        return diffs;
    }

    private static ResultsComparison CompareResults(RunResult parent, RunResult child)
    {
        // Collect all unique metric names
        var allMetricNames = new HashSet<string>();
        if (parent.Summary?.Metrics is not null)
        {
            foreach (var name in parent.Summary.Metrics.Keys)
                allMetricNames.Add(name);
        }
        if (child.Summary?.Metrics is not null)
        {
            foreach (var name in child.Summary.Metrics.Keys)
                allMetricNames.Add(name);
        }

        // Build metric deltas
        var metricDeltas = new List<MetricDelta>();
        foreach (var name in allMetricNames.OrderBy(n => n))
        {
            double? parentValue = parent.Summary?.Metrics?.TryGetValue(name, out var pv) == true ? pv : null;
            double? childValue = child.Summary?.Metrics?.TryGetValue(name, out var cv) == true ? cv : null;

            var isPrimary = name == parent.PrimaryMetricName || name == child.PrimaryMetricName;

            metricDeltas.Add(new MetricDelta
            {
                Name = name,
                ParentValue = parentValue,
                ChildValue = childValue,
                IsPrimary = isPrimary
            });
        }

        // Primary metric (prefer child's primary, fall back to parent's)
        MetricDelta? primaryMetric = null;
        var primaryName = child.PrimaryMetricName ?? parent.PrimaryMetricName;
        if (primaryName is not null)
        {
            primaryMetric = metricDeltas.FirstOrDefault(m => m.Name == primaryName);
        }

        return new ResultsComparison
        {
            ParentStatus = parent.Status,
            ChildStatus = child.Status,
            ParentDurationMs = parent.DurationMs,
            ChildDurationMs = child.DurationMs,
            PrimaryMetric = primaryMetric,
            MetricDeltas = metricDeltas
        };
    }

    private static ArtifactComparison CompareArtifacts(
        IReadOnlyList<ArtifactInfo> parentArtifacts,
        IReadOnlyList<ArtifactInfo> childArtifacts)
    {
        // Index by path for matching
        var parentByPath = parentArtifacts.ToDictionary(a => a.Path, StringComparer.OrdinalIgnoreCase);
        var childByPath = childArtifacts.ToDictionary(a => a.Path, StringComparer.OrdinalIgnoreCase);

        var common = new List<ArtifactPair>();
        var addedInChild = new List<ArtifactInfo>();
        var removedFromParent = new List<ArtifactInfo>();

        // Find common and added
        foreach (var child in childArtifacts)
        {
            if (parentByPath.TryGetValue(child.Path, out var parent))
            {
                common.Add(new ArtifactPair { Parent = parent, Child = child });
            }
            else
            {
                addedInChild.Add(child);
            }
        }

        // Find removed
        foreach (var parent in parentArtifacts)
        {
            if (!childByPath.ContainsKey(parent.Path))
            {
                removedFromParent.Add(parent);
            }
        }

        return new ArtifactComparison
        {
            Common = common,
            AddedInChild = addedInChild,
            RemovedFromParent = removedFromParent
        };
    }

    private static string SerializeHyperparameters(Dictionary<string, JsonElement>? hyperparameters)
    {
        if (hyperparameters is null || hyperparameters.Count == 0)
        {
            return "(default)";
        }

        try
        {
            return JsonSerializer.Serialize(hyperparameters, new JsonSerializerOptions
            {
                WriteIndented = false
            });
        }
        catch
        {
            return "(error)";
        }
    }
}
