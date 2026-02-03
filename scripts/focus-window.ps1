param(
    [string]$ProcessName = "RunForgeDesktop"
)

Add-Type @"
using System;
using System.Runtime.InteropServices;

public class WindowHelper {
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);
}
"@

$process = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue
if ($process) {
    $hwnd = $process.MainWindowHandle

    # If minimized, restore it
    if ([WindowHelper]::IsIconic($hwnd)) {
        [WindowHelper]::ShowWindow($hwnd, 9) # SW_RESTORE
    }

    # Bring to foreground
    [WindowHelper]::SetForegroundWindow($hwnd)

    Write-Output "Window brought to foreground: $($process.MainWindowTitle)"
} else {
    Write-Output "Process not found: $ProcessName"
}
