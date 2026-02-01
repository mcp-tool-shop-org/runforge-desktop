# RunForge Desktop Installation Guide

## System Requirements

- Windows 10 version 1809 (build 17763) or later
- Windows 11 (any version)
- x64 processor
- 100 MB disk space (app + runtime)

## Installation Methods

### Method 1: MSIX Package (Recommended)

1. **Enable Developer Mode** (if using unsigned package):
   - Open Settings > Privacy & Security > For developers
   - Enable "Developer Mode"

2. **Install the Package**:
   - Download the `.msix` file from the releases page
   - Double-click the `.msix` file
   - Click "Install" in the App Installer dialog

3. **Launch**:
   - Find "RunForge Desktop" in the Start Menu
   - Or search for "RunForge" in Windows Search

### Method 2: Build from Source

1. **Prerequisites**:
   - .NET 10 SDK
   - Visual Studio 2022 (17.12+) with MAUI workload, OR
   - VS Code with .NET MAUI extension

2. **Clone and Build**:
   ```powershell
   git clone https://github.com/mcp-tool-shop-org/runforge-desktop
   cd runforge-desktop
   dotnet build src/RunForgeDesktop/RunForgeDesktop.csproj
   ```

3. **Run**:
   ```powershell
   dotnet run --project src/RunForgeDesktop/RunForgeDesktop.csproj
   ```

### Method 3: Portable Build

For a portable executable (no installer):

```powershell
cd runforge-desktop
dotnet publish src/RunForgeDesktop/RunForgeDesktop.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output ./publish
```

Run `./publish/RunForgeDesktop.exe` directly.

## Uninstallation

### MSIX Package

1. Open Settings > Apps > Installed apps
2. Find "RunForge Desktop"
3. Click the three dots (...) and select "Uninstall"

### Portable Build

Simply delete the application folder.

## Troubleshooting

### "Windows protected your PC" / SmartScreen Warning

This appears for unsigned packages. Options:
1. Click "More info" then "Run anyway"
2. Use a code-signed release (when available)

### Missing .NET Runtime

If you see a runtime error, install the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

### Permission Errors Reading Workspace

Ensure you have read permissions on the workspace folder. RunForge Desktop is read-only and does not require write access.

## Configuration

RunForge Desktop stores minimal settings in:
- `%LOCALAPPDATA%\RunForgeDesktop\settings.json`

This includes:
- Last workspace path
- Window size/position (future)

To reset settings, delete the settings file.
