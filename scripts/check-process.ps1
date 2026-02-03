$proc = Get-Process -Name "RunForgeDesktop" -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "Found: $($proc.ProcessName) - PID: $($proc.Id) - Title: $($proc.MainWindowTitle)"
} else {
    Write-Host "RunForgeDesktop not running"
}
