using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

/// <summary>
/// Page displaying detailed feature importance information.
/// </summary>
public partial class FeatureImportancePage : ContentPage
{
    public FeatureImportancePage(FeatureImportanceViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
