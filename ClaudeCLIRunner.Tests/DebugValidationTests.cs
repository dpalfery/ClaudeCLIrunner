using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClaudeCLIRunner.Services;
using ClaudeCLIRunner.Configuration;
using ClaudeCLIRunner.Models;
using System;
using System.Threading.Tasks;
using System.Threading;
using Xunit.Abstractions;

namespace ClaudeCLIRunner.Tests;

public class DebugValidationTests
{
    private readonly ITestOutputHelper _output;

    public DebugValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ExecuteAsync_WithPipe_DebugTest()
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
        
        _output.WriteLine("Creating executor...");
        var executor = new ClaudeCliExecutor(options, logger);
        _output.WriteLine("Executor created");
        
        var workItem = new WorkItem
        {
            Id = 1,
            Title = "Test | pipe",
            Description = "Description"
        };
        
        _output.WriteLine($"Testing with title: '{workItem.Title}'");

        // Act
        Exception caughtException = null;
        try
        {
            _output.WriteLine("Calling ExecuteAsync...");
            var result = await executor.ExecuteAsync(workItem, CancellationToken.None);
            _output.WriteLine($"ExecuteAsync completed without exception. Success: {result.Success}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Caught exception: {ex.GetType().Name}: {ex.Message}");
            caughtException = ex;
        }

        // Assert
        Assert.NotNull(caughtException);
        Assert.IsType<ArgumentException>(caughtException);
        Assert.Contains("|", caughtException.Message);
    }
}