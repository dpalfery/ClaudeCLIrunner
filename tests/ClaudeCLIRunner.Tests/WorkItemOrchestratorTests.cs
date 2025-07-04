using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClaudeCLIRunner.Services;
using ClaudeCLIRunner.Interfaces;
using ClaudeCLIRunner.Configuration;
using ClaudeCLIRunner.Models;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ClaudeCLIRunner.Tests;

public class WorkItemOrchestratorTests
{
    private readonly Mock<IWorkItemService> _mockWorkItemService;
    private readonly Mock<IClaudeCliExecutor> _mockCliExecutor;
    private readonly Mock<ILogger<WorkItemOrchestrator>> _mockLogger;
    private readonly IOptions<ClaudeCliConfig> _config;
    private readonly WorkItemOrchestrator _orchestrator;

    public WorkItemOrchestratorTests()
    {
        _mockWorkItemService = new Mock<IWorkItemService>();
        _mockCliExecutor = new Mock<IClaudeCliExecutor>();
        _mockLogger = new Mock<ILogger<WorkItemOrchestrator>>();
        
        var config = new ClaudeCliConfig
        {
            MaxRetries = 3,
            MaxTaskDurationMinutes = 60
        };
        _config = Options.Create(config);
        
        _orchestrator = new WorkItemOrchestrator(
            _mockWorkItemService.Object,
            _mockCliExecutor.Object,
            _config,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessWorkItemsAsync_WithNoWorkItems_CompletesSuccessfully()
    {
        // Arrange
        _mockWorkItemService
            .Setup(x => x.GetClaudeTaggedWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItem>());

        // Act
        await _orchestrator.ProcessWorkItemsAsync(CancellationToken.None);

        // Assert
        _mockWorkItemService.Verify(x => x.GetClaudeTaggedWorkItemsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCliExecutor.Verify(x => x.ExecuteAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWorkItemsAsync_WithWorkItems_ProcessesEachItem()
    {
        // Arrange
        var workItems = new List<WorkItem>
        {
            new WorkItem { Id = 1, Title = "Test 1" },
            new WorkItem { Id = 2, Title = "Test 2" }
        };

        _mockWorkItemService
            .Setup(x => x.GetClaudeTaggedWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        _mockCliExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClaudeCliResult { Success = true });

        // Act
        await _orchestrator.ProcessWorkItemsAsync(CancellationToken.None);

        // Assert
        _mockCliExecutor.Verify(x => x.ExecuteAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockWorkItemService.Verify(x => x.UpdateWorkItemStateAsync(It.IsAny<int>(), "Active", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessWorkItemsAsync_WithFailedExecution_UpdatesWorkItemAsBlocked()
    {
        // Arrange
        var workItem = new WorkItem { Id = 1, Title = "Test Failed" };

        _mockWorkItemService
            .Setup(x => x.GetClaudeTaggedWorkItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { workItem });

        _mockCliExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<WorkItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClaudeCliResult { Success = false, Error = "Test error" });

        // Act
        await _orchestrator.ProcessWorkItemsAsync(CancellationToken.None);

        // Assert
        _mockWorkItemService.Verify(x => x.UpdateWorkItemStateAsync(1, "Blocked", It.IsAny<CancellationToken>()), Times.Once);
        _mockWorkItemService.Verify(x => x.UpdateWorkItemAsync(1, It.Is<string>(s => s.Contains("failed")), It.IsAny<CancellationToken>()), Times.Once);
    }
} 