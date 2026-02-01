using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

/// <summary>
/// Page for browsing runs in the workspace.
/// </summary>
public partial class RunsListPage : ContentPage
{
    public RunsListPage(RunsListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Trigger initial load if workspace is already set
        if (BindingContext is RunsListViewModel vm && !string.IsNullOrEmpty(vm.WorkspacePath))
        {
            vm.LoadRunsCommand.Execute(null);
        }
    }
}
