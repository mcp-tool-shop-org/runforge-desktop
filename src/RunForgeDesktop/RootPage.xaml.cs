using RunForgeDesktop.Core.Services;
using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop;

/// <summary>
/// Root page that wraps the AppShell with a persistent Activity Strip.
/// Part of the Visual Activity System.
/// </summary>
public partial class RootPage : ContentPage
{
    private readonly ActivityStripViewModel _activityStripViewModel;
    private readonly IWorkspaceService _workspaceService;

    public RootPage(
        IActivityMonitorService activityMonitorService,
        IWorkspaceService workspaceService,
        AppShell appShell)
    {
        InitializeComponent();

        _workspaceService = workspaceService;
        _activityStripViewModel = new ActivityStripViewModel(activityMonitorService);

        // Inject the AppShell into the container
        ShellContainer.Content = appShell;

        // Bind the activity strip to its view model
        ActivityStripControl.BindingContext = _activityStripViewModel;

        // Subscribe to workspace changes
        _workspaceService.WorkspaceChanged += OnWorkspaceChanged;

        // Start monitoring if we have a workspace
        StartMonitoringIfWorkspaceSet();
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await UpdateWorkspaceAsync(e.NewPath);
        });
    }

    private async void StartMonitoringIfWorkspaceSet()
    {
        try
        {
            if (!string.IsNullOrEmpty(_workspaceService.CurrentWorkspacePath))
            {
                await _activityStripViewModel.StartMonitoringAsync(_workspaceService.CurrentWorkspacePath);
            }
        }
        catch
        {
            // Silently ignore - activity strip will show idle state
        }
    }

    /// <summary>
    /// Update the activity monitor when workspace changes.
    /// </summary>
    private async Task UpdateWorkspaceAsync(string? workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath))
        {
            _activityStripViewModel.StopMonitoring();
        }
        else
        {
            await _activityStripViewModel.StartMonitoringAsync(workspacePath);
        }
    }
}
