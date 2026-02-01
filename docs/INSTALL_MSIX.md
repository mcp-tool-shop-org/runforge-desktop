# Installing RunForge Desktop (MSIX)

## System Requirements

- **Windows 10** version 1809 (October 2018 Update) or later
- **Windows 11** any version
- **Architecture:** x64
- **Disk space:** ~150 MB

## Installation Steps

### 1. Download the MSIX Package

Download the latest `.msix` file from the [Releases](https://github.com/mcp-tool-shop-org/runforge-desktop/releases) page.

### 2. Install the Package

**Option A: Double-click to install**
1. Double-click the downloaded `.msix` file
2. Windows will show the app installer dialog
3. Click **Install**
4. Wait for installation to complete
5. Click **Launch** or find "RunForge Desktop" in your Start menu

**Option B: PowerShell installation**
```powershell
Add-AppPackage -Path "RunForgeDesktop_0.1.1.0_x64.msix"
```

### 3. First Launch

1. Launch RunForge Desktop from the Start menu
2. You'll see the welcome screen (no workspace configured yet)
3. Go to **Settings** and configure your RunForge workspace path
4. Start browsing your training runs!

## Troubleshooting

### "App can't be installed" or signature errors

**Cause:** The MSIX is signed with a development certificate that isn't trusted by your system.

**Fix:** Install the certificate first:
1. Right-click the `.msix` file → **Properties** → **Digital Signatures**
2. Select the signature → **Details** → **View Certificate**
3. Click **Install Certificate**
4. Choose **Local Machine** → **Next**
5. Select **Place all certificates in the following store** → **Browse**
6. Choose **Trusted People** → **OK** → **Next** → **Finish**
7. Try installing the MSIX again

### "This app package is not supported for installation"

**Cause:** Your Windows version is too old.

**Fix:** Update to Windows 10 version 1809 or later:
1. Open **Settings** → **Update & Security** → **Windows Update**
2. Click **Check for updates**
3. Install all available updates

### App launches but shows blank screen

**Cause:** Windows App SDK runtime issue.

**Fix:** The app is self-contained and shouldn't require additional runtimes. Try:
1. Uninstall the app: **Settings** → **Apps** → find "RunForge Desktop" → **Uninstall**
2. Restart your computer
3. Reinstall the MSIX

### App crashes on startup

**Cause:** Corrupted installation or conflicting software.

**Fix:**
1. Check Windows Event Viewer for error details:
   - Open **Event Viewer** → **Windows Logs** → **Application**
   - Look for errors from "RunForge Desktop" or ".NET"
2. Try a clean install:
   ```powershell
   Get-AppPackage *runforge* | Remove-AppPackage
   Add-AppPackage -Path "RunForgeDesktop_0.1.1.0_x64.msix"
   ```

### Workspace not loading / "Invalid workspace"

**Cause:** The selected folder doesn't contain a valid RunForge `index.json`.

**Fix:**
1. Verify your workspace folder contains an `index.json` file at the root
2. Check that the `index.json` is valid JSON (open it in a text editor)
3. Make sure you're selecting the workspace root, not a subdirectory

### Runs not appearing

**Cause:** Run data is missing or malformed.

**Fix:**
1. Go to **Diagnostics** page in the app
2. Check the workspace status and run count
3. Verify that each run folder contains:
   - `run.json`
   - `request.json`
   - `result.json`

## Uninstalling

**Via Settings:**
1. Open **Settings** → **Apps**
2. Search for "RunForge Desktop"
3. Click **Uninstall**

**Via PowerShell:**
```powershell
Get-AppPackage *runforge* | Remove-AppPackage
```

## Getting Help

- **GitHub Issues:** [Report a bug](https://github.com/mcp-tool-shop-org/runforge-desktop/issues)
- **Diagnostics:** Use the in-app Diagnostics page and click **Copy All** to share system info

---

*See [WHAT_RUNFORGE_DESKTOP_IS.md](./WHAT_RUNFORGE_DESKTOP_IS.md) for product overview.*
