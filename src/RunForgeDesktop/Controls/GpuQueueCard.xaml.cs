namespace RunForgeDesktop.Controls;

/// <summary>
/// Card showing GPU queue status: active slots and waiting jobs.
/// Part of the Visual Activity System.
/// </summary>
public partial class GpuQueueCard : ContentView
{
    public static readonly BindableProperty ActiveGpuSlotsProperty =
        BindableProperty.Create(
            nameof(ActiveGpuSlots),
            typeof(int),
            typeof(GpuQueueCard),
            0,
            propertyChanged: OnSlotsChanged);

    public static readonly BindableProperty TotalGpuSlotsProperty =
        BindableProperty.Create(
            nameof(TotalGpuSlots),
            typeof(int),
            typeof(GpuQueueCard),
            1,
            propertyChanged: OnSlotsChanged);

    public static readonly BindableProperty QueuedGpuCountProperty =
        BindableProperty.Create(
            nameof(QueuedGpuCount),
            typeof(int),
            typeof(GpuQueueCard),
            0,
            propertyChanged: OnQueuedChanged);

    public GpuQueueCard()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    public int ActiveGpuSlots
    {
        get => (int)GetValue(ActiveGpuSlotsProperty);
        set => SetValue(ActiveGpuSlotsProperty, value);
    }

    public int TotalGpuSlots
    {
        get => (int)GetValue(TotalGpuSlotsProperty);
        set => SetValue(TotalGpuSlotsProperty, value);
    }

    public int QueuedGpuCount
    {
        get => (int)GetValue(QueuedGpuCountProperty);
        set => SetValue(QueuedGpuCountProperty, value);
    }

    private static void OnSlotsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((GpuQueueCard)bindable).UpdateDisplay();
    }

    private static void OnQueuedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((GpuQueueCard)bindable).UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        // Update slot usage text
        SlotUsageLabel.Text = $"{ActiveGpuSlots}/{TotalGpuSlots} slots";

        // Update slot indicators
        SlotIndicators.Children.Clear();
        for (int i = 0; i < TotalGpuSlots; i++)
        {
            var isFilled = i < ActiveGpuSlots;
            var box = new BoxView
            {
                WidthRequest = 16,
                HeightRequest = 16,
                CornerRadius = 3,
                Color = isFilled
                    ? Color.FromArgb("#FF9800") // Orange for GPU active
                    : Color.FromArgb("#E0E0E0") // Gray for empty
            };
            SlotIndicators.Children.Add(box);
        }

        // Update waiting count
        if (QueuedGpuCount > 0)
        {
            WaitingLabel.Text = $"{QueuedGpuCount} job{(QueuedGpuCount > 1 ? "s" : "")} waiting for GPU";
            WaitingLabel.IsVisible = true;
        }
        else
        {
            WaitingLabel.IsVisible = false;
        }
    }
}
