using CommunityToolkit.Mvvm.ComponentModel;
using RunForgeDesktop.Core.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RunForgeDesktop.ViewModels;

/// <summary>
/// ViewModel for the global Activity Strip control.
/// Wraps IActivityMonitorService and provides UI-friendly properties.
/// </summary>
public partial class ActivityStripViewModel : ObservableObject
{
    private readonly IActivityMonitorService _activityMonitor;

    public ActivityStripViewModel(IActivityMonitorService activityMonitor)
    {
        _activityMonitor = activityMonitor;
        _activityMonitor.PropertyChanged += OnActivityMonitorPropertyChanged;

        // Initialize slot collections
        UpdateSlotIndicators();
    }

    private void OnActivityMonitorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Forward all property changes and update derived values
        switch (e.PropertyName)
        {
            case nameof(IActivityMonitorService.ActiveCpuSlots):
            case nameof(IActivityMonitorService.TotalCpuSlots):
            case nameof(IActivityMonitorService.ActiveGpuSlots):
            case nameof(IActivityMonitorService.TotalGpuSlots):
                UpdateSlotIndicators();
                break;

            case nameof(IActivityMonitorService.QueuedCount):
                OnPropertyChanged(nameof(QueueCountText));
                OnPropertyChanged(nameof(ShowQueueCount));
                break;

            case nameof(IActivityMonitorService.DaemonHealthy):
            case nameof(IActivityMonitorService.DaemonRunning):
            case nameof(IActivityMonitorService.DaemonStateText):
                OnPropertyChanged(nameof(DaemonStateIcon));
                OnPropertyChanged(nameof(DaemonStateColor));
                break;

            case nameof(IActivityMonitorService.SystemState):
            case nameof(IActivityMonitorService.StatusReason):
                OnPropertyChanged(nameof(SystemStateText));
                OnPropertyChanged(nameof(SystemStateColor));
                break;

            case nameof(IActivityMonitorService.HasGpuSlots):
                OnPropertyChanged(nameof(HasGpuSlots));
                break;
        }
    }

    // === Slot Indicators ===
    public ObservableCollection<SlotIndicatorModel> CpuSlots { get; } = [];
    public ObservableCollection<SlotIndicatorModel> GpuSlots { get; } = [];

    private void UpdateSlotIndicators()
    {
        // Update CPU slots
        UpdateSlotCollection(CpuSlots, _activityMonitor.ActiveCpuSlots, _activityMonitor.TotalCpuSlots);

        // Update GPU slots
        UpdateSlotCollection(GpuSlots, _activityMonitor.ActiveGpuSlots, _activityMonitor.TotalGpuSlots);

        OnPropertyChanged(nameof(CpuSlotSummary));
        OnPropertyChanged(nameof(GpuSlotSummary));
    }

    private static void UpdateSlotCollection(ObservableCollection<SlotIndicatorModel> slots, int active, int total)
    {
        // Ensure collection has correct number of items
        while (slots.Count < total)
            slots.Add(new SlotIndicatorModel());
        while (slots.Count > total)
            slots.RemoveAt(slots.Count - 1);

        // Update filled state
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].IsFilled = i < active;
        }
    }

    // === Display Properties ===
    public string CpuSlotSummary => $"CPU {_activityMonitor.ActiveCpuSlots}/{_activityMonitor.TotalCpuSlots}";
    public string GpuSlotSummary => $"GPU {_activityMonitor.ActiveGpuSlots}/{_activityMonitor.TotalGpuSlots}";

    public bool HasGpuSlots => _activityMonitor.HasGpuSlots;

    public string QueueCountText => _activityMonitor.QueuedCount > 0
        ? $"\u23f3 {_activityMonitor.QueuedCount}"  // ⏳ unicode
        : "";

    public bool ShowQueueCount => _activityMonitor.QueuedCount > 0;

    public string DaemonStateIcon => _activityMonitor.DaemonRunning
        ? (_activityMonitor.DaemonHealthy ? "\u25b6" : "\u26a0")  // ▶ or ⚠
        : "\u23f9";  // ⏹

    public Color DaemonStateColor
    {
        get
        {
            if (!_activityMonitor.DaemonRunning)
                return Color.FromArgb("#9E9E9E"); // Gray - stopped

            if (!_activityMonitor.DaemonHealthy)
                return Color.FromArgb("#FFA726"); // Amber - unhealthy

            return Color.FromArgb("#4CAF50"); // Green - healthy
        }
    }

    public string SystemStateText => _activityMonitor.SystemState switch
    {
        ActivitySystemState.Idle => "Idle",
        ActivitySystemState.Busy => _activityMonitor.StatusReason ?? "Busy",
        ActivitySystemState.Stalled => "Stalled",
        ActivitySystemState.Error => "Error",
        _ => ""
    };

    public Color SystemStateColor => _activityMonitor.SystemState switch
    {
        ActivitySystemState.Idle => Color.FromArgb("#9CA3AF"),    // Gray - idle/ready
        ActivitySystemState.Busy => Color.FromArgb("#3B82F6"),    // Blue - working
        ActivitySystemState.Stalled => Color.FromArgb("#F59E0B"), // Amber - warning
        ActivitySystemState.Error => Color.FromArgb("#EF4444"),   // Red - error
        _ => Color.FromArgb("#9CA3AF")                            // Gray fallback
    };

    // === Monitor Control ===
    public async Task StartMonitoringAsync(string workspacePath)
    {
        await _activityMonitor.StartAsync(workspacePath);
    }

    public void StopMonitoring()
    {
        _activityMonitor.Stop();
    }
}

/// <summary>
/// Model for a single slot indicator in the activity strip.
/// </summary>
public partial class SlotIndicatorModel : ObservableObject
{
    [ObservableProperty]
    private bool _isFilled;
}
