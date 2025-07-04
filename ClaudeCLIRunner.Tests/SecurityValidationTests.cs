using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClaudeCLIRunner.Services;
using ClaudeCLIRunner.Configuration;
using ClaudeCLIRunner.Models;
using System;
using System.Threading;

namespace ClaudeCLIRunner.Tests;

public class SecurityValidationTests
{
    [Fact]
    public void ExecuteAsync_WithPipeInTitle_ShouldThrowArgumentException()
    {
        // Arrange
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ClaudeCliExecutor>();
        var config = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "/bin/echo", // Use a valid path
            MaxTaskDurationMinutes = 60,
            DefaultBranch = "main",
            McpEndpoint = "https://test.example.com",
            AzureDevOpsOrg = "testorg", 
            Project = "testproject",
            Repo = "testrepo"
        };
        var options = Options.Create(config);
        var executor = new ClaudeCliExecutor(options, logger);
        
        var workItem = new WorkItem
        {
            Id = 1,
            Title = "Test | dangerous command", // Contains pipe character
            Description = "Description"
        };

        // Act & Assert
        var exception = Assert.ThrowsAsync<ArgumentException>(async () => 
            await executor.ExecuteAsync(workItem, CancellationToken.None));
        
        // Verify the exception message mentions the dangerous pattern
        Assert.Contains("|", exception.Result.Message);
    }
}