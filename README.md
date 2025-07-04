# ClaudeCLIrunner
A Claude Agent that uses Azure DevOps to manage its tasks

## Implementation Status ✅

This repository now contains a complete implementation of the Claude CLI Runner orchestrator based on the design specifications below. The implementation includes:

### Features Implemented
- ✅ **Polling Service**: Background service that polls Azure DevOps for @Claude-tagged work items
- ✅ **Process Management**: Executes Claude CLI with proper timeout enforcement (60 minutes)
- ✅ **Retry Logic**: Exponential backoff with configurable max retries (default: 3)
- ✅ **Error Handling**: Comprehensive error reporting back to work items
- ✅ **Windows Service Support**: Can run as a Windows Service for production deployment
- ✅ **Configuration**: JSON-based configuration with environment variable overrides
- ✅ **Logging**: Structured logging with configurable levels
- ✅ **Mock Testing**: Includes mock work item service for testing

### Quick Start

1. **Configure the application** by editing `appsettings.json`:
   ```json
   {
     "ClaudeCliConfig": {
       "ClaudeCodeCliPath": "C:\\path\\to\\claude.exe",
       "McpEndpoint": "https://your-mcp-server.com",
       "AzureDevOpsOrg": "yourorg",
       "Project": "yourproject",
       "Repo": "yourrepo"
     }
   }
   ```

2. **Run the demo**:
   ```powershell
   .\Demo-ClaudeRunner.ps1
   ```

3. **Deploy as Windows Service**:
   ```powershell
   .\Scripts\Deploy-AsService.ps1 -InstallPath "C:\Services\ClaudeCLI"
   ```

### Project Structure
- `/Configuration` - Configuration models
- `/Models` - Data models (WorkItem, etc.)
- `/Services` - Core services (Orchestrator, CLI Executor, etc.)
- `/Interfaces` - Service interfaces
- `/Scripts` - Deployment and testing scripts
- `/Tests` - Unit tests with xUnit and Moq

### Next Steps
See [IMPLEMENTATION.md](IMPLEMENTATION.md) for detailed implementation guide and extension points.

---

## Original Design Document

Certainly! Here is the previous response as raw, unrendered Markdown:

---

### Solution Design Document: Claude Code CLI + Azure DevOps via MCP

#### 1. Overview

This solution automates the completion of Azure DevOps (AzDo) work items tagged with `@Claude` by leveraging **Claude Code CLI** (Anthropic) as the primary automation engine. The CLI is responsible for all interactions with AzDo via the [MCP server](https://github.com/microsoft/azure-devops-mcp), including work item management, code changes, PR creation, and feedback handling. A minimal C# orchestration layer is used solely to trigger and monitor Claude Code CLI runs, either on a timed polling basis or via webhooks.

---

#### 2. Objectives

- Detect AzDo work items tagged with `@Claude` (via polling or webhook).
- Delegate all work item processing, repo operations, PR management, and feedback handling to Claude Code CLI.
- Ensure errors and status updates are written back to the original work item.
- Retry failed tasks up to 3 times.
- Enforce a maximum runtime of 60 minutes per task.

---

#### 3. Architecture Overview

**Components:**

- **Claude Code CLI**: Anthropic’s CLI, responsible for all business logic, code generation, AzDo/MCP integration, error reporting, and feedback handling.
- **MCP Server**: Secure proxy for AzDo REST APIs, used exclusively by Claude Code CLI.
- **Minimal Orchestrator**: C# service, responsible only for triggering Claude Code CLI runs, monitoring execution, enforcing timeouts, and managing retries.
- **Azure DevOps**: Source of truth for work items, code, and PRs.

**Triggering:**

- **Polling**: Orchestrator checks for new/updated `@Claude`-tagged work items at a configurable interval.
- **Webhook (preferred if available)**: Orchestrator receives event notifications from AzDo and triggers Claude Code CLI accordingly.

---

#### 4. Detailed Design

##### 4.1. Claude Code CLI Responsibilities

- **MCP Integration**: Handles all communication with AzDo via MCP (work items, git, PRs, comments).
- **Task Execution**: Reads work item details, clones repo, creates branch, generates code/tests/docs, pushes changes, creates PR, monitors PR for feedback.
- **Error Handling**: On error, writes a detailed error message back to the original work item (via MCP).
- **Feedback Loop**: Monitors PR for `@Claude`-tagged comments, applies changes, updates PR, and marks comments as resolved.
- **Timeout Awareness**: Exits gracefully if the orchestrator signals a timeout.

##### 4.2. Minimal C# Orchestrator Responsibilities

- **Triggering**: Starts Claude Code CLI for each new/updated `@Claude`-tagged work item (via polling or webhook).
- **Timeout Enforcement**: Kills Claude Code CLI process if it exceeds 60 minutes.
- **Retry Logic**: Retries failed tasks up to 3 times, with exponential backoff.
- **Monitoring**: Captures CLI exit codes and logs, and ensures errors are surfaced.
- **Configuration**: Stores CLI path, polling interval, and other settings.

##### 4.3. Error Handling & Reporting

- **On Error**: Claude Code CLI writes error details to the work item (via MCP).
- **Retries**: Orchestrator retries up to 3 times on failure.
- **Timeouts**: If the CLI exceeds 60 minutes, orchestrator terminates the process and logs a timeout error to the work item.

##### 4.4. Configuration

- **Config File**: `claudecli.json` (CLI path, MCP endpoint, polling/webhook settings, retry policy, timeout).
- **Secrets**: Stored securely (e.g., Azure Key Vault, local secrets manager).

---

#### 5. Workflow Example

1. **Trigger**: Orchestrator detects a new/updated work item tagged `@Claude` (via polling or webhook).
2. **Execution**: Orchestrator launches Claude Code CLI, passing work item context.
3. **Processing**: Claude Code CLI handles all repo, code, PR, and feedback operations via MCP.
4. **Error/Timeout**: If an error occurs or 60 minutes elapse, orchestrator ensures error is written to the work item and retries up to 3 times.
5. **Completion**: On success, PR is created and linked to the work item; feedback loop continues until PR is merged.

---

#### 6. Security Considerations

- All credentials and tokens are encrypted at rest.
- Least privilege principle for AzDo and MCP access.
- Audit logging for all actions.

---

#### 7. Extensibility

- Orchestrator is modular and can be extended for additional triggers or reporting.
- Claude Code CLI can be updated independently as Anthropic releases new features.

---

#### 8. Technologies Used

- **C# / .NET 8+** (Minimal Orchestrator)
- **Claude Code CLI (Anthropic)**
- **Azure DevOps (Repos, Work Items, Pipelines)**
- **Azure DevOps MCP**
- **Git**
- **Azure Key Vault (optional for secrets)**
- **YAML (for config and pipelines)**

---

#### 9. Open Questions / Next Steps

- Is webhook integration with AzDo feasible in your environment, or should polling be the default?
- Should orchestrator run as a service, container, or scheduled job?
- What is the preferred logging and alerting mechanism for orchestrator failures?

---

#### 10. Appendix: Example Config

```json
{
  "claudeCodeCliPath": "/usr/local/bin/claude",
  "mcpEndpoint": "https://mcp.example.com",
  "azureDevOpsOrg": "myorg",
  "project": "myproject",
  "repo": "myrepo",
  "defaultBranch": "main",
  "pollIntervalSeconds": 60,
  "maxRetries": 3,
  "maxTaskDurationMinutes": 60
}


Let me know if you want a sample orchestrator code outline, webhook setup details, or a more detailed error handling flow! Warning: Some URLs are invalid

https://mcp.example.com