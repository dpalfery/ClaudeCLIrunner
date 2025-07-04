using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
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
    
    // Security: Regex to validate executable paths - allow alphanumeric, spaces, hyphens, dots, and common path separators
    // Accept both Windows executables (.exe, .bat, .cmd, .ps1) and Unix executables (no extension)
    private static readonly Regex ExecutablePathRegex = new Regex(@"^[a-zA-Z0-9\s\-\.\\/:\\]+(\.(exe|bat|cmd|ps1))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // Security: Maximum lengths for input validation
    private const int MaxTitleLength = 1000;
    private const int MaxDescriptionLength = 50000;
    private const int MaxBranchNameLength = 255;

    public ClaudeCliExecutor(IOptions<ClaudeCliConfig> config, ILogger<ClaudeCliExecutor> logger)
    {
        _config = config.Value;
        _logger = logger;
        
        // Security: Validate configuration at startup
        ValidateConfiguration(_config);
        
        // Security: Validate executable path at startup
        ValidateExecutablePath(_config.ClaudeCodeCliPath);
    }

    public async Task<ClaudeCliResult> ExecuteAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        // Security: Validate input before processing - do this outside try-catch so validation exceptions bubble up
        ValidateWorkItem(workItem);
        
        var stopwatch = Stopwatch.StartNew();
        var result = new ClaudeCliResult();

        try
        {
            _logger.LogInformation("Starting Claude CLI for work item {WorkItemId}", workItem.Id);

            var arguments = BuildSecureArguments(workItem);
            var processInfo = new ProcessStartInfo
            {
                FileName = _config.ClaudeCodeCliPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_config.ClaudeCodeCliPath)
            };
            
            // Security: Add arguments individually to prevent command injection
            foreach (var arg in arguments)
            {
                processInfo.ArgumentList.Add(arg);
            }

            // Security: Sanitize environment variables before setting
            SetSecureEnvironmentVariable(processInfo, "MCP_ENDPOINT", _config.McpEndpoint);
            SetSecureEnvironmentVariable(processInfo, "AZURE_DEVOPS_ORG", _config.AzureDevOpsOrg);
            SetSecureEnvironmentVariable(processInfo, "AZURE_DEVOPS_PROJECT", _config.Project);
            SetSecureEnvironmentVariable(processInfo, "AZURE_DEVOPS_REPO", _config.Repo);
            
            // Security: Handle PAT from environment variable (preferred) or config (deprecated)
            var pat = Environment.GetEnvironmentVariable("CLAUDECLI_AZURE_DEVOPS_PAT") ?? _config.AzureDevOpsPat;
            if (!string.IsNullOrEmpty(pat))
            {
                SetSecureEnvironmentVariable(processInfo, "AZURE_DEVOPS_PAT", pat);
            }

            using var process = new Process { StartInfo = processInfo };
            
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    // Security: Only log at debug level and sanitize output
                    _logger.LogDebug("CLI Output: {Output}", SanitizeLogOutput(e.Data));
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    // Security: Only log at debug level and sanitize error output
                    _logger.LogDebug("CLI Error: {Error}", SanitizeLogOutput(e.Data));
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

    private void ValidateConfiguration(ClaudeCliConfig config)
    {
        // Security: Validate critical security settings
        if (config.RequireHttps && !config.McpEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("HTTPS is required for MCP endpoint when RequireHttps is enabled");
        }
        
        if (config.MaxConcurrentProcesses < 1 || config.MaxConcurrentProcesses > 10)
        {
            throw new ArgumentException("MaxConcurrentProcesses must be between 1 and 10");
        }
        
        if (config.WebhookPort < 1024 || config.WebhookPort > 65535)
        {
            throw new ArgumentException("WebhookPort must be between 1024 and 65535");
        }
        
        // Security: Warn about deprecated PAT usage
        if (!string.IsNullOrEmpty(config.AzureDevOpsPat))
        {
            _logger.LogWarning("AzureDevOpsPat in configuration is deprecated for security reasons. Use environment variable CLAUDECLI_AZURE_DEVOPS_PAT instead.");
        }
        
        // Security: Validate audit logging settings
        if (config.EnableAuditLogging && !string.IsNullOrEmpty(config.AuditLogPath))
        {
            var auditDir = Path.GetDirectoryName(config.AuditLogPath);
            if (!string.IsNullOrEmpty(auditDir) && !Directory.Exists(auditDir))
            {
                throw new ArgumentException($"Audit log directory does not exist: {auditDir}");
            }
        }
    }
    
    private void ValidateExecutablePath(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be null or empty", nameof(executablePath));
        }
        
        if (!ExecutablePathRegex.IsMatch(executablePath))
        {
            throw new ArgumentException($"Invalid executable path format: {executablePath}", nameof(executablePath));
        }
        
        // Security: Check if path tries to escape to restricted areas
        var fullPath = Path.GetFullPath(executablePath);
        if (fullPath.Contains(".."))
        {
            throw new ArgumentException($"Path traversal detected in executable path: {executablePath}", nameof(executablePath));
        }
    }
    
    private void ValidateWorkItem(WorkItem workItem)
    {
        if (workItem == null)
        {
            throw new ArgumentNullException(nameof(workItem));
        }
        
        if (workItem.Id <= 0)
        {
            throw new ArgumentException("Work item ID must be positive", nameof(workItem));
        }
        
        if (string.IsNullOrEmpty(workItem.Title))
        {
            throw new ArgumentException("Work item title cannot be null or empty", nameof(workItem));
        }
        
        if (workItem.Title.Length > MaxTitleLength)
        {
            throw new ArgumentException($"Work item title exceeds maximum length of {MaxTitleLength} characters", nameof(workItem));
        }
        
        if (workItem.Description != null && workItem.Description.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"Work item description exceeds maximum length of {MaxDescriptionLength} characters", nameof(workItem));
        }
        
        if (_config.DefaultBranch.Length > MaxBranchNameLength)
        {
            throw new ArgumentException($"Branch name exceeds maximum length of {MaxBranchNameLength} characters");
        }
        
        // Security: Check for potential command injection patterns
        ValidateInputForCommandInjection(workItem.Title, nameof(workItem.Title));
        if (!string.IsNullOrEmpty(workItem.Description))
        {
            ValidateInputForCommandInjection(workItem.Description, nameof(workItem.Description));
        }
        ValidateInputForCommandInjection(_config.DefaultBranch, "DefaultBranch");
    }
    
    private void ValidateInputForCommandInjection(string input, string parameterName)
    {
        // Security: Check for common command injection patterns
        var dangerousPatterns = new[]
        {
            "|", "&", ";", "$", "`", "$(", "${", "powershell", "cmd", "bash", "/bin/", "\\bin\\",
            "rm -", "del ", "format ", "net ", "sc ", "reg ", "wmic ", "certutil"
        };
        
        var lowerInput = input.ToLowerInvariant();
        foreach (var pattern in dangerousPatterns)
        {
            if (lowerInput.Contains(pattern))
            {
                _logger.LogWarning("Potential command injection detected in {Parameter}: {Pattern}", parameterName, pattern);
                throw new ArgumentException($"Input contains potentially dangerous pattern '{pattern}' in {parameterName}", parameterName);
            }
        }
    }
    
    private List<string> BuildSecureArguments(WorkItem workItem)
    {
        // Security: Use List<string> for proper argument separation instead of concatenated string
        // This prevents command injection by ensuring each argument is properly escaped by ProcessStartInfo
        var args = new List<string>
        {
            "--work-item-id",
            workItem.Id.ToString(),
            "--title",
            SanitizeArgumentValue(workItem.Title),
            "--description",
            SanitizeArgumentValue(workItem.Description ?? string.Empty),
            "--branch",
            SanitizeArgumentValue(_config.DefaultBranch)
        };

        return args;
    }
    
    private string SanitizeArgumentValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        
        // Security: Remove or escape potentially dangerous characters
        // Replace newlines and carriage returns with spaces to prevent argument splitting
        var sanitized = value.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
        
        // Remove null characters
        sanitized = sanitized.Replace('\0', ' ');
        
        // Trim whitespace
        sanitized = sanitized.Trim();
        
        return sanitized;
    }

    private void SetSecureEnvironmentVariable(ProcessStartInfo processInfo, string name, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _logger.LogWarning("Environment variable {Name} is null or empty", name);
            return;
        }
        
        // Security: Validate environment variable values
        if (value.Length > 2048) // Reasonable limit for environment variables
        {
            throw new ArgumentException($"Environment variable {name} value is too long");
        }
        
        // Security: Check for injection patterns in environment variables
        ValidateInputForCommandInjection(value, $"Environment variable {name}");
        
        processInfo.Environment[name] = value;
    }
    
    private string SanitizeLogOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return string.Empty;
        }
        
        // Security: Remove potentially sensitive information from logs
        var sanitized = output;
        
        // Mask common sensitive patterns
        sanitized = Regex.Replace(sanitized, @"(?i)(token|password|key|secret|pat)[=:\s]+[^\s]*", "$1=***", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"(?i)(bearer\s+)[^\s]*", "$1***", RegexOptions.IgnoreCase);
        
        // Truncate very long output to prevent log flooding
        if (sanitized.Length > 500)
        {
            sanitized = sanitized.Substring(0, 500) + "... [truncated]";
        }
        
        return sanitized;
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