using CommunityToolkit.Maui.Storage;

namespace RunForgeDesktop.Services;

/// <summary>
/// Folder picker service using CommunityToolkit.Maui.
/// </summary>
public static class FolderPickerService
{
    /// <summary>
    /// Shows a folder picker dialog and returns the selected path.
    /// </summary>
    /// <returns>The selected folder path, or null if cancelled.</returns>
    public static async Task<string?> PickFolderAsync()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);

            if (result.IsSuccessful && result.Folder is not null)
            {
                return result.Folder.Path;
            }

            // User cancelled
            return null;
        }
        catch (Exception ex)
        {
            // Re-throw with more context
            throw new InvalidOperationException($"Folder picker failed: {ex.Message}", ex);
        }
    }
}
