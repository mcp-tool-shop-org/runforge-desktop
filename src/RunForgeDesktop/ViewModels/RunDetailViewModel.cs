using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Models;
using RunForgeDesktop.Core.Services;
using RunForgeDesktop.Views;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the run detail page.
/// </summary>
public partial class RunDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IRunDetailService _runDetailService;
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
    private RunRequest? _request;

    [ObservableProperty]
    private RunResult? _result;

    [ObservableProperty]
    private TrainingMetrics? _metrics;

    public RunDetailViewModel(IRunDetailService runDetailService, IWorkspaceService workspaceService)
    {
        _runDetailService = runDetailService;
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
            _ = LoadRunDetailAsync();
        }
    }

    [RelayCommand]
    private async Task LoadRunDetailAsync()
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
            var loadResult = await _runDetailService.LoadRunDetailAsync(
                _workspaceService.CurrentWorkspacePath,
                RunDir);

            if (loadResult.IsSuccess)
            {
                Request = loadResult.Request;
                Result = loadResult.Result;
                Metrics = loadResult.Metrics;
            }
            else
            {
                ErrorMessage = loadResult.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load run details: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenRawFileAsync(string fileName)
    {
        if (string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            return;
        }

        var filePath = Path.Combine(
            _workspaceService.CurrentWorkspacePath,
            RunDir.Replace('/', Path.DirectorySeparatorChar),
            fileName);

        if (File.Exists(filePath))
        {
            try
            {
                // Open in default application
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
    private async Task NavigateToInterpretabilityAsync()
    {
        if (string.IsNullOrEmpty(RunId) || string.IsNullOrEmpty(RunDir))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { "runId", RunId },
            { "runName", RunName ?? RunId },
            { "runDir", RunDir }
        };

        await Shell.Current.GoToAsync(nameof(InterpretabilityPage), parameters);
    }
}
