<#
.SYNOPSIS
    Creates a complete release bundle for RunForge Desktop.

.DESCRIPTION
    This script builds the application, generates checksums, and creates
    a release-ready bundle with all necessary artifacts for distribution.

.PARAMETER Version
    Version string (e.g., "0.9.0-rc.1"). If not specified, reads from csproj.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.

.PARAMETER OutputPath
    Output directory for the release bundle. Default is ./release.

.PARAMETER SkipBuild
    Skip the build step (use existing artifacts).

.PARAMETER CreateZip
    Create a ZIP archive of the release bundle.

.EXAMPLE
    .\create-release-bundle.ps1 -Version "0.9.0-rc.1"

.EXAMPLE
    .\create-release-bundle.ps1 -CreateZip

.NOTES
    Requires .NET 10 SDK and Windows App SDK.
    For signed MSIX, requires a valid code signing certificate.
#>

param(
    [string]$Version = "",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$OutputPath = (Join-Path $PSScriptRoot ".." "release"),

    [switch]$SkipBuild,

    [switch]$CreateZip
)

$ErrorActionPreference = "Stop"
$StartTime = Get-Date

# Banner
Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  RunForge Desktop Release Bundle Creator" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Paths
$RepoRoot = Join-Path $PSScriptRoot ".."
$ProjectDir = Join-Path $RepoRoot "src" "RunForgeDesktop"
$ProjectFile = Join-Path $ProjectDir "RunForgeDesktop.csproj"
$ArtifactsPath = Join-Path $OutputPath "artifacts"
$ChecksumsFile = Join-Path $OutputPath "SHA256SUMS.txt"
$ManifestFile = Join-Path $OutputPath "RELEASE_MANIFEST.md"

# Get version from csproj if not specified
if ([string]::IsNullOrEmpty($Version)) {
    $csprojContent = Get-Content $ProjectFile -Raw
    if ($csprojContent -match '<InformationalVersion>([^<]+)</InformationalVersion>') {
        $Version = $Matches[1]
    } else {
        $Version = "0.0.0-unknown"
    }
}

Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Output Path: $OutputPath" -ForegroundColor Yellow
Write-Host ""

# Clean and create output directories
if (Test-Path $OutputPath) {
    Write-Host "Cleaning existing release directory..." -ForegroundColor Gray
    Remove-Item -Path $OutputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
New-Item -ItemType Directory -Path $ArtifactsPath -Force | Out-Null

# Step 1: Build the project
if (-not $SkipBuild) {
    Write-Host "[1/5] Building RunForge Desktop..." -ForegroundColor Cyan

    $buildArgs = @(
        "publish"
        $ProjectFile
        "--configuration", $Configuration
        "--framework", "net10.0-windows10.0.19041.0"
        "--runtime", "win-x64"
        "--self-contained", "true"
        "-p:WindowsPackageType=MSIX"
        "-p:WindowsAppSDKSelfContained=true"
        "-p:PublishSingleFile=false"
        "--output", $ArtifactsPath
    )

    & dotnet @buildArgs 2>&1 | Out-Host

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "Build completed." -ForegroundColor Green
} else {
    Write-Host "[1/5] Skipping build (using existing artifacts)..." -ForegroundColor Yellow
}

# Step 2: Locate MSIX package
Write-Host "[2/5] Locating release artifacts..." -ForegroundColor Cyan

$msixFiles = Get-ChildItem -Path $ArtifactsPath -Filter "*.msix" -Recurse -ErrorAction SilentlyContinue
$msixbundleFiles = Get-ChildItem -Path $ArtifactsPath -Filter "*.msixbundle" -Recurse -ErrorAction SilentlyContinue

$releaseArtifacts = @()

if ($msixbundleFiles.Count -gt 0) {
    $releaseArtifacts += $msixbundleFiles
    Write-Host "  Found MSIX bundle: $($msixbundleFiles[0].Name)" -ForegroundColor White
}

if ($msixFiles.Count -gt 0) {
    $releaseArtifacts += $msixFiles
    foreach ($msix in $msixFiles) {
        Write-Host "  Found MSIX: $($msix.Name)" -ForegroundColor White
    }
}

# Also look for the certificate if present
$certFiles = Get-ChildItem -Path $ArtifactsPath -Filter "*.cer" -Recurse -ErrorAction SilentlyContinue
if ($certFiles.Count -gt 0) {
    $releaseArtifacts += $certFiles
    Write-Host "  Found certificate: $($certFiles[0].Name)" -ForegroundColor White
}

# Copy key documentation files
Write-Host "[3/5] Copying documentation..." -ForegroundColor Cyan

$docsToInclude = @(
    "README.md",
    "LICENSE",
    "CHANGELOG.md",
    "SECURITY.md"
)

foreach ($doc in $docsToInclude) {
    $docPath = Join-Path $RepoRoot $doc
    if (Test-Path $docPath) {
        Copy-Item -Path $docPath -Destination $OutputPath -Force
        Write-Host "  Copied: $doc" -ForegroundColor White
    }
}

# Copy install instructions
$installInstructions = @"
# Installing RunForge Desktop

## Prerequisites
- Windows 10 (1809) or later, or Windows 11
- Developer Mode enabled (for sideloading)

## Installation Steps

### Option 1: Direct Install (Recommended)
1. Download ``RunForgeDesktop_${Version}_x64.msix``
2. Double-click the file
3. Click **Install** in the App Installer dialog
4. Launch from Start menu: "RunForge Desktop"

### Option 2: PowerShell Install
``````powershell
Add-AppPackage -Path ".\artifacts\RunForgeDesktop_${Version}_x64.msix"
``````

## Verify Installation
``````powershell
Get-AppPackage *runforge* | Select-Object Name, Version
``````

## Uninstall
``````powershell
Get-AppPackage *runforge* | Remove-AppPackage
``````

Or: Settings > Apps > Installed apps > RunForge Desktop > Uninstall

## Troubleshooting

### "App package not trusted" error
The package is self-signed for development. Either:
1. Install the signing certificate (if provided), or
2. Enable Developer Mode: Settings > Privacy & Security > For developers > Developer Mode

### App won't start
1. Check Windows Event Viewer for errors
2. Run the Diagnostics tab in the app
3. Report issues: https://github.com/mcp-tool-shop-org/runforge-desktop/issues

---
Version: $Version
Build Configuration: $Configuration
"@

$installInstructions | Out-File -FilePath (Join-Path $OutputPath "INSTALL.md") -Encoding utf8
Write-Host "  Created: INSTALL.md" -ForegroundColor White

# Step 4: Generate SHA256 checksums
Write-Host "[4/5] Generating SHA256 checksums..." -ForegroundColor Cyan

$checksumContent = @"
# SHA256 Checksums for RunForge Desktop $Version
# Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC" -AsUTC)
#
# To verify on Windows PowerShell:
#   Get-FileHash -Algorithm SHA256 <filename>
#
# To verify on Linux/macOS:
#   sha256sum -c SHA256SUMS.txt
#

"@

$allFiles = Get-ChildItem -Path $OutputPath -File -Recurse | Where-Object {
    $_.Extension -in @(".msix", ".msixbundle", ".cer", ".exe", ".dll") -or
    $_.Name -eq "INSTALL.md"
}

foreach ($file in $allFiles) {
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLower()
    $relativePath = $file.FullName.Replace($OutputPath, "").TrimStart("\", "/")
    $checksumContent += "$hash  $relativePath`n"
    Write-Host "  $($file.Name): $($hash.Substring(0,16))..." -ForegroundColor Gray
}

$checksumContent | Out-File -FilePath $ChecksumsFile -Encoding utf8 -NoNewline
Write-Host "  Created: SHA256SUMS.txt" -ForegroundColor White

# Step 5: Create release manifest
Write-Host "[5/5] Creating release manifest..." -ForegroundColor Cyan

$artifactList = ""
foreach ($file in $allFiles) {
    $size = "{0:N2} MB" -f ($file.Length / 1MB)
    $relativePath = $file.FullName.Replace($OutputPath, "").TrimStart("\", "/")
    $artifactList += "| ``$relativePath`` | $size |`n"
}

$manifest = @"
# Release Manifest: RunForge Desktop $Version

## Build Information
| Property | Value |
|----------|-------|
| Version | $Version |
| Configuration | $Configuration |
| Build Date | $(Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC" -AsUTC) |
| .NET Version | net10.0-windows10.0.19041.0 |
| Runtime | win-x64 (self-contained) |
| Package Type | MSIX |

## Release Artifacts
| File | Size |
|------|------|
$artifactList
## Checksums
See ``SHA256SUMS.txt`` for cryptographic verification.

## Installation
See ``INSTALL.md`` for installation instructions.

## Changelog
See ``CHANGELOG.md`` for version history.

## System Requirements
- **OS**: Windows 10 version 1809 (build 17763) or later
- **Architecture**: x64
- **RAM**: 4 GB minimum, 8 GB recommended
- **Storage**: 500 MB for installation
- **GPU**: Optional (for ML training acceleration)

## Known Issues
See GitHub Issues: https://github.com/mcp-tool-shop-org/runforge-desktop/issues

## Support
- Documentation: https://github.com/mcp-tool-shop-org/runforge-desktop
- Issues: https://github.com/mcp-tool-shop-org/runforge-desktop/issues
- Security: See SECURITY.md

---
Generated by create-release-bundle.ps1
"@

$manifest | Out-File -FilePath $ManifestFile -Encoding utf8
Write-Host "  Created: RELEASE_MANIFEST.md" -ForegroundColor White

# Optional: Create ZIP archive
if ($CreateZip) {
    Write-Host ""
    Write-Host "Creating ZIP archive..." -ForegroundColor Cyan
    $zipPath = Join-Path (Split-Path $OutputPath -Parent) "RunForgeDesktop_${Version}_release.zip"

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path "$OutputPath\*" -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "  Created: $zipPath" -ForegroundColor White
}

# Summary
$EndTime = Get-Date
$Duration = $EndTime - $StartTime

Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host "  Release Bundle Complete!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Version:  $Version" -ForegroundColor White
Write-Host "Location: $OutputPath" -ForegroundColor White
Write-Host "Duration: $($Duration.TotalSeconds.ToString("F1")) seconds" -ForegroundColor White
Write-Host ""
Write-Host "Contents:" -ForegroundColor Yellow
Get-ChildItem -Path $OutputPath | ForEach-Object {
    $icon = if ($_.PSIsContainer) { "[DIR]" } else { "[FILE]" }
    Write-Host "  $icon $($_.Name)" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Test installation with INSTALL.md instructions" -ForegroundColor White
Write-Host "  2. Verify checksums match" -ForegroundColor White
Write-Host "  3. Create GitHub Release with these artifacts" -ForegroundColor White
Write-Host ""

exit 0
