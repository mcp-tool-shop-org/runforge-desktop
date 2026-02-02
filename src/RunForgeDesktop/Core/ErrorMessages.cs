namespace RunForgeDesktop.Core;

/// <summary>
/// Standardized error messages following the RunForge error messaging checklist.
/// Each message explains what happened, why (if known), and what to do next.
/// Voice: helpful guide, no blame, no drama, no panic.
/// </summary>
public static class ErrorMessages
{
    // ═══════════════════════════════════════════════════════════════════
    // WORKSPACE & PATHS
    // ═══════════════════════════════════════════════════════════════════

    public static class Workspace
    {
        public const string NotSelected = "No workspace selected.\n\nGo to Dashboard and select a workspace folder to get started.";

        public const string InvalidFolder = "This folder doesn't contain RunForge data.\n\nPlease select a folder that contains training outputs, or start a new run to create one.";

        public static string FolderMissing(string path) =>
            $"The workspace folder no longer exists:\n{path}\n\nSelect a different folder or create a new one.";

        public static string AccessDenied(string path) =>
            $"Can't access this folder (permission denied):\n{path}\n\nTry selecting a folder in your Documents or another location you have access to.";
    }

    // ═══════════════════════════════════════════════════════════════════
    // PYTHON & DEPENDENCIES
    // ═══════════════════════════════════════════════════════════════════

    public static class Python
    {
        public const string NotFound = "Python wasn't found on this system.\n\nInstall Python 3.8+ and make sure it's in your PATH, or set a custom path in Settings.";

        public const string WrongVersion = "Python was found but the version is too old.\n\nRunForge requires Python 3.8 or newer. Update Python or set a different path in Settings.";

        public const string PackageMissing = "A required Python package is missing.\n\nOpen a terminal and run:\npip install torch numpy\n\nThen try again.";

        public static string CustomPathInvalid(string path) =>
            $"The Python path in Settings doesn't work:\n{path}\n\nBrowse for a valid Python executable or clear the override to use auto-discovery.";

        public const string DiscoveryFailed = "Python detection failed unexpectedly.\n\nTry setting a custom Python path in Settings, or restart the app.";
    }

    // ═══════════════════════════════════════════════════════════════════
    // GPU & DEVICE
    // ═══════════════════════════════════════════════════════════════════

    public static class Gpu
    {
        public const string NotAvailable = "GPU not detected.\n\nTraining will use CPU instead (slower but works fine). To enable GPU, install NVIDIA drivers and CUDA toolkit.";

        public const string CudaNotInstalled = "NVIDIA GPU detected but CUDA toolkit isn't installed.\n\nInstall CUDA from nvidia.com/cuda-downloads, or use CPU for training.";

        public const string OutOfMemory = "GPU ran out of memory during training.\n\nTry reducing the batch size (current value may be too high for your GPU), or switch to CPU.";

        public const string DriverTooOld = "GPU driver is too old for this CUDA version.\n\nUpdate your NVIDIA drivers from nvidia.com/drivers.";
    }

    // ═══════════════════════════════════════════════════════════════════
    // RUNNER LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════

    public static class Runner
    {
        public const string AlreadyRunning = "A training run is already in progress.\n\nWait for it to complete, or cancel it from the live view.";

        public const string FailedToStart = "Training couldn't start.\n\nCheck that Python is configured correctly in Settings. Open Logs for details.";

        public const string Crashed = "Training stopped unexpectedly.\n\nThis sometimes happens with memory issues or invalid parameters. Check the logs for the specific error.";

        public const string Timeout = "Training timed out (no response).\n\nThe process may be stuck. Try cancelling and starting a new run with different parameters.";

        public const string CancelledByUser = "Training was cancelled.\n\nYou can start a new run whenever you're ready.";

        public static string FailedWithCode(int exitCode) =>
            $"Training ended with an error (exit code {exitCode}).\n\nOpen the logs to see what went wrong.";

        public static string FailedWithMessage(string message) =>
            $"Training encountered an error:\n{message}\n\nCheck Settings to verify Python configuration, or open Logs for more details.";
    }

    // ═══════════════════════════════════════════════════════════════════
    // DATA & I/O
    // ═══════════════════════════════════════════════════════════════════

    public static class Data
    {
        public static string DatasetNotFound(string path) =>
            $"Can't find the dataset file:\n{path}\n\nBrowse for a valid dataset or leave the field empty to use built-in simulation data.";

        public const string DatasetCorrupt = "The dataset file couldn't be read.\n\nMake sure it's a valid CSV or supported format. Try a different file.";

        public const string MetricsCorrupt = "Metrics data is incomplete or corrupted.\n\nThe run may have ended unexpectedly. Start a new run to generate fresh metrics.";

        public const string LogsNotFound = "Log file not found for this run.\n\nThe run may not have started, or logs were deleted.";

        public static string WriteError(string path) =>
            $"Can't write to this location:\n{path}\n\nCheck that the folder exists and you have permission to write there.";

        public const string IndexCorrupt = "The run index is corrupted.\n\nReloading the workspace should fix this automatically.";
    }

    // ═══════════════════════════════════════════════════════════════════
    // FILE OPERATIONS
    // ═══════════════════════════════════════════════════════════════════

    public static class File
    {
        public static string OpenFailed(string item) =>
            $"Couldn't open {item}.\n\nMake sure the file exists and you have a default program set to open it.";

        public const string CopyFailed = "Couldn't copy to clipboard.\n\nTry again, or manually select and copy the text.";

        public static string DeleteFailed(string item) =>
            $"Couldn't delete {item}.\n\nMake sure no other program is using it, then try again.";

        public const string BrowseCancelled = ""; // Not an error, just return silently
    }

    // ═══════════════════════════════════════════════════════════════════
    // SETTINGS
    // ═══════════════════════════════════════════════════════════════════

    public static class Settings
    {
        public const string SaveFailed = "Settings couldn't be saved.\n\nTry again. If the problem continues, check that the app has write access to its data folder.";

        public const string LoadFailed = "Settings file couldn't be read.\n\nUsing default values. Your preferences will be saved when you make changes.";

        public const string ResetComplete = "Settings restored to defaults.\n\nYou can reconfigure anytime.";
    }

    // ═══════════════════════════════════════════════════════════════════
    // VALIDATION
    // ═══════════════════════════════════════════════════════════════════

    public static class Validation
    {
        public const string RunNameRequired = "Give your run a name.\n\nThis helps you identify it later (e.g., \"test-1\", \"lr-sweep-high\").";

        public const string InvalidEpochs = "Epochs must be at least 1.\n\nMore epochs = longer training but potentially better results.";

        public const string InvalidBatchSize = "Batch size must be at least 1.\n\nCommon values: 16, 32, 64, 128. Lower values use less memory.";

        public const string InvalidLearningRate = "Learning rate must be greater than 0.\n\nCommon values: 0.001, 0.0001. Smaller = slower but more stable training.";
    }

    // ═══════════════════════════════════════════════════════════════════
    // MULTI-RUN / SWEEP
    // ═══════════════════════════════════════════════════════════════════

    public static class Sweep
    {
        public const string NoRunsConfigured = "No runs configured for this sweep.\n\nEnter at least one learning rate, batch size, and optimizer.";

        public const string PartialFailure = "Some runs in the sweep failed.\n\nCheck the results table - successful runs are still valid. You can re-run the failed ones individually.";

        public const string CancelledMidway = "Sweep was cancelled.\n\nCompleted runs are saved. You can view them in the Runs tab.";

        public static string BestRunFound(int runNumber, double loss) =>
            $"Sweep complete! Best result: Run {runNumber} with loss {loss:F4}.\n\nCheck the table for all results.";

        public const string NoSuccessfulRuns = "Sweep finished but no runs completed successfully.\n\nTry adjusting the hyperparameters or check Python/GPU configuration in Settings.";
    }

    // ═══════════════════════════════════════════════════════════════════
    // STORAGE & CLEANUP
    // ═══════════════════════════════════════════════════════════════════

    public static class Storage
    {
        public const string CalculationFailed = "Couldn't calculate storage usage.\n\nTry refreshing, or check that the workspace folder is accessible.";

        public static string DeleteRunSuccess(string runName) =>
            $"Deleted: {runName}\n\nStorage has been freed up.";

        public static string DeleteRunFailed(string runName) =>
            $"Couldn't delete {runName}.\n\nMake sure no other program is using those files.";
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER: Format exception message without technical jargon
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Converts a raw exception into a user-friendly message.
    /// Falls back to the generic message if we can't extract anything useful.
    /// </summary>
    public static string FromException(Exception ex, string context = "operation")
    {
        // Extract the most relevant message
        var message = ex.InnerException?.Message ?? ex.Message;

        // Check for common patterns and provide friendly messages
        if (message.Contains("Access") && message.Contains("denied"))
            return $"Permission denied during {context}.\n\nTry running as administrator or selecting a different location.";

        if (message.Contains("not found") || message.Contains("does not exist"))
            return $"A required file or folder wasn't found.\n\nCheck that paths are correct and try again.";

        if (message.Contains("disk") || message.Contains("space"))
            return $"Not enough disk space for {context}.\n\nFree up some space and try again.";

        if (message.Contains("timeout") || message.Contains("timed out"))
            return $"The {context} took too long and was stopped.\n\nTry again - if it keeps happening, there may be a larger issue.";

        // Default: include the original message but frame it helpfully
        return $"Something went wrong during {context}.\n\nDetails: {message}";
    }
}
