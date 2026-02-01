using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Json;
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

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _showRawJsonModal;

    [ObservableProperty]
    private string? _rawJsonContent;

    [ObservableProperty]
    private string? _rawJsonTitle;

    /// <summary>
    /// Whether the request has optional fields to display.
    /// </summary>
    public bool HasRequestExtras => Request is not null &&
        (!string.IsNullOrEmpty(Request.Name) ||
         !string.IsNullOrEmpty(Request.RerunFrom) ||
         (Request.Tags is not null && Request.Tags.Count > 0) ||
         !string.IsNullOrEmpty(Request.Notes) ||
         !string.IsNullOrEmpty(Request.Device.GpuReason));

    /// <summary>
    /// Formatted tags string.
    /// </summary>
    public string? TagsDisplay => Request?.Tags is not null && Request.Tags.Count > 0
        ? string.Join(", ", Request.Tags)
        : null;

    /// <summary>
    /// Formatted created_at timestamp.
    /// </summary>
    public string? FormattedCreatedAt
    {
        get
        {
            if (Request?.ParsedCreatedAt is { } parsed)
            {
                return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
            return Request?.CreatedAt;
        }
    }

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

    partial void OnRequestChanged(RunRequest? value)
    {
        OnPropertyChanged(nameof(HasRequestExtras));
        OnPropertyChanged(nameof(TagsDisplay));
        OnPropertyChanged(nameof(FormattedCreatedAt));
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
        StatusMessage = null;

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
    private async Task OpenRunFolderAsync()
    {
        if (string.IsNullOrEmpty(RunDir) || string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
        {
            return;
        }

        var folderPath = Path.Combine(
            _workspaceService.CurrentWorkspacePath,
            RunDir.Replace('/', Path.DirectorySeparatorChar));

        if (Directory.Exists(folderPath))
        {
            try
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(folderPath)
                });
            }
            catch
            {
                // Silently ignore if we can't open
            }
        }
    }

    [RelayCommand]
    private async Task CopyRequestJsonAsync()
    {
        if (Request is null)
        {
            StatusMessage = "No request data available";
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(Request, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            await Clipboard.Default.SetTextAsync(json);
            StatusMessage = "Request JSON copied to clipboard";

            // Clear status after 3 seconds
            _ = ClearStatusAfterDelayAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ViewRequestJson()
    {
        if (Request is null)
        {
            return;
        }

        try
        {
            RawJsonContent = JsonSerializer.Serialize(Request, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            RawJsonTitle = "Request JSON";
            ShowRawJsonModal = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to format JSON: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CloseRawJsonModal()
    {
        ShowRawJsonModal = false;
        RawJsonContent = null;
        RawJsonTitle = null;
    }

    [RelayCommand]
    private async Task CopyRawJsonAsync()
    {
        if (string.IsNullOrEmpty(RawJsonContent))
        {
            return;
        }

        try
        {
            await Clipboard.Default.SetTextAsync(RawJsonContent);
            StatusMessage = "JSON copied to clipboard";
            _ = ClearStatusAfterDelayAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy: {ex.Message}";
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

    private async Task ClearStatusAfterDelayAsync()
    {
        await Task.Delay(3000);
        if (StatusMessage is not null && !StatusMessage.StartsWith("Failed"))
        {
            StatusMessage = null;
        }
    }
}
