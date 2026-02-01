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

        // Register view models
        builder.Services.AddTransient<RunsListViewModel>();
        builder.Services.AddTransient<RunDetailViewModel>();

        // Register pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<RunsListPage>();
        builder.Services.AddTransient<RunDetailPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
