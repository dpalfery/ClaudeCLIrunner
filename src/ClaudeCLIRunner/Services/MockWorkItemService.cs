using ClaudeCLIRunner.Interfaces;
using ClaudeCLIRunner.Models;
using Microsoft.Extensions.Logging;

namespace ClaudeCLIRunner.Services;

public class MockWorkItemService : IWorkItemService
{
    private readonly ILogger<MockWorkItemService> _logger;
    private readonly List<WorkItem> _mockWorkItems;

    public MockWorkItemService(ILogger<MockWorkItemService> logger)
    {
        _logger = logger;
        
        // Initialize with some mock data for testing
        _mockWorkItems = new List<WorkItem>
        {
            new WorkItem
            {
                Id = 101,
                Title = "Implement user authentication module",
                Description = "Create a secure authentication module with JWT tokens and refresh token support. @Claude",
                State = "New",
                Tags = new List<string> { "@Claude", "Security", "Backend" },
                CreatedDate = DateTime.UtcNow.AddHours(-2),
                ChangedDate = DateTime.UtcNow.AddHours(-1)
            },
            new WorkItem
            {
                Id = 102,
                Title = "Add unit tests for payment service",
                Description = "Write comprehensive unit tests for the payment processing service. @Claude",
                State = "Active",
                Tags = new List<string> { "@Claude", "Testing" },
                CreatedDate = DateTime.UtcNow.AddHours(-5),
                ChangedDate = DateTime.UtcNow.AddMinutes(-30)
            }
        };
    }

    public async Task<IEnumerable<WorkItem>> GetClaudeTaggedWorkItemsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching Claude-tagged work items");
        
        // Simulate async operation
        await Task.Delay(100, cancellationToken);
        
        var claudeItems = _mockWorkItems.Where(wi => wi.HasClaudeTag && wi.State != "Closed").ToList();
        
        _logger.LogInformation("Found {Count} Claude-tagged work items", claudeItems.Count);
        
        return claudeItems;
    }

    public async Task UpdateWorkItemAsync(int workItemId, string comment, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating work item {WorkItemId} with comment", workItemId);
        
        // Simulate async operation
        await Task.Delay(50, cancellationToken);
        
        var workItem = _mockWorkItems.FirstOrDefault(wi => wi.Id == workItemId);
        if (workItem != null)
        {
            workItem.ChangedDate = DateTime.UtcNow;
            _logger.LogInformation("Work item {WorkItemId} updated successfully", workItemId);
        }
        else
        {
            _logger.LogWarning("Work item {WorkItemId} not found", workItemId);
        }
    }

    public async Task UpdateWorkItemStateAsync(int workItemId, string state, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating work item {WorkItemId} state to {State}", workItemId, state);
        
        // Simulate async operation
        await Task.Delay(50, cancellationToken);
        
        var workItem = _mockWorkItems.FirstOrDefault(wi => wi.Id == workItemId);
        if (workItem != null)
        {
            workItem.State = state;
            workItem.ChangedDate = DateTime.UtcNow;
            _logger.LogInformation("Work item {WorkItemId} state updated to {State}", workItemId, state);
        }
        else
        {
            _logger.LogWarning("Work item {WorkItemId} not found", workItemId);
        }
    }
} 