using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the metrics detail view.
/// </summary>
public partial class MetricsDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IInterpretabilityService _interpretabilityService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IExportService _exportService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _runId;

    [ObservableProperty]
    private string? _runName;

    [ObservableProperty]
    private string? _runDir;

    [ObservableProperty]
    private MetricsV1? _artifact;

    [ObservableProperty]
    private List<MetricCategoryGroup> _metricGroups = [];

    public MetricsDetailViewModel(
        IInterpretabilityService interpretabilityService,
        IWorkspaceService workspaceService,
        IExportService exportService)
    {
        _interpretabilityService = interpretabilityService;
        _workspaceService = workspaceService;
        _exportService = exportService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("runId", out var runIdObj) && runIdObj is string runId)
        {
            RunId = runId;
        }

        if (query.TryGetValue("runName", out var nameObj) && nameObj is string name)
        {
            RunName = name;
        }

        if (query.TryGetValue("runDir", out var dirObj) && dirObj is string dir)
        {
            RunDir = dir;
            _ = LoadAsync();
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            ErrorMessage = "Invalid run or workspace";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // First load the index to get artifact path
            var indexResult = await _interpretabilityService.LoadIndexAsync(
                _workspaceService.CurrentWorkspacePath,
                RunDir);

            if (!indexResult.IsSuccess || indexResult.Index is null)
            {
                ErrorMessage = indexResult.ErrorMessage ?? "Failed to load interpretability index";
                return;
            }

            var entry = indexResult.Index.GetArtifact("metrics.v1");
            if (entry is null || !entry.Available)
            {
                ErrorMessage = "Metrics artifact not available";
                return;
            }

            Artifact = await _interpretabilityService.LoadArtifactAsync<MetricsV1>(
                _workspaceService.CurrentWorkspacePath,
                RunDir,
                entry.Path);

            if (Artifact is not null)
            {
                UpdateMetricGroups();
            }
            else
            {
                ErrorMessage = "Failed to load metrics data";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load metrics: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateMetricGroups()
    {
        if (Artifact is null)
        {
            MetricGroups = [];
            return;
        }

        var groups = Artifact.Metrics
            .Select(kv => new MetricCategoryGroup
            {
                CategoryName = FormatCategoryName(kv.Key),
                Metrics = kv.Value
                    .Select(m => new MetricItem
                    {
                        Name = FormatMetricName(m.Key),
                        Value = m.Value,
                        ValueDisplay = FormatMetricValue(m.Key, m.Value)
                    })
                    .OrderBy(m => m.Name)
                    .ToList()
            })
            .OrderBy(g => g.CategoryName)
            .ToList();

        MetricGroups = groups;
    }

    private static string FormatCategoryName(string category)
    {
        // Convert snake_case to Title Case
        return string.Join(" ", category.Split('_')
            .Select(s => char.ToUpper(s[0]) + s[1..].ToLower()));
    }

    private static string FormatMetricName(string name)
    {
        // Common metric name formatting
        return name.Replace("_", " ").ToUpper();
    }

    private static string FormatMetricValue(string name, double value)
    {
        // Format as percentage for common metrics
        var lowerName = name.ToLower();
        if (lowerName.Contains("accuracy") ||
            lowerName.Contains("precision") ||
            lowerName.Contains("recall") ||
            lowerName.Contains("f1") ||
            lowerName.Contains("auc") ||
            lowerName.Contains("roc"))
        {
            return value <= 1.0
                ? (value * 100).ToString("F2") + "%"
                : value.ToString("F4");
        }

        // Format integers appropriately
        if (Math.Abs(value - Math.Round(value)) < 0.0001 && value < 1_000_000)
        {
            return value.ToString("N0");
        }

        return value.ToString("F4");
    }

    [RelayCommand]
    private async Task OpenRawJsonAsync()
    {
        if (string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            return;
        }

        var filePath = Path.Combine(
            _workspaceService.CurrentWorkspacePath,
            RunDir.Replace('/', Path.DirectorySeparatorChar),
            "interpretability",
            "metrics.v1.json");

        if (File.Exists(filePath))
        {
            try
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath)
                });
            }
            catch
            {
                // Silently ignore
            }
        }
    }

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            return;
        }

        StatusMessage = null;

        try
        {
            var suggestedName = $"metrics_{RunId ?? "run"}.csv";
            var outputPath = await Services.FileSavePickerService.SaveCsvFileAsync(suggestedName);

            if (string.IsNullOrEmpty(outputPath))
            {
                return; // User cancelled
            }

            var result = await _exportService.ExportMetricsToCsvAsync(
                _workspaceService.CurrentWorkspacePath,
                RunDir,
                outputPath);

            if (result.IsSuccess)
            {
                StatusMessage = $"Exported to {Path.GetFileName(outputPath)} ({result.BytesWritten:N0} bytes)";
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Export failed: {ex.Message}";
        }
    }
}

/// <summary>
/// Display item for a metric category group.
/// </summary>
public sealed class MetricCategoryGroup
{
    public required string CategoryName { get; init; }
    public required List<MetricItem> Metrics { get; init; }
}

/// <summary>
/// Display item for a single metric.
/// </summary>
public sealed class MetricItem
{
    public required string Name { get; init; }
    public double Value { get; init; }
    public required string ValueDisplay { get; init; }
}
