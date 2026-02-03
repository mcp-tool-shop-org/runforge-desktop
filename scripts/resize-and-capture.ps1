# Resize RunForge window and capture screenshot
param(
    [string]$OutputPath = "F:/AI/runforge-desktop-phase12/docs/phase11/screenshots/screenshot.png",
    [int]$Width = 1280,
    [int]$Height = 900
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;

public class Win32 {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
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

# Restore window
[Win32]::ShowWindow($hwnd, 9)
Start-Sleep -Milliseconds 200

# Resize and move to top-left
[Win32]::MoveWindow($hwnd, 100, 100, $Width, $Height, $true)
Start-Sleep -Milliseconds 300

# Make topmost and focus
[Win32]::SetWindowPos($hwnd, [Win32]::HWND_TOPMOST, 0, 0, 0, 0, ([Win32]::SWP_NOMOVE -bor [Win32]::SWP_NOSIZE))
[Win32]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 300

# Get window rect after resize
$rect = New-Object Win32+RECT
[Win32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null

$actualWidth = $rect.Right - $rect.Left
$actualHeight = $rect.Bottom - $rect.Top

Write-Host "Window resized to: ${actualWidth}x${actualHeight}"
Write-Host "Position: ($($rect.Left), $($rect.Top))"

# Capture
$bitmap = New-Object System.Drawing.Bitmap($actualWidth, $actualHeight)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($actualWidth, $actualHeight))

$dir = [System.IO.Path]::GetDirectoryName($OutputPath)
if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

# Remove topmost
[Win32]::SetWindowPos($hwnd, [Win32]::HWND_NOTOPMOST, 0, 0, 0, 0, ([Win32]::SWP_NOMOVE -bor [Win32]::SWP_NOSIZE))

Write-Host "Screenshot saved to: $OutputPath"
