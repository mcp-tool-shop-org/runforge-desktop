# VM Install Runbook — Cold Machine Certification

This runbook documents the test procedure for certifying RunForge Desktop installation, upgrade, and uninstall on a clean Windows machine.

## Purpose

Verify that RunForge Desktop:
1. Installs correctly on a fresh Windows machine
2. Upgrades cleanly from a previous version
3. Uninstalls completely without leaving residue

## Test Environment Requirements

### VM Configuration
- **OS**: Windows 10 (22H2) or Windows 11 (23H2)
- **RAM**: 4 GB minimum
- **Storage**: 40 GB (20 GB for OS, 20 GB free)
- **CPU**: 2 cores
- **Type**: Hyper-V, VMware, or VirtualBox

### VM State
- Fresh Windows installation OR
- Snapshot of clean Windows state (recommended)
- No previous RunForge installation
- No .NET SDK installed (tests runtime bundling)

---

## Test Procedures

### Test 1: Fresh Install (Clean Machine)

**Prerequisites:**
- [ ] VM is at clean Windows state (no RunForge)
- [ ] Have MSIX package ready (`RunForgeDesktop_0.9.0-rc.1_x64.msix`)

**Steps:**

1. **Pre-install Verification**
   - [ ] Open PowerShell and run:
     ```powershell
     Get-AppPackage *runforge* | Select-Object Name, Version
     ```
   - [ ] Verify: No packages found
   - [ ] Screenshot: `pre-install-check.png`

2. **Install via MSIX**
   - [ ] Double-click the MSIX package
   - [ ] App Installer dialog appears
   - [ ] Click **Install**
   - [ ] Installation completes without errors
   - [ ] Screenshot: `install-dialog.png`

3. **First Launch**
   - [ ] Click **Launch** from installer OR
   - [ ] Find "RunForge Desktop" in Start menu
   - [ ] App launches successfully
   - [ ] Title bar shows version: `RunForge Desktop v0.9.0-rc.1`
   - [ ] Welcome/Dashboard screen appears
   - [ ] No crash dialogs or errors
   - [ ] Screenshot: `first-launch.png`

4. **Basic Functionality Check**
   - [ ] Navigate to Settings tab
   - [ ] Settings page loads
   - [ ] Navigate to Help tab
   - [ ] Help page loads
   - [ ] Navigate back to Dashboard
   - [ ] All tabs are responsive
   - [ ] Screenshot: `functionality-check.png`

5. **Post-install Verification**
   - [ ] Open PowerShell and run:
     ```powershell
     Get-AppPackage *runforge* | Select-Object Name, Version, InstallLocation
     ```
   - [ ] Verify: Package is installed with correct version
   - [ ] Screenshot: `post-install-check.png`

**Result:** ☐ PASS / ☐ FAIL

---

### Test 2: Upgrade from Previous Version

**Prerequisites:**
- [ ] VM has previous version installed (e.g., v0.5.0)
- [ ] Have new MSIX package ready
- [ ] Have sample workspace with test runs (optional)

**Steps:**

1. **Pre-upgrade State**
   - [ ] Open PowerShell and run:
     ```powershell
     Get-AppPackage *runforge* | Select-Object Name, Version
     ```
   - [ ] Record current version: `_____________`
   - [ ] Launch existing app
   - [ ] Note any saved settings (workspace path, etc.)
   - [ ] Screenshot: `pre-upgrade-state.png`

2. **Install Upgrade**
   - [ ] Double-click new MSIX package
   - [ ] App Installer shows **Update** (not Install)
   - [ ] Click **Update**
   - [ ] Update completes without errors
   - [ ] Screenshot: `upgrade-dialog.png`

3. **Post-upgrade Launch**
   - [ ] Launch RunForge Desktop
   - [ ] Title bar shows new version: `RunForge Desktop v0.9.0-rc.1`
   - [ ] Previous settings preserved (workspace path)
   - [ ] No crash or error dialogs
   - [ ] Screenshot: `post-upgrade-launch.png`

4. **Post-upgrade Verification**
   - [ ] Open PowerShell and run:
     ```powershell
     Get-AppPackage *runforge* | Select-Object Name, Version
     ```
   - [ ] Verify: Only ONE package with new version
   - [ ] Verify: No duplicate packages
   - [ ] Screenshot: `post-upgrade-check.png`

5. **Functionality Regression Check**
   - [ ] All tabs load correctly
   - [ ] If workspace was configured, runs still load
   - [ ] No new errors or warnings
   - [ ] Screenshot: `upgrade-functionality.png`

**Result:** ☐ PASS / ☐ FAIL

---

### Test 3: Uninstall (Clean Removal)

**Prerequisites:**
- [ ] VM has RunForge Desktop installed
- [ ] App has been used at least once

**Steps:**

1. **Pre-uninstall State**
   - [ ] Record app data location:
     ```powershell
     $appData = "$env:LOCALAPPDATA\RunForgeDesktop"
     Test-Path $appData
     Get-ChildItem $appData -ErrorAction SilentlyContinue
     ```
   - [ ] Screenshot: `pre-uninstall-appdata.png`

2. **Uninstall via Settings**
   - [ ] Open Settings → Apps → Installed apps
   - [ ] Search for "RunForge"
   - [ ] Click three dots (...) → Uninstall
   - [ ] Confirm uninstall
   - [ ] Wait for completion
   - [ ] Screenshot: `uninstall-dialog.png`

3. **Verify Package Removal**
   - [ ] Open PowerShell and run:
     ```powershell
     Get-AppPackage *runforge* | Select-Object Name, Version
     ```
   - [ ] Verify: No packages found
   - [ ] Screenshot: `post-uninstall-check.png`

4. **Verify Start Menu Removal**
   - [ ] Search for "RunForge" in Start menu
   - [ ] Verify: No results found
   - [ ] Screenshot: `start-menu-clean.png`

5. **Verify App Data Handling**
   - [ ] Check if app data folder exists:
     ```powershell
     $appData = "$env:LOCALAPPDATA\RunForgeDesktop"
     Test-Path $appData
     ```
   - [ ] Document behavior:
     - ☐ App data deleted (clean uninstall)
     - ☐ App data preserved (user data retained)
   - [ ] Screenshot: `post-uninstall-appdata.png`

**Result:** ☐ PASS / ☐ FAIL

---

## Summary Checklist

| Test | Status | Notes |
|------|--------|-------|
| Fresh Install | ☐ PASS / ☐ FAIL | |
| Upgrade | ☐ PASS / ☐ FAIL | |
| Uninstall | ☐ PASS / ☐ FAIL | |

## Certification Sign-off

| Item | Value |
|------|-------|
| Tester | |
| Date | |
| VM OS Version | |
| MSIX Version | |
| All Tests Passed | ☐ Yes / ☐ No |

---

## Troubleshooting

### Install fails with "not trusted"
Install the signing certificate first (see INSTALL_MSIX.md).

### Upgrade shows "Install" instead of "Update"
Package identity may have changed. This is a critical failure - report immediately.

### App data not cleaned on uninstall
By design, MSIX preserves user data in LocalAppData. This is expected behavior for non-destructive uninstall.

---

**Document Version:** 1.0
**Last Updated:** Phase 12 Commit 3
