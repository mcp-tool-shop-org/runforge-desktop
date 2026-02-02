using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;
using RunForgeDesktop.Views;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the interpretability index view.
/// </summary>
public partial class InterpretabilityViewModel : ObservableObject, IQueryAttributable
{
    private readonly IInterpretabilityService _interpretabilityService;
    private readonly IWorkspaceService _workspaceService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _notFound;

    [ObservableProperty]
    private string? _runId;

    [ObservableProperty]
    private string? _runName;

    [ObservableProperty]
    private string? _runDir;

    [ObservableProperty]
    private InterpretabilityIndexV1? _index;

    [ObservableProperty]
    private MetricsV1? _metricsArtifact;

    [ObservableProperty]
    private FeatureImportanceV1? _featureImportance;

    [ObservableProperty]
    private LinearCoefficientsV1? _linearCoefficients;

    public InterpretabilityViewModel(
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
            _ = LoadInterpretabilityAsync();
        }
    }

    [RelayCommand]
    private async Task LoadInterpretabilityAsync()
    {
        if (string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            ErrorMessage = "Invalid run or workspace";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        NotFound = false;

        try
        {
            var loadResult = await _interpretabilityService.LoadIndexAsync(
                _workspaceService.CurrentWorkspacePath,
                RunDir);

            if (loadResult.IsSuccess && loadResult.Index is not null)
            {
                Index = loadResult.Index;
                // Load available artifacts
                await LoadArtifactsAsync();
            }
            else if (loadResult.NotFound)
            {
                NotFound = true;
                ErrorMessage = loadResult.ErrorMessage;
            }
            else
            {
                ErrorMessage = loadResult.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load interpretability index: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadArtifactsAsync()
    {
        if (Index is null || string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            return;
        }

        // Load metrics v1 if available
        var metricsEntry = Index.GetArtifact("metrics.v1");
        if (metricsEntry?.Available == true)
        {
            MetricsArtifact = await _interpretabilityService.LoadArtifactAsync<MetricsV1>(
                _workspaceService.CurrentWorkspacePath,
                RunDir,
                metricsEntry.Path);
        }

        // Load feature importance if available
        var featureEntry = Index.GetArtifact("feature_importance.v1");
        if (featureEntry?.Available == true)
        {
            FeatureImportance = await _interpretabilityService.LoadArtifactAsync<FeatureImportanceV1>(
                _workspaceService.CurrentWorkspacePath,
                RunDir,
                featureEntry.Path);
        }

        // Load linear coefficients if available
        var coefficientsEntry = Index.GetArtifact("linear_coefficients.v1");
        if (coefficientsEntry?.Available == true)
        {
            LinearCoefficients = await _interpretabilityService.LoadArtifactAsync<LinearCoefficientsV1>(
                _workspaceService.CurrentWorkspacePath,
                RunDir,
                coefficientsEntry.Path);
        }
    }

    [RelayCommand]
    private async Task OpenArtifactAsync(string artifactType)
    {
        if (Index is null || string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            return;
        }

        var entry = Index.GetArtifact(artifactType);
        if (entry is null)
        {
            return;
        }

        var filePath = Path.Combine(
            _workspaceService.CurrentWorkspacePath,
            RunDir.Replace('/', Path.DirectorySeparatorChar),
            entry.Path.Replace('/', Path.DirectorySeparatorChar));

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
                // Silently ignore if we can't open
            }
        }
    }

    [RelayCommand]
    private async Task NavigateToMetricsDetailAsync()
    {
        if (string.IsNullOrEmpty(RunDir))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { "runId", RunId ?? "" },
            { "runName", RunName ?? "" },
            { "runDir", RunDir }
        };

        await Shell.Current.GoToAsync(nameof(MetricsDetailPage), parameters);
    }

    [RelayCommand]
    private async Task NavigateToFeatureImportanceAsync()
    {
        if (string.IsNullOrEmpty(RunDir))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { "runId", RunId ?? "" },
            { "runName", RunName ?? "" },
            { "runDir", RunDir }
        };

        await Shell.Current.GoToAsync(nameof(FeatureImportancePage), parameters);
    }

    [RelayCommand]
    private async Task NavigateToLinearCoefficientsAsync()
    {
        if (string.IsNullOrEmpty(RunDir))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { "runId", RunId ?? "" },
            { "runName", RunName ?? "" },
            { "runDir", RunDir }
        };

        await Shell.Current.GoToAsync(nameof(LinearCoefficientsPage), parameters);
    }
}
