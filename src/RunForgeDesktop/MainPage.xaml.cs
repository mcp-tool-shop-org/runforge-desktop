using RunForgeDesktop.Core.Services;
using RunForgeDesktop.Services;
using RunForgeDesktop.Views;

namespace RunForgeDesktop;

/// <summary>
/// Main landing page for RunForge Desktop.
/// Displays workspace selection and navigation to run browsing.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly IWorkspaceService _workspaceService;

    public MainPage(IWorkspaceService workspaceService)
    {
        InitializeComponent();
        _workspaceService = workspaceService;

        // Try to load last workspace on startup
        _ = TryLoadLastWorkspaceAsync();
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
                    // Navigate directly to runs list
                    await NavigateToRunsListAsync();
                }
            }
        }
        catch
        {
            // Silently ignore errors loading last workspace
        }
    }

    private async void OnSelectWorkspaceClicked(object? sender, EventArgs e)
    {
        try
        {
            // Use Windows-native folder picker
            var folderPath = await FolderPickerService.PickFolderAsync();

            if (folderPath is not null)
            {
                var discoveryResult = await _workspaceService.SetWorkspaceAsync(folderPath);

                if (discoveryResult.IsValid)
                {
                    await _workspaceService.SaveLastWorkspaceAsync();
                    await NavigateToRunsListAsync();
                }
                else
                {
                    await DisplayAlertAsync(
                        "Invalid Workspace",
                        $"{discoveryResult.ErrorMessage}\n\n" +
                        $"Please select a folder containing RunForge outputs " +
                        $"(look for .ml/outputs/index.json or .ml/runs/ directory).",
                        "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(
                "Error",
                $"Failed to select workspace: {ex.Message}",
                "OK");
        }
    }

    private async Task NavigateToRunsListAsync()
    {
        await Shell.Current.GoToAsync(nameof(RunsListPage));
    }
}
