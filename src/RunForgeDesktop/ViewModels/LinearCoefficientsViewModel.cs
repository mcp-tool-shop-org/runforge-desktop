using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the linear coefficients detail view.
/// </summary>
public partial class LinearCoefficientsViewModel : ObservableObject, IQueryAttributable
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
    private LinearCoefficientsV1? _artifact;

    [ObservableProperty]
    private List<string> _classLabels = [];

    [ObservableProperty]
    private string? _selectedClass;

    [ObservableProperty]
    private double _selectedIntercept;

    [ObservableProperty]
    private List<CoefficientItem> _coefficients = [];

    [ObservableProperty]
    private int _displayCount = 20;

    public LinearCoefficientsViewModel(
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

    partial void OnSelectedClassChanged(string? value)
    {
        if (value is not null)
        {
            DisplayCount = 20;
            UpdateCoefficientList();
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

            var entry = indexResult.Index.GetArtifact("linear_coefficients.v1");
            if (entry is null || !entry.Available)
            {
                ErrorMessage = "Linear coefficients artifact not available";
                return;
            }

            Artifact = await _interpretabilityService.LoadArtifactAsync<LinearCoefficientsV1>(
                _workspaceService.CurrentWorkspacePath,
                RunDir,
                entry.Path);

            if (Artifact is not null)
            {
                ClassLabels = Artifact.ClassLabels.ToList();
                if (ClassLabels.Count > 0)
                {
                    SelectedClass = ClassLabels[0];
                }
            }
            else
            {
                ErrorMessage = "Failed to load linear coefficients data";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load linear coefficients: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ShowMore()
    {
        DisplayCount = Math.Min(DisplayCount + 20, GetCurrentCoefficientsCount());
        UpdateCoefficientList();
    }

    [RelayCommand]
    private void ShowAll()
    {
        DisplayCount = GetCurrentCoefficientsCount();
        UpdateCoefficientList();
    }

    private int GetCurrentCoefficientsCount()
    {
        if (Artifact is null || string.IsNullOrEmpty(SelectedClass))
        {
            return 0;
        }

        return Artifact.Coefficients.TryGetValue(SelectedClass, out var coeffs)
            ? coeffs.Count
            : 0;
    }

    private void UpdateCoefficientList()
    {
        if (Artifact is null || string.IsNullOrEmpty(SelectedClass))
        {
            Coefficients = [];
            SelectedIntercept = 0;
            return;
        }

        // Update intercept
        if (Artifact.Intercepts.TryGetValue(SelectedClass, out var intercept))
        {
            SelectedIntercept = intercept;
        }

        if (!Artifact.Coefficients.TryGetValue(SelectedClass, out var classCoeffs))
        {
            Coefficients = [];
            return;
        }

        // Find max absolute coefficient for scaling
        var maxAbsCoeff = classCoeffs.Values.Max(Math.Abs);

        var items = classCoeffs
            .OrderByDescending(x => Math.Abs(x.Value))
            .Take(DisplayCount)
            .Select((kv, index) => new CoefficientItem
            {
                Rank = index + 1,
                FeatureName = kv.Key,
                Coefficient = kv.Value,
                CoefficientDisplay = kv.Value.ToString("F6"),
                IsPositive = kv.Value >= 0,
                BarWidthPercent = maxAbsCoeff > 0 ? (Math.Abs(kv.Value) / maxAbsCoeff) * 100 : 0
            })
            .ToList();

        Coefficients = items;
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
            "linear_coefficients.v1.json");

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
/// Display item for coefficient list.
/// </summary>
public sealed class CoefficientItem
{
    public int Rank { get; init; }
    public required string FeatureName { get; init; }
    public double Coefficient { get; init; }
    public required string CoefficientDisplay { get; init; }
    public bool IsPositive { get; init; }
    public double BarWidthPercent { get; init; }
}
