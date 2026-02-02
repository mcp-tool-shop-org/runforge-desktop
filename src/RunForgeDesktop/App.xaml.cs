using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;

        // Default to dark theme
        UserAppTheme = AppTheme.Dark;

        // Recover any orphaned runs from previous crash
        _ = RecoverOrphanedRunsAsync();
    }

    private async Task RecoverOrphanedRunsAsync()
    {
        try
        {
            var runnerService = _serviceProvider.GetRequiredService<IRunnerService>();
            await runnerService.RecoverOrphanedRunsAsync();
        }
        catch
        {
            // Ignore startup recovery errors
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var appShell = _serviceProvider.GetRequiredService<AppShell>();
        return new Window(appShell);
    }
}
