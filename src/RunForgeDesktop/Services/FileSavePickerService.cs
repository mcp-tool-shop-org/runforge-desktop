using Windows.Storage.Pickers;
using WinRT.Interop;

namespace RunForgeDesktop.Services;

/// <summary>
/// Windows-native file save picker service.
/// </summary>
public static class FileSavePickerService
{
    /// <summary>
    /// Opens a native file save dialog.
    /// </summary>
    /// <param name="suggestedFileName">Suggested file name.</param>
    /// <param name="fileTypeChoices">File type choices (e.g., ".csv" => "CSV Files").</param>
    /// <returns>Selected file path or null if cancelled.</returns>
    public static async Task<string?> SaveFileAsync(
        string suggestedFileName,
        Dictionary<string, List<string>> fileTypeChoices)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName
        };

        foreach (var choice in fileTypeChoices)
        {
            picker.FileTypeChoices.Add(choice.Key, choice.Value);
        }

        // Get the window handle for the picker
        var windowHandle = GetWindowHandle();
        InitializeWithWindow.Initialize(picker, windowHandle);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    /// <summary>
    /// Opens a native CSV file save dialog.
    /// </summary>
    public static Task<string?> SaveCsvFileAsync(string suggestedFileName)
    {
        return SaveFileAsync(suggestedFileName, new Dictionary<string, List<string>>
        {
            { "CSV Files", new List<string> { ".csv" } }
        });
    }

    /// <summary>
    /// Opens a native JSON file save dialog.
    /// </summary>
    public static Task<string?> SaveJsonFileAsync(string suggestedFileName)
    {
        return SaveFileAsync(suggestedFileName, new Dictionary<string, List<string>>
        {
            { "JSON Files", new List<string> { ".json" } }
        });
    }

    private static IntPtr GetWindowHandle()
    {
        // Get the current application window handle
        var window = Application.Current?.Windows?.FirstOrDefault();
        if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window mauiWindow)
        {
            return WindowNative.GetWindowHandle(mauiWindow);
        }

        // Fallback: try to get the active window
        return System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
    }
}
