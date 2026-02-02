using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

/// <summary>
/// Page for viewing run details and summary.
/// </summary>
public partial class RunDetailPage : ContentPage
{
    public RunDetailPage(RunDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
