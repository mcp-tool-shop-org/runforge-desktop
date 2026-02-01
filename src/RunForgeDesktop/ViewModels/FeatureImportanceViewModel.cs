using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the feature importance detail view.
/// </summary>
public partial class FeatureImportanceViewModel : ObservableObject, IQueryAttributable
{
    private readonly IInterpretabilityService _interpretabilityService;
    private readonly IWorkspaceService _workspaceService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _runId;

    [ObservableProperty]
    private string? _runName;

    [ObservableProperty]
    private string? _runDir;

    [ObservableProperty]
    private FeatureImportanceV1? _artifact;

    [ObservableProperty]
    private List<FeatureImportanceItem> _features = [];

    [ObservableProperty]
    private int _displayCount = 20;

    public FeatureImportanceViewModel(
        IInterpretabilityService interpretabilityService,
        IWorkspaceService workspaceService)
    {
        _interpretabilityService = interpretabilityService;
        _workspaceService = workspaceService;
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

            var entry = indexResult.Index.GetArtifact("feature_importance.v1");
            if (entry is null || !entry.Available)
            {
                ErrorMessage = "Feature importance artifact not available";
                return;
            }

            Artifact = await _interpretabilityService.LoadArtifactAsync<FeatureImportanceV1>(
                _workspaceService.CurrentWorkspacePath,
                RunDir,
                entry.Path);

            if (Artifact is not null)
            {
                UpdateFeatureList();
            }
            else
            {
                ErrorMessage = "Failed to load feature importance data";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load feature importance: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ShowMore()
    {
        DisplayCount = Math.Min(DisplayCount + 20, Artifact?.Importances.Count ?? 0);
        UpdateFeatureList();
    }

    [RelayCommand]
    private void ShowAll()
    {
        DisplayCount = Artifact?.Importances.Count ?? 0;
        UpdateFeatureList();
    }

    private void UpdateFeatureList()
    {
        if (Artifact is null)
        {
            Features = [];
            return;
        }

        // Find max importance for scaling
        var maxImportance = Artifact.Importances.Values.Max();

        var items = Artifact.Importances
            .OrderByDescending(x => x.Value)
            .Take(DisplayCount)
            .Select((kv, index) => new FeatureImportanceItem
            {
                Rank = index + 1,
                FeatureName = kv.Key,
                Importance = kv.Value,
                PercentDisplay = (kv.Value * 100).ToString("F2") + "%",
                BarWidthPercent = maxImportance > 0 ? (kv.Value / maxImportance) * 100 : 0
            })
            .ToList();

        Features = items;
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
            "feature_importance.v1.json");

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
}

/// <summary>
/// Display item for feature importance list.
/// </summary>
public sealed class FeatureImportanceItem
{
    public int Rank { get; init; }
    public required string FeatureName { get; init; }
    public double Importance { get; init; }
    public required string PercentDisplay { get; init; }
    public double BarWidthPercent { get; init; }
}
