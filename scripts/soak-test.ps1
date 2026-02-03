<#
.SYNOPSIS
    2-Hour Soak Test Harness for RunForge Desktop

.DESCRIPTION
    Monitors RunForge Desktop for stability over an extended period.
    Tracks memory usage, handle count, and detects crashes.
    Generates a report at the end of the test.

.PARAMETER DurationMinutes
    Test duration in minutes. Default is 120 (2 hours).

.PARAMETER SampleIntervalSeconds
    How often to sample metrics in seconds. Default is 30.

.PARAMETER ProcessName
    Name of the process to monitor. Default is "RunForgeDesktop".

.PARAMETER OutputPath
    Directory for test results. Default is ./soak-test-results.

.PARAMETER MemoryThresholdMB
    Maximum acceptable memory in MB before flagging growth. Default is 500.

.PARAMETER HandleThreshold
    Maximum acceptable handles before flagging leak. Default is 5000.

.EXAMPLE
    .\soak-test.ps1 -DurationMinutes 120

.EXAMPLE
    .\soak-test.ps1 -DurationMinutes 30 -SampleIntervalSeconds 10

.NOTES
    RunForge Desktop must be running before starting this test.
    The script will NOT start the application automatically.
#>

param(
    [int]$DurationMinutes = 120,
    [int]$SampleIntervalSeconds = 30,
    [string]$ProcessName = "RunForgeDesktop",
    [string]$OutputPath = (Join-Path $PSScriptRoot ".." "soak-test-results"),
    [int]$MemoryThresholdMB = 500,
    [int]$HandleThreshold = 5000
)

$ErrorActionPreference = "Stop"

# ===== Configuration =====
$TestId = Get-Date -Format "yyyyMMdd-HHmmss"
$ResultDir = Join-Path $OutputPath $TestId
$CsvFile = Join-Path $ResultDir "metrics.csv"
$ReportFile = Join-Path $ResultDir "report.md"
$LogFile = Join-Path $ResultDir "soak-test.log"

# ===== Helper Functions =====
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logLine = "[$timestamp] [$Level] $Message"
    Write-Host $logLine -ForegroundColor $(switch ($Level) {
        "ERROR" { "Red" }
        "WARN"  { "Yellow" }
        "INFO"  { "Cyan" }
        default { "White" }
    })
    Add-Content -Path $LogFile -Value $logLine
}

function Get-ProcessMetrics {
    param([string]$Name)

    $process = Get-Process -Name $Name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $process) {
        return $null
    }

    return @{
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        MemoryMB = [math]::Round($process.WorkingSet64 / 1MB, 2)
        PrivateMemoryMB = [math]::Round($process.PrivateMemorySize64 / 1MB, 2)
        HandleCount = $process.HandleCount
        ThreadCount = $process.Threads.Count
        CpuTime = $process.TotalProcessorTime.TotalSeconds
        Responding = $process.Responding
        PID = $process.Id
    }
}

function Write-MetricsToCsv {
    param($Metrics)

    $csvLine = "$($Metrics.Timestamp),$($Metrics.MemoryMB),$($Metrics.PrivateMemoryMB),$($Metrics.HandleCount),$($Metrics.ThreadCount),$($Metrics.CpuTime),$($Metrics.Responding)"
    Add-Content -Path $CsvFile -Value $csvLine
}

function Analyze-Results {
    param([array]$AllMetrics)

    $memoryValues = $AllMetrics | ForEach-Object { $_.MemoryMB }
    $handleValues = $AllMetrics | ForEach-Object { $_.HandleCount }

    $analysis = @{
        SampleCount = $AllMetrics.Count
        StartMemoryMB = $memoryValues[0]
        EndMemoryMB = $memoryValues[-1]
        MinMemoryMB = ($memoryValues | Measure-Object -Minimum).Minimum
        MaxMemoryMB = ($memoryValues | Measure-Object -Maximum).Maximum
        AvgMemoryMB = [math]::Round(($memoryValues | Measure-Object -Average).Average, 2)
        MemoryGrowthMB = [math]::Round($memoryValues[-1] - $memoryValues[0], 2)
        StartHandles = $handleValues[0]
        EndHandles = $handleValues[-1]
        MinHandles = ($handleValues | Measure-Object -Minimum).Minimum
        MaxHandles = ($handleValues | Measure-Object -Maximum).Maximum
        HandleGrowth = $handleValues[-1] - $handleValues[0]
        UnresponsiveCount = ($AllMetrics | Where-Object { $_.Responding -eq $false }).Count
    }

    # Determine pass/fail
    $analysis.MemoryOK = $analysis.MaxMemoryMB -lt $MemoryThresholdMB
    $analysis.HandlesOK = $analysis.MaxHandles -lt $HandleThreshold
    $analysis.ResponsiveOK = $analysis.UnresponsiveCount -eq 0
    $analysis.OverallPass = $analysis.MemoryOK -and $analysis.HandlesOK -and $analysis.ResponsiveOK

    return $analysis
}

function Generate-Report {
    param($Analysis, $DurationMinutes, $CrashDetected)

    $status = if ($Analysis.OverallPass -and -not $CrashDetected) { "✅ PASSED" } else { "❌ FAILED" }

    $report = @"
# Soak Test Report

**Test ID:** $TestId
**Duration:** $DurationMinutes minutes
**Status:** $status

---

## Summary

| Metric | Value | Threshold | Status |
|--------|-------|-----------|--------|
| Max Memory (MB) | $($Analysis.MaxMemoryMB) | < $MemoryThresholdMB | $(if ($Analysis.MemoryOK) { "✅" } else { "❌" }) |
| Max Handles | $($Analysis.MaxHandles) | < $HandleThreshold | $(if ($Analysis.HandlesOK) { "✅" } else { "❌" }) |
| Unresponsive Events | $($Analysis.UnresponsiveCount) | 0 | $(if ($Analysis.ResponsiveOK) { "✅" } else { "❌" }) |
| Crash Detected | $(if ($CrashDetected) { "Yes" } else { "No" }) | No | $(if (-not $CrashDetected) { "✅" } else { "❌" }) |

---

## Memory Analysis

| Metric | Value |
|--------|-------|
| Start | $($Analysis.StartMemoryMB) MB |
| End | $($Analysis.EndMemoryMB) MB |
| Min | $($Analysis.MinMemoryMB) MB |
| Max | $($Analysis.MaxMemoryMB) MB |
| Average | $($Analysis.AvgMemoryMB) MB |
| Growth | $($Analysis.MemoryGrowthMB) MB |

**Memory Verdict:** $(if ($Analysis.MemoryGrowthMB -gt 100) { "⚠️ Significant growth detected" } elseif ($Analysis.MemoryGrowthMB -gt 50) { "⚠️ Moderate growth" } else { "✅ Stable" })

---

## Handle Analysis

| Metric | Value |
|--------|-------|
| Start | $($Analysis.StartHandles) |
| End | $($Analysis.EndHandles) |
| Min | $($Analysis.MinHandles) |
| Max | $($Analysis.MaxHandles) |
| Growth | $($Analysis.HandleGrowth) |

**Handle Verdict:** $(if ($Analysis.HandleGrowth -gt 500) { "⚠️ Possible handle leak" } elseif ($Analysis.HandleGrowth -gt 200) { "⚠️ Handle growth detected" } else { "✅ Stable" })

---

## Test Configuration

| Parameter | Value |
|-----------|-------|
| Duration | $DurationMinutes minutes |
| Sample Interval | $SampleIntervalSeconds seconds |
| Total Samples | $($Analysis.SampleCount) |
| Memory Threshold | $MemoryThresholdMB MB |
| Handle Threshold | $HandleThreshold |

---

## Files

- Metrics CSV: ``metrics.csv``
- Log File: ``soak-test.log``

---

**Generated:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
"@

    return $report
}

# ===== Main Script =====
Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host " RunForge Desktop Soak Test Harness" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Create output directory
if (-not (Test-Path $ResultDir)) {
    New-Item -ItemType Directory -Path $ResultDir -Force | Out-Null
}

Write-Log "Soak test starting"
Write-Log "Duration: $DurationMinutes minutes"
Write-Log "Sample interval: $SampleIntervalSeconds seconds"
Write-Log "Output directory: $ResultDir"

# Initialize CSV
Set-Content -Path $CsvFile -Value "Timestamp,MemoryMB,PrivateMemoryMB,HandleCount,ThreadCount,CpuTime,Responding"

# Check if process is running
$initialMetrics = Get-ProcessMetrics -Name $ProcessName
if ($null -eq $initialMetrics) {
    Write-Log "ERROR: Process '$ProcessName' not found. Please start RunForge Desktop first." "ERROR"
    Write-Host ""
    Write-Host "To start the test:" -ForegroundColor Yellow
    Write-Host "  1. Launch RunForge Desktop" -ForegroundColor White
    Write-Host "  2. Re-run this script" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Log "Process found (PID: $($initialMetrics.PID))"
Write-Log "Initial memory: $($initialMetrics.MemoryMB) MB"
Write-Log "Initial handles: $($initialMetrics.HandleCount)"

# Calculate iterations
$totalIterations = [math]::Ceiling(($DurationMinutes * 60) / $SampleIntervalSeconds)
Write-Log "Total samples to collect: $totalIterations"

# Collect metrics
$allMetrics = @()
$crashDetected = $false
$startTime = Get-Date

Write-Host ""
Write-Host "Collecting metrics... (Press Ctrl+C to stop early)" -ForegroundColor Yellow
Write-Host ""

for ($i = 1; $i -le $totalIterations; $i++) {
    $metrics = Get-ProcessMetrics -Name $ProcessName

    if ($null -eq $metrics) {
        Write-Log "Process not found - possible crash!" "ERROR"
        $crashDetected = $true
        break
    }

    $allMetrics += $metrics
    Write-MetricsToCsv -Metrics $metrics

    # Progress
    $elapsed = (Get-Date) - $startTime
    $progress = [math]::Round(($i / $totalIterations) * 100, 1)
    Write-Host "`r[$progress%] Sample $i/$totalIterations | Memory: $($metrics.MemoryMB) MB | Handles: $($metrics.HandleCount) | Elapsed: $($elapsed.ToString('hh\:mm\:ss'))    " -NoNewline

    # Log warnings
    if ($metrics.MemoryMB -gt $MemoryThresholdMB * 0.8) {
        Write-Log "Memory approaching threshold: $($metrics.MemoryMB) MB" "WARN"
    }
    if ($metrics.HandleCount -gt $HandleThreshold * 0.8) {
        Write-Log "Handle count approaching threshold: $($metrics.HandleCount)" "WARN"
    }
    if (-not $metrics.Responding) {
        Write-Log "Application not responding!" "WARN"
    }

    # Sleep
    if ($i -lt $totalIterations) {
        Start-Sleep -Seconds $SampleIntervalSeconds
    }
}

Write-Host ""
Write-Host ""

# Analyze results
Write-Log "Analyzing results..."
$analysis = Analyze-Results -AllMetrics $allMetrics

# Generate report
Write-Log "Generating report..."
$report = Generate-Report -Analysis $analysis -DurationMinutes $DurationMinutes -CrashDetected $crashDetected
Set-Content -Path $ReportFile -Value $report

# Summary
Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host " Test Complete" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

if ($analysis.OverallPass -and -not $crashDetected) {
    Write-Host "Result: PASSED" -ForegroundColor Green
} else {
    Write-Host "Result: FAILED" -ForegroundColor Red
}

Write-Host ""
Write-Host "Memory: $($analysis.StartMemoryMB) MB -> $($analysis.EndMemoryMB) MB (growth: $($analysis.MemoryGrowthMB) MB)"
Write-Host "Handles: $($analysis.StartHandles) -> $($analysis.EndHandles) (growth: $($analysis.HandleGrowth))"
Write-Host ""
Write-Host "Report: $ReportFile" -ForegroundColor Cyan
Write-Host "Metrics: $CsvFile" -ForegroundColor Cyan
Write-Host ""

Write-Log "Soak test completed"

exit $(if ($analysis.OverallPass -and -not $crashDetected) { 0 } else { 1 })
