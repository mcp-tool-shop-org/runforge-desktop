using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using RunForgeDesktop.Core.Services;
using RunForgeDesktop.ViewModels;
using RunForgeDesktop.Views;

namespace RunForgeDesktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ═══════════════════════════════════════════════════════════════════
        // v1.0.0 Services
        // ═══════════════════════════════════════════════════════════════════

        // Core infrastructure
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IWorkspaceService, WorkspaceService>();
        builder.Services.AddSingleton<IStorageService, StorageService>();
        builder.Services.AddSingleton<ICrashRecoveryService, CrashRecoveryService>();

        // Python/training
        builder.Services.AddSingleton<IPythonDiscoveryService, PythonDiscoveryService>();
        builder.Services.AddSingleton<IRunnerService, RunnerService>();

        // Monitoring (for future activity strip)
        builder.Services.AddSingleton<IExecutionQueueService, ExecutionQueueService>();
        builder.Services.AddSingleton<IActivityMonitorService, ActivityMonitorService>();

        // Diagnostics (run count display)
        builder.Services.AddSingleton<IRunIndexService, RunIndexService>();

        // ═══════════════════════════════════════════════════════════════════
        // v1.0.0 ViewModels
        // ═══════════════════════════════════════════════════════════════════
        builder.Services.AddSingleton<WorkspaceDashboardViewModel>();
        builder.Services.AddSingleton<RunsDashboardViewModel>();
        builder.Services.AddTransient<NewRunViewModel>();
        builder.Services.AddTransient<LiveRunViewModel>();
        builder.Services.AddTransient<MultiRunViewModel>();
        builder.Services.AddTransient<DiagnosticsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddSingleton<ActivityStripViewModel>();

        // ═══════════════════════════════════════════════════════════════════
        // v1.0.0 Pages
        // ═══════════════════════════════════════════════════════════════════
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<WorkspaceDashboardPage>();
        builder.Services.AddTransient<RunsDashboardPage>();
        builder.Services.AddTransient<NewRunPage>();
        builder.Services.AddTransient<LiveRunPage>();
        builder.Services.AddTransient<MultiRunPage>();
        builder.Services.AddTransient<DiagnosticsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<HelpPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
