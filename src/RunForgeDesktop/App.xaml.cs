using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Create RootPage with activity strip wrapping the shell
        var activityMonitor = _serviceProvider.GetRequiredService<IActivityMonitorService>();
        var workspaceService = _serviceProvider.GetRequiredService<IWorkspaceService>();

        var rootPage = new RootPage(activityMonitor, workspaceService);
        return new Window(rootPage);
    }
}
