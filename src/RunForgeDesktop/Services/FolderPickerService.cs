#if WINDOWS
using Windows.Storage.Pickers;
using WinRT.Interop;
using Microsoft.Maui.Platform;
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
        // Ensure we're on the UI thread for WinRT picker
        if (!MainThread.IsMainThread)
        {
            return await MainThread.InvokeOnMainThreadAsync(PickFolderInternalAsync);
        }
        return await PickFolderInternalAsync();
#else
        await Task.CompletedTask;
        return null;
#endif
    }

#if WINDOWS
    private static async Task<string?> PickFolderInternalAsync()
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        // Get the window handle for the picker
        // In MAUI, we need to get the native window handle for WinRT interop
        var mauiWindow = Application.Current?.Windows.FirstOrDefault();
        if (mauiWindow?.Handler?.PlatformView is MauiWinUIWindow winuiWindow)
        {
            var hwnd = WindowNative.GetWindowHandle(winuiWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
        }
        else
        {
            // Fallback: try to get handle from current process main window
            var hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (hwnd != IntPtr.Zero)
            {
                InitializeWithWindow.Initialize(picker, hwnd);
            }
            else
            {
                throw new InvalidOperationException("Could not obtain window handle for folder picker.");
            }
        }

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
#endif
}
