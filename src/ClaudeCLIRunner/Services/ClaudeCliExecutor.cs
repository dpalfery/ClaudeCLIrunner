using System.Diagnostics;
using System.Text;
using ClaudeCLIRunner.Configuration;
using ClaudeCLIRunner.Interfaces;
using ClaudeCLIRunner.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaudeCLIRunner.Services;

public class ClaudeCliExecutor : IClaudeCliExecutor
{
    private readonly ClaudeCliConfig _config;
    private readonly ILogger<ClaudeCliExecutor> _logger;

    public ClaudeCliExecutor(IOptions<ClaudeCliConfig> config, ILogger<ClaudeCliExecutor> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public async Task<ClaudeCliResult> ExecuteAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ClaudeCliResult();

        try
        {
            _logger.LogInformation("Starting Claude CLI for work item {WorkItemId}: {Title}", 
                workItem.Id, workItem.Title);

            var arguments = BuildArguments(workItem);
            var processInfo = new ProcessStartInfo
            {
                FileName = _config.ClaudeCodeCliPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_config.ClaudeCodeCliPath)
            };

            // Set environment variables for MCP integration
            processInfo.Environment["MCP_ENDPOINT"] = _config.McpEndpoint;
            processInfo.Environment["AZURE_DEVOPS_ORG"] = _config.AzureDevOpsOrg;
            processInfo.Environment["AZURE_DEVOPS_PROJECT"] = _config.Project;
            processInfo.Environment["AZURE_DEVOPS_REPO"] = _config.Repo;

            using var process = new Process { StartInfo = processInfo };
            
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    _logger.LogDebug("CLI Output: {Output}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    _logger.LogWarning("CLI Error: {Error}", e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for the process to exit or timeout
            var timeout = TimeSpan.FromMinutes(_config.MaxTaskDurationMinutes);
            var completed = await WaitForExitAsync(process, timeout, cancellationToken);

            if (!completed)
            {
                _logger.LogError("Claude CLI timed out after {Timeout} minutes", _config.MaxTaskDurationMinutes);
                KillProcess(process);
                result.Success = false;
                result.Error = $"Process timed out after {_config.MaxTaskDurationMinutes} minutes";
                result.ExitCode = -1;
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Claude CLI execution cancelled");
                KillProcess(process);
                result.Success = false;
                result.Error = "Execution was cancelled";
                result.ExitCode = -2;
            }
            else
            {
                result.ExitCode = process.ExitCode;
                result.Success = process.ExitCode == 0;
                result.Output = outputBuilder.ToString();
                result.Error = errorBuilder.ToString();

                _logger.LogInformation("Claude CLI completed with exit code {ExitCode}", result.ExitCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Claude CLI");
            result.Success = false;
            result.Error = ex.Message;
            result.ExitCode = -3;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            _logger.LogInformation("Claude CLI execution took {Duration}", result.Duration);
        }

        return result;
    }

    private string BuildArguments(WorkItem workItem)
    {
        // Build command line arguments for Claude CLI
        var args = new List<string>
        {
            "--work-item-id", workItem.Id.ToString(),
            "--title", $"\"{workItem.Title}\"",
            "--description", $"\"{workItem.Description}\"",
            "--branch", _config.DefaultBranch
        };

        return string.Join(" ", args);
    }

    private async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true); // Kill entire process tree
                process.WaitForExit(5000); // Wait up to 5 seconds for process to exit
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error killing process");
        }
    }
} 