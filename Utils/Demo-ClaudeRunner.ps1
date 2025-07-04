# Demo script for Claude CLI Runner
# This script demonstrates the application in action

Write-Host @"
===============================================
       Claude CLI Runner Demo
===============================================

This demo shows how the Claude CLI Runner:
1. Polls for Azure DevOps work items with @Claude tag
2. Executes Claude CLI for each work item
3. Handles timeouts and retries
4. Reports results back to work items

"@ -ForegroundColor Cyan

# Set demo environment variables
Write-Host "Setting up demo environment..." -ForegroundColor Yellow
$env:CLAUDECLI_ClaudeCliConfig__ClaudeCodeCliPath = "powershell"
$env:CLAUDECLI_ClaudeCliConfig__PollIntervalSeconds = "10"
$env:Logging__LogLevel__Default = "Information"
$env:Logging__LogLevel__ClaudeCLIRunner = "Debug"

# Create a mock Claude CLI script
$mockScript = @'
param($args)
Write-Host "Claude CLI Mock - Processing work item..."
Write-Host "Arguments received: $args"
Start-Sleep -Seconds 2
Write-Host "Mock processing complete!"
exit 0
'@

$mockScriptPath = ".\mock-claude-cli.ps1"
$mockScript | Out-File -FilePath $mockScriptPath -Encoding UTF8

# Update config to use mock script
$env:CLAUDECLI_ClaudeCliConfig__ClaudeCodeCliPath = "powershell"
$env:CLAUDECLI_ClaudeCliConfig__ClaudeCodeCliPath = "$mockScriptPath"

Write-Host "`nStarting Claude CLI Runner with mock work items..." -ForegroundColor Green
Write-Host "The application will:" -ForegroundColor White
Write-Host "  - Poll every 10 seconds for work items" -ForegroundColor Gray
Write-Host "  - Process 2 mock work items (ID: 101, 102)" -ForegroundColor Gray
Write-Host "  - Execute the mock Claude CLI script" -ForegroundColor Gray
Write-Host "  - Show retry logic if errors occur" -ForegroundColor Gray
Write-Host "`nPress Ctrl+C to stop the demo`n" -ForegroundColor Yellow

# Run for 30 seconds
$process = Start-Process -FilePath "dotnet" -ArgumentList "run" -PassThru -NoNewWindow

Write-Host "`nApplication is running. Watch the logs above...`n" -ForegroundColor Cyan

# Let it run for demonstration
$duration = 30
$elapsed = 0
while ($elapsed -lt $duration -and !$process.HasExited) {
    Write-Progress -Activity "Demo Running" -Status "$($duration - $elapsed) seconds remaining" -PercentComplete (($elapsed / $duration) * 100)
    Start-Sleep -Seconds 1
    $elapsed++
}

# Clean up
if (!$process.HasExited) {
    Write-Host "`nStopping demo..." -ForegroundColor Yellow
    $process.Kill()
    $process.WaitForExit(5000)
}

# Remove mock script
Remove-Item $mockScriptPath -ErrorAction SilentlyContinue

Write-Host @"

===============================================
           Demo Complete!
===============================================

What you saw:
✓ Application started and began polling
✓ Found 2 mock work items with @Claude tag
✓ Executed mock Claude CLI for each item
✓ Updated work item status and comments
✓ Handled the processing lifecycle

Next Steps:
1. Configure real Azure DevOps connection
2. Install actual Claude Code CLI
3. Deploy as Windows Service
4. Monitor production workloads

"@ -ForegroundColor Green 