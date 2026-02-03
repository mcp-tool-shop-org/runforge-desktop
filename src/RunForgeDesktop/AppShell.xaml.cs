using RunForgeDesktop.Core.Services;
using RunForgeDesktop.Views;
using System.Reflection;

namespace RunForgeDesktop;

public partial class AppShell : Shell
{
    public AppShell(IActivityMonitorService activityMonitor, IWorkspaceService workspaceService)
    {
        InitializeComponent();

        // Display version in title bar
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.9.0-rc.1";
        Title = $"RunForge Desktop v{version}";

        // ═══════════════════════════════════════════════════════════════════
        // v1.0.0 Routes - These are the only routes accessible from the UI
        // ═══════════════════════════════════════════════════════════════════

        // Task flows
        Routing.RegisterRoute("newrun", typeof(NewRunPage));
        Routing.RegisterRoute("rundetail", typeof(LiveRunPage));
        Routing.RegisterRoute("multirun", typeof(MultiRunPage));

        // Utility pages (accessible from Dashboard)
        Routing.RegisterRoute("diagnostics", typeof(DiagnosticsPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));

#if LEGACY_INSPECTION
        // ═══════════════════════════════════════════════════════════════════
        // Legacy inspection routes - disabled for v1.0.0
        // These were part of the original "read-only inspection" flow.
        // Enable with LEGACY_INSPECTION compile flag if needed for debugging.
        // ═══════════════════════════════════════════════════════════════════
        Routing.RegisterRoute(nameof(WorkspaceDashboardPage), typeof(WorkspaceDashboardPage));
        Routing.RegisterRoute(nameof(RunsListPage), typeof(RunsListPage));
        Routing.RegisterRoute(nameof(RunDetailPage), typeof(RunDetailPage));
        Routing.RegisterRoute(nameof(InterpretabilityPage), typeof(InterpretabilityPage));
        Routing.RegisterRoute(nameof(FeatureImportancePage), typeof(FeatureImportancePage));
        Routing.RegisterRoute(nameof(LinearCoefficientsPage), typeof(LinearCoefficientsPage));
        Routing.RegisterRoute(nameof(MetricsDetailPage), typeof(MetricsDetailPage));
        Routing.RegisterRoute(nameof(RequestEditorPage), typeof(RequestEditorPage));
        Routing.RegisterRoute(nameof(RunComparePage), typeof(RunComparePage));
        Routing.RegisterRoute(nameof(RunsDashboardPage), typeof(RunsDashboardPage));
#endif

        // Store services for future activity strip implementation
        _ = activityMonitor;
        _ = workspaceService;
    }
}
