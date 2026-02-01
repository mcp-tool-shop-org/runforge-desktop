namespace RunForgeDesktop;

/// <summary>
/// Main landing page for RunForge Desktop.
/// Displays workspace selection and navigation to run browsing.
/// </summary>
public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnSelectWorkspaceClicked(object? sender, EventArgs e)
    {
        try
        {
            // TODO: Implement workspace selection via FolderPicker
            // For now, show placeholder message
            await DisplayAlertAsync(
                "Coming Soon",
                "Workspace selection will be implemented in the next commit.\n\n" +
                "This will allow you to select a folder containing RunForge outputs.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(
                "Error",
                $"Failed to select workspace: {ex.Message}",
                "OK");
        }
    }
}
