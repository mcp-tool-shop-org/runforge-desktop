<#
.SYNOPSIS
    Builds the RunForge Desktop MSIX package.

.DESCRIPTION
    This script builds the RunForge Desktop application and creates an MSIX installer.
    It supports both Debug and Release configurations and creates self-contained packages.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default is Release.

.PARAMETER SkipBuild
    Skip the build step and only package.

.PARAMETER OutputPath
    Output directory for the MSIX package. Default is ./artifacts.

.EXAMPLE
    .\build-msix.ps1 -Configuration Release

.NOTES
    Requires .NET 10 SDK and Windows App SDK.
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipBuild,

    [string]$OutputPath = (Join-Path $PSScriptRoot ".." "artifacts")
)

$ErrorActionPreference = "Stop"

Write-Host "RunForge Desktop MSIX Build Script" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Paths
$ProjectDir = Join-Path $PSScriptRoot ".." "src" "RunForgeDesktop"
$ProjectFile = Join-Path $ProjectDir "RunForgeDesktop.csproj"

# Ensure output directory exists
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# Build the project
if (-not $SkipBuild) {
    Write-Host "Building RunForge Desktop ($Configuration)..." -ForegroundColor Yellow

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
        "-p:IncludeNativeLibrariesForSelfExtract=false"
        "--output", $OutputPath
    )

    & dotnet @buildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "Build completed successfully." -ForegroundColor Green
}

# Find the generated MSIX package
$msixFiles = Get-ChildItem -Path $OutputPath -Filter "*.msix" -ErrorAction SilentlyContinue

if ($msixFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "MSIX package(s) created:" -ForegroundColor Green
    foreach ($msix in $msixFiles) {
        Write-Host "  $($msix.FullName)" -ForegroundColor White
    }
} else {
    Write-Host ""
    Write-Host "Note: MSIX package location may vary. Check the publish output." -ForegroundColor Yellow
    Write-Host "For sideloading, you can install from the publish directory." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "To install for sideloading (development):" -ForegroundColor Cyan
Write-Host "  1. Enable Developer Mode in Windows Settings" -ForegroundColor White
Write-Host "  2. Right-click the .msix file and select 'Install'" -ForegroundColor White
Write-Host ""

exit 0
