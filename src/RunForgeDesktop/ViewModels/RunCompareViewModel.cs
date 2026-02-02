using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the run comparison page.
/// Supports two entry modes:
/// 1. Parent-child mode: runId + runDir (navigates from run detail "Compare with Parent")
/// 2. A vs B mode: runIdA/runDirA + runIdB/runDirB (navigates from multi-select in runs list)
/// </summary>
public partial class RunCompareViewModel : ObservableObject, IQueryAttributable
{
    private readonly IRunComparisonService _comparisonService;
    private readonly IWorkspaceService _workspaceService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    // A vs B mode parameters (primary)
    [ObservableProperty]
    private string? _runIdA;

    [ObservableProperty]
    private string? _runDirA;

    [ObservableProperty]
    private string? _runIdB;

    [ObservableProperty]
    private string? _runDirB;

    // Legacy parent-child mode parameters
    [ObservableProperty]
    private string? _childRunId;

    [ObservableProperty]
    private string? _childRunDir;

    /// <summary>
    /// Whether lineage is detected (one run is parent of the other).
    /// </summary>
    [ObservableProperty]
    private bool _hasLineage;

    /// <summary>
    /// Lineage direction text (e.g., "A → B" or "B → A").
    /// </summary>
    [ObservableProperty]
    private string? _lineageText;

    [ObservableProperty]
    private RunComparisonResult? _comparison;

    public RunCompareViewModel(
        IRunComparisonService comparisonService,
        IWorkspaceService workspaceService)
    {
        _comparisonService = comparisonService;
        _workspaceService = workspaceService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // A vs B mode (from multi-select)
        if (query.TryGetValue("runIdA", out var idAObj) && idAObj is string idA &&
            query.TryGetValue("runIdB", out var idBObj) && idBObj is string idB)
        {
            RunIdA = idA;
            RunIdB = idB;

            if (query.TryGetValue("runDirA", out var dirAObj) && dirAObj is string dirA)
                RunDirA = dirA;
            if (query.TryGetValue("runDirB", out var dirBObj) && dirBObj is string dirB)
                RunDirB = dirB;

            _ = LoadComparisonABAsync();
            return;
        }

        // Legacy parent-child mode (from run detail)
        if (query.TryGetValue("runId", out var runIdObj) && runIdObj is string runId)
        {
            ChildRunId = runId;
        }

        if (query.TryGetValue("runDir", out var dirObj) && dirObj is string dir)
        {
            ChildRunDir = dir;
            _ = LoadComparisonWithParentAsync();
        }
    }

    /// <summary>
    /// Page title showing the comparison.
    /// </summary>
    public string PageTitle
    {
        get
        {
            if (Comparison is not null)
                return $"{Comparison.ParentRunId} vs {Comparison.ChildRunId}";
            if (!string.IsNullOrEmpty(RunIdA) && !string.IsNullOrEmpty(RunIdB))
                return $"{RunIdA} vs {RunIdB}";
            return "Run Comparison";
        }
    }

    /// <summary>
    /// Whether comparison data is available.
    /// </summary>
    public bool HasComparison => Comparison is not null && Comparison.IsComplete;

    /// <summary>
    /// Whether primary metric improved.
    /// </summary>
    public bool PrimaryMetricImproved =>
        Comparison?.Results?.PrimaryMetric?.Severity == "improved";

    /// <summary>
    /// Whether primary metric degraded.
    /// </summary>
    public bool PrimaryMetricDegraded =>
        Comparison?.Results?.PrimaryMetric?.Severity == "degraded";

    partial void OnComparisonChanged(RunComparisonResult? value)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(HasComparison));
        OnPropertyChanged(nameof(PrimaryMetricImproved));
        OnPropertyChanged(nameof(PrimaryMetricDegraded));
    }

    /// <summary>
    /// Load comparison for A vs B mode (arbitrary two runs).
    /// </summary>
    [RelayCommand]
    private async Task LoadComparisonABAsync()
    {
        if (string.IsNullOrEmpty(RunDirA) || string.IsNullOrEmpty(RunDirB) ||
            string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            ErrorMessage = "Invalid runs or workspace";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        HasLineage = false;
        LineageText = null;

        try
        {
            // Use A as "parent" and B as "child" for comparison purposes
            Comparison = await _comparisonService.CompareAsync(
                _workspaceService.CurrentWorkspacePath,
                RunDirA,
                RunDirB);

            if (!Comparison.IsComplete && !string.IsNullOrEmpty(Comparison.ErrorMessage))
            {
                ErrorMessage = Comparison.ErrorMessage;
            }

            // Detect lineage
            await DetectLineageAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load comparison: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Load comparison using parent-child relationship (legacy mode).
    /// </summary>
    [RelayCommand]
    private async Task LoadComparisonWithParentAsync()
    {
        if (string.IsNullOrEmpty(ChildRunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            ErrorMessage = "Invalid run or workspace";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        HasLineage = true;
        LineageText = "Parent → Child";

        try
        {
            Comparison = await _comparisonService.CompareWithParentAsync(
                _workspaceService.CurrentWorkspacePath,
                ChildRunDir);

            if (!Comparison.IsComplete && !string.IsNullOrEmpty(Comparison.ErrorMessage))
            {
                ErrorMessage = Comparison.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load comparison: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Detect if there's a lineage relationship between the two runs.
    /// </summary>
    private async Task DetectLineageAsync()
    {
        if (string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath) ||
            string.IsNullOrEmpty(RunDirA) || string.IsNullOrEmpty(RunDirB))
            return;

        try
        {
            // Check if B is a child of A
            var runBPath = Path.Combine(
                _workspaceService.CurrentWorkspacePath,
                RunDirB.Replace('/', Path.DirectorySeparatorChar),
                "request.json");

            if (File.Exists(runBPath))
            {
                var json = await File.ReadAllTextAsync(runBPath);
                var request = JsonSerializer.Deserialize<RunRequest>(json, Core.Json.JsonOptions.Default);
                if (request?.RerunFrom == RunIdA)
                {
                    HasLineage = true;
                    LineageText = "A → B (A is parent)";
                    return;
                }
            }

            // Check if A is a child of B
            var runAPath = Path.Combine(
                _workspaceService.CurrentWorkspacePath,
                RunDirA.Replace('/', Path.DirectorySeparatorChar),
                "request.json");

            if (File.Exists(runAPath))
            {
                var json = await File.ReadAllTextAsync(runAPath);
                var request = JsonSerializer.Deserialize<RunRequest>(json, Core.Json.JsonOptions.Default);
                if (request?.RerunFrom == RunIdB)
                {
                    HasLineage = true;
                    LineageText = "B → A (B is parent)";
                    return;
                }
            }
        }
        catch
        {
            // Ignore lineage detection errors
        }
    }

    [RelayCommand]
    private async Task CopySummaryAsync()
    {
        if (Comparison is null)
        {
            StatusMessage = "No comparison data available";
            return;
        }

        try
        {
            var summary = BuildSummaryText();
            await Clipboard.Default.SetTextAsync(summary);
            StatusMessage = "Summary copied to clipboard";
            _ = ClearStatusAfterDelayAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private string BuildSummaryText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Run Comparison Summary ===");
        sb.AppendLine($"Run A: {Comparison!.ParentRunId}");
        sb.AppendLine($"Run B: {Comparison.ChildRunId}");
        if (HasLineage)
        {
            sb.AppendLine($"Lineage: {LineageText}");
        }
        sb.AppendLine();

        // Status comparison
        if (Comparison.Results is not null)
        {
            sb.AppendLine("--- Results ---");
            sb.AppendLine($"Status: {Comparison.Results.ParentStatus} vs {Comparison.Results.ChildStatus}");
            sb.AppendLine($"Duration: {Comparison.Results.ParentDurationFormatted} vs {Comparison.Results.ChildDurationFormatted} ({Comparison.Results.DurationDeltaFormatted})");

            if (Comparison.Results.PrimaryMetric is not null)
            {
                var pm = Comparison.Results.PrimaryMetric;
                sb.AppendLine($"Primary ({pm.DisplayName}): {pm.ParentValueFormatted} vs {pm.ChildValueFormatted} ({pm.DeltaFormatted})");
            }
            sb.AppendLine();
        }

        // Config changes
        if (Comparison.HasEffectiveConfigDifferences)
        {
            sb.AppendLine("--- Config Changes ---");
            foreach (var diff in Comparison.EffectiveConfigDifferences)
            {
                sb.AppendLine($"{diff.DisplayName}: {diff.ParentValue} → {diff.CurrentValue}");
            }
            sb.AppendLine();
        }

        // Metric changes
        if (Comparison.Results?.MetricDeltas.Count > 0)
        {
            sb.AppendLine("--- Metrics ---");
            foreach (var delta in Comparison.Results.MetricDeltas)
            {
                var indicator = delta.Severity switch
                {
                    "improved" => "+",
                    "degraded" => "-",
                    _ => " "
                };
                sb.AppendLine($"[{indicator}] {delta.DisplayName}: {delta.ParentValueFormatted} vs {delta.ChildValueFormatted} ({delta.DeltaFormatted})");
            }
            sb.AppendLine();
        }

        // Artifact changes
        if (Comparison.Artifacts?.HasChanges == true)
        {
            sb.AppendLine("--- Artifacts ---");
            if (Comparison.Artifacts.AddedInChild.Count > 0)
            {
                sb.AppendLine($"Only in B: {string.Join(", ", Comparison.Artifacts.AddedInChild.Select(a => a.Path))}");
            }
            if (Comparison.Artifacts.RemovedFromParent.Count > 0)
            {
                sb.AppendLine($"Only in A: {string.Join(", ", Comparison.Artifacts.RemovedFromParent.Select(a => a.Path))}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        return sb.ToString();
    }

    private async Task ClearStatusAfterDelayAsync()
    {
        await Task.Delay(3000);
        if (StatusMessage is not null && !StatusMessage.StartsWith("Failed"))
        {
            StatusMessage = null;
        }
    }
}
