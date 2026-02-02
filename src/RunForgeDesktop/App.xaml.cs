using RunForgeDesktop.Core.Services;
using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;

        // Load and apply theme from settings (default to dark)
        _ = LoadAndApplyThemeAsync();

        // Recover any orphaned runs from previous crash
        _ = RecoverOrphanedRunsAsync();
    }

    private async Task LoadAndApplyThemeAsync()
    {
        try
        {
            var settings = _serviceProvider.GetRequiredService<ISettingsService>();
            await settings.LoadAsync();
            ApplyTheme(settings.AppTheme);
        }
        catch
        {
            // Default to dark on error
            UserAppTheme = AppTheme.Dark;
        }
    }

    /// <summary>
    /// Applies the specified theme to the application.
    /// </summary>
    /// <param name="theme">Theme name: "Dark", "Light", or "System"</param>
    public void ApplyTheme(string theme)
    {
        UserAppTheme = theme switch
        {
            "Light" => AppTheme.Light,
            "System" => AppTheme.Unspecified,
            _ => AppTheme.Dark  // Default to dark
        };
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
