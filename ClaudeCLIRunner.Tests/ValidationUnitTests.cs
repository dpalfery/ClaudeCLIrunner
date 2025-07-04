using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClaudeCLIRunner.Services;
using ClaudeCLIRunner.Configuration;
using ClaudeCLIRunner.Models;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace ClaudeCLIRunner.Tests;

public class ValidationUnitTests
{
    [Theory]
    [InlineData("Title with | pipe")]
    [InlineData("Title with & ampersand")]
    [InlineData("Title with ; semicolon")]
    [InlineData("Title with $ dollar")]
    [InlineData("Title with ` backtick")]
    [InlineData("Title with powershell")]
    [InlineData("Title with cmd")]
    [InlineData("Title with bash")]
    public async Task ExecuteAsync_WithDangerousPattern_ShouldThrowArgumentException(string dangerousTitle)
    {
        // Arrange
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ClaudeCliExecutor>();
        var config = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "/bin/echo",
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
            Title = dangerousTitle,
            Description = "Safe description"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            executor.ExecuteAsync(workItem, CancellationToken.None));
            
        Assert.NotNull(exception);
        Assert.Contains("dangerous pattern", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithSafeInput_ShouldNotThrowValidationException()
    {
        // Arrange
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ClaudeCliExecutor>();
        var config = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "/bin/echo",
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
            Title = "Safe title without dangerous patterns",
            Description = "Safe description without dangerous patterns"
        };

        // Act - this should not throw an ArgumentException for validation
        // It might throw other exceptions related to process execution, but not validation
        try
        {
            var result = await executor.ExecuteAsync(workItem, CancellationToken.None);
            // If we get here, validation passed (good)
        }
        catch (ArgumentException ex) when (ex.Message.Contains("dangerous pattern"))
        {
            // This should not happen for safe input
            Assert.True(false, $"Validation failed for safe input: {ex.Message}");
        }
        catch (Exception)
        {
            // Other exceptions (like process execution failures) are acceptable for this test
            // We're only testing that validation doesn't reject safe input
        }
    }
}