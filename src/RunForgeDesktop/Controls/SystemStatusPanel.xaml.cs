using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.Controls;

/// <summary>
/// Contextual panel showing system status: Idle, Busy, or Stalled.
/// Part of the Visual Activity System.
/// </summary>
public partial class SystemStatusPanel : ContentView
{
    public static readonly BindableProperty SystemStateProperty =
        BindableProperty.Create(
            nameof(SystemState),
            typeof(ActivitySystemState),
            typeof(SystemStatusPanel),
            ActivitySystemState.Idle,
            propertyChanged: OnSystemStateChanged);

    public static readonly BindableProperty StatusReasonProperty =
        BindableProperty.Create(
            nameof(StatusReason),
            typeof(string),
            typeof(SystemStatusPanel),
            null,
            propertyChanged: OnStatusReasonChanged);

    public static readonly BindableProperty RunningCountProperty =
        BindableProperty.Create(
            nameof(RunningCount),
            typeof(int),
            typeof(SystemStatusPanel),
            0,
            propertyChanged: OnCountsChanged);

    public static readonly BindableProperty QueuedCountProperty =
        BindableProperty.Create(
            nameof(QueuedCount),
            typeof(int),
            typeof(SystemStatusPanel),
            0,
            propertyChanged: OnCountsChanged);

    public static readonly BindableProperty GpuRunningCountProperty =
        BindableProperty.Create(
            nameof(GpuRunningCount),
            typeof(int),
            typeof(SystemStatusPanel),
            0,
            propertyChanged: OnCountsChanged);

    public SystemStatusPanel()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    public ActivitySystemState SystemState
    {
        get => (ActivitySystemState)GetValue(SystemStateProperty);
        set => SetValue(SystemStateProperty, value);
    }

    public string? StatusReason
    {
        get => (string?)GetValue(StatusReasonProperty);
        set => SetValue(StatusReasonProperty, value);
    }

    public int RunningCount
    {
        get => (int)GetValue(RunningCountProperty);
        set => SetValue(RunningCountProperty, value);
    }

    public int QueuedCount
    {
        get => (int)GetValue(QueuedCountProperty);
        set => SetValue(QueuedCountProperty, value);
    }

    public int GpuRunningCount
    {
        get => (int)GetValue(GpuRunningCountProperty);
        set => SetValue(GpuRunningCountProperty, value);
    }

    private static void OnSystemStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((SystemStatusPanel)bindable).UpdateDisplay();
    }

    private static void OnStatusReasonChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((SystemStatusPanel)bindable).UpdateDisplay();
    }

    private static void OnCountsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((SystemStatusPanel)bindable).UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        switch (SystemState)
        {
            case ActivitySystemState.Idle:
                StatusIcon.Text = "\u2713"; // ✓
                StatusIcon.TextColor = Color.FromArgb("#4CAF50"); // Green
                StatusTitle.Text = "System idle";
                StatusTitle.TextColor = Color.FromArgb("#4CAF50");
                StatusSubtitle.Text = StatusReason ?? "Ready for work";
                break;

            case ActivitySystemState.Busy:
                StatusIcon.Text = "\u25b6"; // ▶
                StatusIcon.TextColor = Color.FromArgb("#2196F3"); // Blue
                StatusTitle.Text = "Executing";
                StatusTitle.TextColor = Color.FromArgb("#2196F3");
                StatusSubtitle.Text = BuildBusySubtitle();
                break;

            case ActivitySystemState.Stalled:
                StatusIcon.Text = "\u26a0"; // ⚠
                StatusIcon.TextColor = Color.FromArgb("#FFA726"); // Amber
                StatusTitle.Text = "Stalled";
                StatusTitle.TextColor = Color.FromArgb("#FFA726");
                StatusSubtitle.Text = StatusReason ?? "No recent activity";
                break;

            case ActivitySystemState.Error:
                StatusIcon.Text = "\u26d4"; // ⛔
                StatusIcon.TextColor = Color.FromArgb("#F44336"); // Red
                StatusTitle.Text = "Error";
                StatusTitle.TextColor = Color.FromArgb("#F44336");
                StatusSubtitle.Text = StatusReason ?? "Daemon not running";
                break;
        }
    }

    private string BuildBusySubtitle()
    {
        var parts = new List<string>();

        if (RunningCount > 0)
        {
            var cpuCount = RunningCount - GpuRunningCount;
            if (cpuCount > 0)
                parts.Add($"{cpuCount} CPU job{(cpuCount > 1 ? "s" : "")}");
            if (GpuRunningCount > 0)
                parts.Add($"{GpuRunningCount} GPU job{(GpuRunningCount > 1 ? "s" : "")}");
        }

        if (QueuedCount > 0)
        {
            parts.Add($"{QueuedCount} queued");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "Working...";
    }
}
