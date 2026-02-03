# Capture RunForge window screenshot by clicking on it first
param(
    [string]$OutputPath = "F:/AI/runforge-desktop-phase12/docs/phase11/screenshots/screenshot.png"
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;

public class Win32 {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError=true)]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
}
"@

$process = Get-Process -Name "RunForgeDesktop" -ErrorAction SilentlyContinue
if (!$process) {
    Write-Error "RunForge Desktop not running"
    exit 1
}

$hwnd = $process.MainWindowHandle

# Restore if minimized
if ([Win32]::IsIconic($hwnd)) {
    [Win32]::ShowWindow($hwnd, 9)
    Start-Sleep -Milliseconds 300
}

# Make it topmost temporarily
[Win32]::SetWindowPos($hwnd, [Win32]::HWND_TOPMOST, 0, 0, 0, 0, ([Win32]::SWP_NOMOVE -bor [Win32]::SWP_NOSIZE))
[Win32]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 500

# Get window rect
$rect = New-Object Win32+RECT
[Win32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null

$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top

Write-Host "Window: $($process.MainWindowTitle)"
Write-Host "Position: ($($rect.Left), $($rect.Top))"
Write-Host "Size: ${width}x${height}"

# Validate
if ($width -lt 100 -or $height -lt 100) {
    Write-Error "Window too small or invalid"
    exit 1
}

# Capture
$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($width, $height))

# Save
$dir = [System.IO.Path]::GetDirectoryName($OutputPath)
if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

# Remove topmost
[Win32]::SetWindowPos($hwnd, [Win32]::HWND_NOTOPMOST, 0, 0, 0, 0, ([Win32]::SWP_NOMOVE -bor [Win32]::SWP_NOSIZE))

Write-Host "Screenshot saved to: $OutputPath"
