using RunForgeDesktop.Core.Services;
using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICrashRecoveryService _crashRecovery;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _crashRecovery = serviceProvider.GetRequiredService<ICrashRecoveryService>();

        // Set up unhandled exception handlers
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Load and apply theme from settings (default to dark)
        _ = LoadAndApplyThemeAsync();

        // Recover any orphaned runs from previous crash
        _ = RecoverOrphanedRunsAsync();

        // Start crash recovery session
        _ = StartCrashRecoverySessionAsync();
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

    private async Task StartCrashRecoverySessionAsync()
    {
        try
        {
            // Check for previous crash first
            var recoveryInfo = await _crashRecovery.CheckForCrashRecoveryAsync();
            if (recoveryInfo.HasRecoverableSession)
            {
                // Show recovery dialog on main thread
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await ShowCrashRecoveryDialogAsync(recoveryInfo);
                });
            }

            // Start new session
            await _crashRecovery.StartSessionAsync();
        }
        catch
        {
            // Don't fail startup if crash recovery fails
        }
    }

    private async Task ShowCrashRecoveryDialogAsync(CrashRecoveryInfo recoveryInfo)
    {
        try
        {
            var page = Current?.Windows.FirstOrDefault()?.Page;
            if (page is null)
            {
                return;
            }

            var result = await page.DisplayAlert(
                "Session Recovery",
                $"RunForge Desktop did not close properly.\n\n{recoveryInfo.RecoveryDescription}\n\nWould you like to restore your previous session?",
                "Restore",
                "Discard");

            if (result)
            {
                // User wants to restore
                await RestorePreviousSessionAsync(recoveryInfo.SessionState!);
            }
            else
            {
                // User dismissed recovery
                await _crashRecovery.DismissRecoveryAsync();
            }
        }
        catch
        {
            // If dialog fails, just dismiss recovery
            await _crashRecovery.DismissRecoveryAsync();
        }
    }

    private async Task RestorePreviousSessionAsync(SessionState session)
    {
        try
        {
            // Restore workspace if available
            if (!string.IsNullOrEmpty(session.WorkspacePath))
            {
                var workspace = _serviceProvider.GetRequiredService<IWorkspaceService>();
                await workspace.SetWorkspaceAsync(session.WorkspacePath);
            }

            // Navigate to previous route if available
            if (!string.IsNullOrEmpty(session.CurrentRoute))
            {
                var shell = Current?.Windows.FirstOrDefault()?.Page as Shell;
                if (shell is not null)
                {
                    try
                    {
                        await shell.GoToAsync(session.CurrentRoute);
                    }
                    catch
                    {
                        // Route might not exist anymore, ignore
                    }
                }
            }

            // Dismiss the old session data
            await _crashRecovery.DismissRecoveryAsync();
        }
        catch
        {
            // Ignore restoration errors
            await _crashRecovery.DismissRecoveryAsync();
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        _ = _crashRecovery.WriteCrashLogAsync(
            "Unhandled exception occurred",
            exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _ = _crashRecovery.WriteCrashLogAsync(
            "Unobserved task exception",
            e.Exception);
        e.SetObserved(); // Prevent app termination
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var appShell = _serviceProvider.GetRequiredService<AppShell>();
        var window = new Window(appShell);

        // Handle window closing for clean shutdown
        window.Destroying += async (s, e) =>
        {
            try
            {
                await _crashRecovery.EndSessionCleanlyAsync();
            }
            catch
            {
                // Ignore cleanup errors during shutdown
            }
        };

        return window;
    }
}
