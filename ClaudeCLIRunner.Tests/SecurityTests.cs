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

public class SecurityTests
{
    private readonly ILogger<ClaudeCliExecutor> _logger;
    private readonly ClaudeCliConfig _config;

    public SecurityTests()
    {
        _logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ClaudeCliExecutor>();
        _config = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "/bin/echo", // Use echo command which exists on Linux
            MaxTaskDurationMinutes = 60,
            DefaultBranch = "main",
            McpEndpoint = "https://test.example.com",
            AzureDevOpsOrg = "testorg",
            Project = "testproject",
            Repo = "testrepo"
        };
    }

    [Fact]
    public void Constructor_WithInvalidExecutablePath_ThrowsArgumentException()
    {
        // Arrange
        var invalidConfig = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "invalid|path" // Contains pipe character
        };
        var options = Options.Create(invalidConfig);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ClaudeCliExecutor(options, _logger));
    }

    [Fact]
    public void Constructor_WithEmptyExecutablePath_ThrowsArgumentException()
    {
        // Arrange
        var invalidConfig = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = ""
        };
        var options = Options.Create(invalidConfig);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ClaudeCliExecutor(options, _logger));
    }

    [Fact]
    public async Task ExecuteAsync_WithCommandInjectionInTitle_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(_config);
        var executor = new ClaudeCliExecutor(options, _logger);
        
        var maliciousWorkItem = new WorkItem
        {
            Id = 1,
            Title = "Test & malicious command",
            Description = "Normal description"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            executor.ExecuteAsync(maliciousWorkItem, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithCommandInjectionInDescription_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(_config);
        var executor = new ClaudeCliExecutor(options, _logger);
        
        var maliciousWorkItem = new WorkItem
        {
            Id = 1,
            Title = "Normal Title",
            Description = "Description with $(dangerous command)"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            executor.ExecuteAsync(maliciousWorkItem, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithPipeCharacterInTitle_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(_config);
        var executor = new ClaudeCliExecutor(options, _logger);
        
        var maliciousWorkItem = new WorkItem
        {
            Id = 1,
            Title = "Title | rm -rf /",
            Description = "Description"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            executor.ExecuteAsync(maliciousWorkItem, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithExcessivelyLongTitle_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(_config);
        var executor = new ClaudeCliExecutor(options, _logger);
        
        var workItemWithLongTitle = new WorkItem
        {
            Id = 1,
            Title = new string('A', 1001), // Exceeds MaxTitleLength of 1000
            Description = "Description"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            executor.ExecuteAsync(workItemWithLongTitle, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullWorkItem_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(_config);
        var executor = new ClaudeCliExecutor(options, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            executor.ExecuteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidWorkItemId_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(_config);
        var executor = new ClaudeCliExecutor(options, _logger);
        
        var invalidWorkItem = new WorkItem
        {
            Id = -1, // Invalid ID
            Title = "Valid Title",
            Description = "Valid Description"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            executor.ExecuteAsync(invalidWorkItem, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithDangerousPatterns_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(_config);
        var executor = new ClaudeCliExecutor(options, _logger);
        
        var dangerousPatterns = new[]
        {
            "powershell.exe",
            "cmd.exe", 
            "bash script",
            "net user",
            "certutil -decode"
        };

        foreach (var pattern in dangerousPatterns)
        {
            var maliciousWorkItem = new WorkItem
            {
                Id = 1,
                Title = $"Title with {pattern}",
                Description = "Description"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                executor.ExecuteAsync(maliciousWorkItem, CancellationToken.None));
        }
    }
}