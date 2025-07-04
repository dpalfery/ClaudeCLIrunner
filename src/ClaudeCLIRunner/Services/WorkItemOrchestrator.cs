using ClaudeCLIRunner.Configuration;
using ClaudeCLIRunner.Interfaces;
using ClaudeCLIRunner.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace ClaudeCLIRunner.Services;

public class WorkItemOrchestrator
{
    private readonly IWorkItemService _workItemService;
    private readonly IClaudeCliExecutor _cliExecutor;
    private readonly ClaudeCliConfig _config;
    private readonly ILogger<WorkItemOrchestrator> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public WorkItemOrchestrator(
        IWorkItemService workItemService,
        IClaudeCliExecutor cliExecutor,
        IOptions<ClaudeCliConfig> config,
        ILogger<WorkItemOrchestrator> logger)
    {
        _workItemService = workItemService;
        _cliExecutor = cliExecutor;
        _config = config.Value;
        _logger = logger;

        // Configure retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                _config.MaxRetries,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    var workItemId = context.TryGetValue("WorkItemId", out var id) ? id : "Unknown";
                    _logger.LogWarning(exception,
                        "Retry {RetryCount} after {Delay} seconds for work item {WorkItemId}",
                        retryCount, timeSpan.TotalSeconds, workItemId);
                });
    }

    public async Task ProcessWorkItemsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting work item processing");

            var workItems = await _workItemService.GetClaudeTaggedWorkItemsAsync(cancellationToken);

            foreach (var workItem in workItems)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Processing cancelled");
                    break;
                }

                await ProcessSingleWorkItemAsync(workItem, cancellationToken);
            }

            _logger.LogInformation("Work item processing completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during work item processing");
            throw;
        }
    }

    private async Task ProcessSingleWorkItemAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing work item {WorkItemId}: {Title}", workItem.Id, workItem.Title);

        try
        {
            // Update work item state to indicate processing has started
            await _workItemService.UpdateWorkItemStateAsync(workItem.Id, "Active", cancellationToken);
            await _workItemService.UpdateWorkItemAsync(workItem.Id, 
                "Claude CLI processing started", cancellationToken);

            // Execute with retry policy
            var context = new Context { { "WorkItemId", workItem.Id } };
            
            var result = await _retryPolicy.ExecuteAsync(
                async (ctx, ct) => await _cliExecutor.ExecuteAsync(workItem, ct),
                context,
                cancellationToken);

            // Handle the result
            if (result.Success)
            {
                _logger.LogInformation("Work item {WorkItemId} processed successfully", workItem.Id);
                await _workItemService.UpdateWorkItemAsync(workItem.Id,
                    $"Claude CLI completed successfully. Duration: {result.Duration.TotalMinutes:F2} minutes",
                    cancellationToken);
                
                // Don't close the work item - let Claude CLI handle that through MCP
            }
            else
            {
                _logger.LogError("Work item {WorkItemId} processing failed: {Error}", 
                    workItem.Id, result.Error);
                
                await _workItemService.UpdateWorkItemAsync(workItem.Id,
                    $"Claude CLI failed after {_config.MaxRetries} attempts. Last error: {result.Error}",
                    cancellationToken);
                
                // Mark as blocked or failed
                await _workItemService.UpdateWorkItemStateAsync(workItem.Id, "Blocked", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing work item {WorkItemId}", workItem.Id);
            
            try
            {
                await _workItemService.UpdateWorkItemAsync(workItem.Id,
                    $"Unexpected error: {ex.Message}",
                    cancellationToken);
                await _workItemService.UpdateWorkItemStateAsync(workItem.Id, "Blocked", cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update work item {WorkItemId} after error", workItem.Id);
            }
        }
    }
} 