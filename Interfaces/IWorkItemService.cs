using ClaudeCLIRunner.Models;

namespace ClaudeCLIRunner.Interfaces;

public interface IWorkItemService
{
    Task<IEnumerable<WorkItem>> GetClaudeTaggedWorkItemsAsync(CancellationToken cancellationToken);
    Task UpdateWorkItemAsync(int workItemId, string comment, CancellationToken cancellationToken);
    Task UpdateWorkItemStateAsync(int workItemId, string state, CancellationToken cancellationToken);
} 