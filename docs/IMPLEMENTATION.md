# Claude CLI Runner Implementation

## Overview

This implementation provides a minimal C# orchestrator for running Claude Code CLI to process Azure DevOps work items tagged with `@Claude`. The orchestrator handles:

- Polling for work items
- Executing Claude CLI with timeout management
- Retry logic with exponential backoff
- Error reporting back to work items
- Running as a Windows service

## Architecture

### Components

1. **Configuration** (`ClaudeCliConfig.cs`)
   - Stores all configuration settings
   - Loaded from appsettings.json and environment variables

2. **Models** (`WorkItem.cs`)
   - Represents Azure DevOps work items
   - Contains helper property to check for @Claude tag

3. **Services**
   - `ClaudeCliExecutor.cs`: Executes Claude CLI with proper process management
   - `WorkItemOrchestrator.cs`: Orchestrates work item processing with retry logic
   - `PollingBackgroundService.cs`: Background service for polling
   - `MockWorkItemService.cs`: Mock implementation for testing

4. **Interfaces**
   - `IClaudeCliExecutor.cs`: Interface for CLI execution
   - `IWorkItemService.cs`: Interface for work item operations

## Key Features

### Process Management
- Proper timeout enforcement (60 minutes default)
- Process tree termination on timeout/cancellation
- Capture of stdout and stderr
- Exit code handling

### Retry Logic
- Exponential backoff (2^n seconds between retries)
- Configurable max retries (default: 3)
- Retry on any exception
- Logging of retry attempts

### Error Handling
- All errors written back to work items
- Graceful shutdown on cancellation
- Comprehensive logging at all levels
- Work item state management (Active -> Blocked on failure)

### Configuration
- JSON configuration file support
- Environment variable overrides (CLAUDECLI_ prefix)
- Command-line argument support
- Hot reload of configuration

## Running the Application

### As Console Application
```bash
dotnet run
```

### As Windows Service
1. Build the application:
   ```bash
   dotnet publish -c Release -r win-x64
   ```

2. Install as service:
   ```powershell
   sc.exe create "ClaudeCLIRunner" binPath="C:\path\to\ClaudeCLIRunner.exe"
   ```

3. Start the service:
   ```powershell
   sc.exe start "ClaudeCLIRunner"
   ```

## Configuration

Edit `appsettings.json`:

```json
{
  "ClaudeCliConfig": {
    "ClaudeCodeCliPath": "C:\\Program Files\\Claude\\claude.exe",
    "McpEndpoint": "https://your-mcp-server.com",
    "AzureDevOpsOrg": "yourorg",
    "Project": "yourproject",
    "Repo": "yourrepo",
    "DefaultBranch": "main",
    "PollIntervalSeconds": 60,
    "MaxRetries": 3,
    "MaxTaskDurationMinutes": 60
  }
}
```

### Environment Variables
All settings can be overridden using environment variables:
```bash
CLAUDECLI_ClaudeCliConfig__ClaudeCodeCliPath=C:\claude\claude.exe
CLAUDECLI_ClaudeCliConfig__PollIntervalSeconds=30
```

## Integration Points

### Claude CLI Arguments
The executor passes these arguments to Claude CLI:
- `--work-item-id`: The work item ID
- `--title`: Work item title (quoted)
- `--description`: Work item description (quoted)
- `--branch`: Default branch name

### Environment Variables for Claude CLI
These are set for the Claude CLI process:
- `MCP_ENDPOINT`: MCP server endpoint
- `AZURE_DEVOPS_ORG`: Azure DevOps organization
- `AZURE_DEVOPS_PROJECT`: Project name
- `AZURE_DEVOPS_REPO`: Repository name

## Extending the Implementation

### Real Azure DevOps Integration
Replace `MockWorkItemService` with a real implementation:

1. Add Azure DevOps SDK:
   ```bash
   dotnet add package Microsoft.TeamFoundationServer.Client
   ```

2. Implement `IWorkItemService` using the SDK

3. Update DI registration in `Program.cs`

### Webhook Support
The configuration includes webhook settings. To implement:

1. Add ASP.NET Core:
   ```bash
   dotnet add package Microsoft.AspNetCore.Hosting
   ```

2. Create webhook controller
3. Process webhook events instead of polling

### Advanced Logging
For production, consider adding:
- Serilog for file logging
- Application Insights for telemetry
- Structured logging for better querying

## Security Considerations

1. **Credentials**: Store Azure DevOps PAT securely (e.g., Windows Credential Manager)
2. **Process Isolation**: Claude CLI runs in separate process with limited permissions
3. **Input Validation**: All work item data should be validated before passing to CLI
4. **Audit Logging**: Log all actions for compliance

## Monitoring

Key metrics to monitor:
- Work items processed per hour
- Average processing time
- Retry rate
- Error rate
- Timeout rate

## Troubleshooting

### Common Issues

1. **Claude CLI not found**
   - Check `ClaudeCodeCliPath` in configuration
   - Ensure Claude CLI is installed and accessible

2. **Timeouts**
   - Increase `MaxTaskDurationMinutes` if needed
   - Check Claude CLI logs for long-running operations

3. **High retry rate**
   - Check MCP server connectivity
   - Review Claude CLI error logs
   - Verify Azure DevOps permissions

### Debug Mode
Set log level to Debug in appsettings.json:
```json
"ClaudeCLIRunner": "Debug"
```

## Next Steps

1. Implement real Azure DevOps integration
2. Add webhook support for real-time processing
3. Create deployment scripts
4. Add unit and integration tests
5. Set up CI/CD pipeline
6. Add health checks and metrics endpoints 