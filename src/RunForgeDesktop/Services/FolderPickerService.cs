#if WINDOWS
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif

namespace RunForgeDesktop.Services;

/// <summary>
/// Windows-native folder picker service.
/// </summary>
public static class FolderPickerService
{
    /// <summary>
    /// Shows a folder picker dialog and returns the selected path.
    /// </summary>
    /// <returns>The selected folder path, or null if cancelled.</returns>
    public static async Task<string?> PickFolderAsync()
    {
#if WINDOWS
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        // Get the window handle for the picker
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView;
        if (window is Microsoft.UI.Xaml.Window win)
        {
            var hwnd = WindowNative.GetWindowHandle(win);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
#else
        await Task.CompletedTask;
        return null;
#endif
    }
}
