using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

/// <summary>
/// Dashboard page that serves as the app's home screen.
/// Shows workspace status and execution activity status.
/// </summary>
public partial class WorkspaceDashboardPage : ContentPage
{
    public WorkspaceDashboardPage(WorkspaceDashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Set build stamp for version verification
        var asm = typeof(WorkspaceDashboardPage).Assembly;
        var buildTime = File.GetLastWriteTime(asm.Location);
        BuildStampLabel.Text = $"v0.5.1 | {buildTime:HH:mm:ss}";
    }
}
