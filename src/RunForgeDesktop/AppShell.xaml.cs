using RunForgeDesktop.Views;

namespace RunForgeDesktop;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute(nameof(RunsListPage), typeof(RunsListPage));
        Routing.RegisterRoute(nameof(RunDetailPage), typeof(RunDetailPage));
        Routing.RegisterRoute(nameof(InterpretabilityPage), typeof(InterpretabilityPage));
        Routing.RegisterRoute(nameof(FeatureImportancePage), typeof(FeatureImportancePage));
        Routing.RegisterRoute(nameof(LinearCoefficientsPage), typeof(LinearCoefficientsPage));
        Routing.RegisterRoute(nameof(MetricsDetailPage), typeof(MetricsDetailPage));
        Routing.RegisterRoute(nameof(DiagnosticsPage), typeof(DiagnosticsPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }
}
