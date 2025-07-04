using ClaudeCLIRunner.Models;

namespace ClaudeCLIRunner.Interfaces;

public interface IClaudeCliExecutor
{
    Task<ClaudeCliResult> ExecuteAsync(WorkItem workItem, CancellationToken cancellationToken);
}

public class ClaudeCliResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public TimeSpan Duration { get; set; }
} 