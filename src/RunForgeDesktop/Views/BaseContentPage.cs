using RunForgeDesktop.Controls;
using RunForgeDesktop.ViewModels;

namespace RunForgeDesktop.Views;

/// <summary>
/// Base page that provides the global Activity Strip header.
/// All app pages should inherit from this to get consistent global UI.
///
/// The ActivityStrip is injected into Shell.TitleView, ensuring it appears
/// on every page without manual duplication. NavBar remains visible but
/// we replace its content with our strip.
/// </summary>
public class BaseContentPage : ContentPage
{
    private static ActivityStripViewModel? _sharedActivityStripViewModel;

    /// <summary>
    /// Sets the shared ActivityStripViewModel instance.
    /// Called once during app startup from MauiProgram.
    /// </summary>
    public static void SetActivityStripViewModel(ActivityStripViewModel viewModel)
    {
        _sharedActivityStripViewModel = viewModel;
    }

    /// <summary>
    /// Gets the shared ActivityStripViewModel for manual access if needed.
    /// </summary>
    public static ActivityStripViewModel? SharedActivityStrip => _sharedActivityStripViewModel;

    public BaseContentPage()
    {
        // Keep NavBar visible so TitleView shows, but we customize it
        Shell.SetNavBarIsVisible(this, true);

        // Remove back button text (cleaner look)
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            TextOverride = "",
            IsVisible = true
        });

        // Set up the global activity strip as TitleView
        SetupActivityStrip();
    }

    private void SetupActivityStrip()
    {
        if (_sharedActivityStripViewModel == null)
            return;

        var activityStrip = new ActivityStrip
        {
            BindingContext = _sharedActivityStripViewModel,
            HorizontalOptions = LayoutOptions.Fill
        };

        Shell.SetTitleView(this, activityStrip);
    }
}
