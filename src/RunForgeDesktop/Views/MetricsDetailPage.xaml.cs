using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

/// <summary>
/// Page displaying detailed metrics information.
/// </summary>
public partial class MetricsDetailPage : ContentPage
{
    public MetricsDetailPage(MetricsDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
