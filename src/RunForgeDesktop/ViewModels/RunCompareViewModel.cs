using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the run comparison page.
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

    [ObservableProperty]
    private string? _childRunId;

    [ObservableProperty]
    private string? _childRunDir;

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
        if (query.TryGetValue("runId", out var runIdObj) && runIdObj is string runId)
        {
            ChildRunId = runId;
        }

        if (query.TryGetValue("runDir", out var dirObj) && dirObj is string dir)
        {
            ChildRunDir = dir;
            _ = LoadComparisonAsync();
        }
    }

    /// <summary>
    /// Page title showing the comparison.
    /// </summary>
    public string PageTitle => Comparison is not null
        ? $"{Comparison.ParentRunId} → {Comparison.ChildRunId}"
        : "Run Comparison";

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

    [RelayCommand]
    private async Task LoadComparisonAsync()
    {
        if (string.IsNullOrEmpty(ChildRunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            ErrorMessage = "Invalid run or workspace";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

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
        sb.AppendLine($"Parent: {Comparison!.ParentRunId}");
        sb.AppendLine($"Child: {Comparison.ChildRunId}");
        sb.AppendLine();

        // Status comparison
        if (Comparison.Results is not null)
        {
            sb.AppendLine("--- Results ---");
            sb.AppendLine($"Status: {Comparison.Results.ParentStatus} → {Comparison.Results.ChildStatus}");
            sb.AppendLine($"Duration: {Comparison.Results.ParentDurationFormatted} → {Comparison.Results.ChildDurationFormatted} ({Comparison.Results.DurationDeltaFormatted})");

            if (Comparison.Results.PrimaryMetric is not null)
            {
                var pm = Comparison.Results.PrimaryMetric;
                sb.AppendLine($"Primary ({pm.DisplayName}): {pm.ParentValueFormatted} → {pm.ChildValueFormatted} ({pm.DeltaFormatted})");
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
                sb.AppendLine($"[{indicator}] {delta.DisplayName}: {delta.ParentValueFormatted} → {delta.ChildValueFormatted} ({delta.DeltaFormatted})");
            }
            sb.AppendLine();
        }

        // Artifact changes
        if (Comparison.Artifacts?.HasChanges == true)
        {
            sb.AppendLine("--- Artifacts ---");
            if (Comparison.Artifacts.AddedInChild.Count > 0)
            {
                sb.AppendLine($"Added: {string.Join(", ", Comparison.Artifacts.AddedInChild.Select(a => a.Path))}");
            }
            if (Comparison.Artifacts.RemovedFromParent.Count > 0)
            {
                sb.AppendLine($"Removed: {string.Join(", ", Comparison.Artifacts.RemovedFromParent.Select(a => a.Path))}");
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
