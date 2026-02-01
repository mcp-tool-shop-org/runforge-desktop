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
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register services
        builder.Services.AddSingleton<IWorkspaceService, WorkspaceService>();
        builder.Services.AddSingleton<IRunIndexService, RunIndexService>();
        builder.Services.AddSingleton<IRunDetailService, RunDetailService>();
        builder.Services.AddSingleton<IInterpretabilityService, InterpretabilityService>();
        builder.Services.AddSingleton<IExportService, ExportService>();
        builder.Services.AddSingleton<ILiveLogService, LiveLogService>();
        builder.Services.AddSingleton<IRunTimelineService, RunTimelineService>();
        builder.Services.AddSingleton<IStorageService, StorageService>();
        builder.Services.AddSingleton<IPythonDiscoveryService, PythonDiscoveryService>();
        builder.Services.AddSingleton<ICliExecutionService, CliExecutionService>();
        builder.Services.AddSingleton<IRunCreationService, RunCreationService>();
        builder.Services.AddSingleton<IRunRequestService, RunRequestService>();

        // Register view models
        // RunsListViewModel is singleton so filter/search state persists across navigation
        builder.Services.AddSingleton<RunsListViewModel>();
        builder.Services.AddTransient<RunDetailViewModel>();
        builder.Services.AddTransient<InterpretabilityViewModel>();
        builder.Services.AddTransient<FeatureImportanceViewModel>();
        builder.Services.AddTransient<LinearCoefficientsViewModel>();
        builder.Services.AddTransient<MetricsDetailViewModel>();
        builder.Services.AddTransient<DiagnosticsViewModel>();

        // Register pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<RunsListPage>();
        builder.Services.AddTransient<RunDetailPage>();
        builder.Services.AddTransient<InterpretabilityPage>();
        builder.Services.AddTransient<FeatureImportancePage>();
        builder.Services.AddTransient<LinearCoefficientsPage>();
        builder.Services.AddTransient<MetricsDetailPage>();
        builder.Services.AddTransient<DiagnosticsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
