using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunForgeDesktop.Core.Services;
using RunForgeDesktop.Services;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the Workspace Dashboard - the app's home screen.
/// </summary>
public partial class WorkspaceDashboardViewModel : ObservableObject
{
    private readonly IWorkspaceService _workspaceService;

    [ObservableProperty]
    private string? _workspacePath;

    public bool HasWorkspace => !string.IsNullOrEmpty(WorkspacePath);

    public WorkspaceDashboardViewModel(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;

        // Initialize from current workspace
        WorkspacePath = _workspaceService.CurrentWorkspacePath;

        // Listen for workspace changes
        _workspaceService.WorkspaceChanged += OnWorkspaceChanged;

        // Try to load last workspace on startup
        _ = TryLoadLastWorkspaceAsync();
    }

    partial void OnWorkspacePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasWorkspace));
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            WorkspacePath = e.NewPath;
        });
    }

    private async Task TryLoadLastWorkspaceAsync()
    {
        try
        {
            var lastPath = await _workspaceService.LoadLastWorkspaceAsync();
            if (lastPath is not null && Directory.Exists(lastPath))
            {
                var result = await _workspaceService.SetWorkspaceAsync(lastPath);
                if (result.IsValid)
                {
                    WorkspacePath = lastPath;
                }
            }
        }
        catch
        {
            // Silently ignore errors loading last workspace
        }
    }

    [RelayCommand]
    private async Task SelectWorkspaceAsync()
    {
        try
        {
            var folderPath = await FolderPickerService.PickFolderAsync();

            if (folderPath is not null)
            {
                var discoveryResult = await _workspaceService.SetWorkspaceAsync(folderPath);

                if (discoveryResult.IsValid)
                {
                    await _workspaceService.SaveLastWorkspaceAsync();
                    WorkspacePath = folderPath;

                    // Navigate to Runs tab
                    await Shell.Current.GoToAsync("//runs");
                }
                else
                {
                    await Shell.Current.DisplayAlert(
                        "Invalid Workspace",
                        $"{discoveryResult.ErrorMessage}\n\n" +
                        "Please select a folder containing RunForge outputs.",
                        "OK");
                }
            }
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrEmpty(ex.Message)
                ? $"{ex.GetType().Name}: {ex.InnerException?.Message ?? "Unknown error"}"
                : ex.Message;
            await Shell.Current.DisplayAlert(
                "Error",
                $"Failed to select workspace: {message}",
                "OK");
        }
    }

    [RelayCommand]
    private async Task BrowseRunsAsync()
    {
        if (HasWorkspace)
        {
            // Navigate to Runs tab
            await Shell.Current.GoToAsync("//runs");
        }
    }

    [RelayCommand]
    private async Task OpenDiagnosticsAsync()
    {
        await Shell.Current.GoToAsync("diagnostics");
    }
}
