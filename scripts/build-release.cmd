@echo off
REM Build RunForge Desktop Release Package
REM Usage: build-release.cmd

echo.
echo RunForge Desktop - Release Build
echo ==================================
echo.

cd /d %~dp0..

echo Building Release configuration...
dotnet publish src/RunForgeDesktop/RunForgeDesktop.csproj ^
    --configuration Release ^
    --framework net10.0-windows10.0.19041.0 ^
    --runtime win-x64 ^
    --self-contained true ^
    -p:WindowsPackageType=MSIX ^
    -p:WindowsAppSDKSelfContained=true ^
    --output artifacts

if %ERRORLEVEL% neq 0 (
    echo.
    echo Build failed!
    exit /b %ERRORLEVEL%
)

echo.
echo Build completed successfully.
echo Output: artifacts/
echo.

pause
