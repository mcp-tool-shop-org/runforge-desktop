using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

public partial class RequestEditorPage : ContentPage
{
    public RequestEditorPage(RequestEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
