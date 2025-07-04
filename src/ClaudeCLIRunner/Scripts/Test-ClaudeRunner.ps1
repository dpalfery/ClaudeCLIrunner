# Test script for Claude CLI Runner

param(
    [string]$ConfigPath = "appsettings.json",
    [int]$Duration = 300,  # Run for 5 minutes by default
    [switch]$Verbose
)

Write-Host "Claude CLI Runner Test Script" -ForegroundColor Green
Write-Host "=============================" -ForegroundColor Green

# Check if configuration exists
if (-not (Test-Path $ConfigPath)) {
    Write-Error "Configuration file not found: $ConfigPath"
    exit 1
}

# Set environment variables for testing
$env:CLAUDECLI_ClaudeCliConfig__ClaudeCodeCliPath = "echo"  # Use echo as mock CLI for testing
$env:CLAUDECLI_ClaudeCliConfig__PollIntervalSeconds = "10"  # Poll every 10 seconds for testing

if ($Verbose) {
    $env:Logging__LogLevel__ClaudeCLIRunner = "Debug"
}

Write-Host "`nStarting Claude CLI Runner..." -ForegroundColor Yellow
Write-Host "Configuration: $ConfigPath"
Write-Host "Test Duration: $Duration seconds"
Write-Host "Press Ctrl+C to stop`n" -ForegroundColor Cyan

# Run the application
$process = Start-Process -FilePath "dotnet" -ArgumentList "run" -PassThru -NoNewWindow

# Wait for specified duration
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
while ($stopwatch.Elapsed.TotalSeconds -lt $Duration) {
    if ($process.HasExited) {
        Write-Host "`nProcess exited unexpectedly with code: $($process.ExitCode)" -ForegroundColor Red
        break
    }
    
    $remaining = $Duration - [int]$stopwatch.Elapsed.TotalSeconds
    Write-Progress -Activity "Running Claude CLI Runner" -Status "Time remaining: $remaining seconds" -PercentComplete (($stopwatch.Elapsed.TotalSeconds / $Duration) * 100)
    
    Start-Sleep -Seconds 1
}

# Stop the process
if (-not $process.HasExited) {
    Write-Host "`nStopping Claude CLI Runner..." -ForegroundColor Yellow
    $process.Kill()
    $process.WaitForExit(5000)
}

Write-Host "`nTest completed!" -ForegroundColor Green 