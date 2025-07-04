using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ClaudeCLIRunner.Services;
using ClaudeCLIRunner.Configuration;
using System;

namespace ClaudeCLIRunner.Tests;

public class ConfigurationSecurityTests
{
    private readonly ILogger<ClaudeCliExecutor> _logger;

    public ConfigurationSecurityTests()
    {
        _logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ClaudeCliExecutor>();
    }

    [Fact]
    public void Constructor_WithHttpEndpointWhenHttpsRequired_ThrowsArgumentException()
    {
        // Arrange
        var config = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "/bin/echo",
            McpEndpoint = "http://insecure.example.com", // HTTP not HTTPS
            RequireHttps = true,
            AzureDevOpsOrg = "org",
            Project = "project",
            Repo = "repo"
        };
        var options = Options.Create(config);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new ClaudeCliExecutor(options, _logger));
        Assert.Contains("HTTPS is required", exception.Message);
    }

    [Fact]
    public void Constructor_WithInvalidMaxConcurrentProcesses_ThrowsArgumentException()
    {
        // Arrange
        var config = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "/bin/echo",
            McpEndpoint = "https://secure.example.com",
            MaxConcurrentProcesses = 0, // Invalid
            AzureDevOpsOrg = "org",
            Project = "project",
            Repo = "repo"
        };
        var options = Options.Create(config);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new ClaudeCliExecutor(options, _logger));
        Assert.Contains("MaxConcurrentProcesses must be between 1 and 10", exception.Message);
    }

    [Fact]
    public void Constructor_WithInvalidWebhookPort_ThrowsArgumentException()
    {
        // Arrange
        var config = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "/bin/echo",
            McpEndpoint = "https://secure.example.com",
            WebhookPort = 500, // Invalid (privileged port)
            AzureDevOpsOrg = "org",
            Project = "project",
            Repo = "repo"
        };
        var options = Options.Create(config);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new ClaudeCliExecutor(options, _logger));
        Assert.Contains("WebhookPort must be between 1024 and 65535", exception.Message);
    }

    [Fact]
    public void Constructor_WithSecureConfiguration_Succeeds()
    {
        // Arrange
        var config = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "/bin/echo",
            McpEndpoint = "https://secure.example.com",
            RequireHttps = true,
            MaxConcurrentProcesses = 1,
            WebhookPort = 8443,
            EnableAuditLogging = true,
            AzureDevOpsOrg = "org",
            Project = "project",
            Repo = "repo"
        };
        var options = Options.Create(config);

        // Act & Assert - Should not throw
        var executor = new ClaudeCliExecutor(options, _logger);
        Assert.NotNull(executor);
    }

    [Fact]
    public void Constructor_WithHttpsEndpointWhenHttpsNotRequired_Succeeds()
    {
        // Arrange
        var config = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "/bin/echo",
            McpEndpoint = "http://legacy.example.com", // HTTP allowed when not required
            RequireHttps = false,
            AzureDevOpsOrg = "org",
            Project = "project",
            Repo = "repo"
        };
        var options = Options.Create(config);

        // Act & Assert - Should not throw
        var executor = new ClaudeCliExecutor(options, _logger);
        Assert.NotNull(executor);
    }

    [Theory]
    [InlineData(11)] // Too high
    [InlineData(-1)] // Too low
    public void Constructor_WithInvalidMaxConcurrentProcessesRange_ThrowsArgumentException(int maxProcesses)
    {
        // Arrange
        var config = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "/bin/echo",
            McpEndpoint = "https://secure.example.com",
            MaxConcurrentProcesses = maxProcesses,
            AzureDevOpsOrg = "org",
            Project = "project",
            Repo = "repo"
        };
        var options = Options.Create(config);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new ClaudeCliExecutor(options, _logger));
        Assert.Contains("MaxConcurrentProcesses must be between 1 and 10", exception.Message);
    }

    [Theory]
    [InlineData(1023)]  // Too low
    [InlineData(65536)] // Too high
    public void Constructor_WithInvalidWebhookPortRange_ThrowsArgumentException(int port)
    {
        // Arrange
        var config = new ClaudeCliConfig
        {
            ClaudeCodeCliPath = "/bin/echo",
            McpEndpoint = "https://secure.example.com",
            WebhookPort = port,
            AzureDevOpsOrg = "org",
            Project = "project",
            Repo = "repo"
        };
        var options = Options.Create(config);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new ClaudeCliExecutor(options, _logger));
        Assert.Contains("WebhookPort must be between 1024 and 65535", exception.Message);
    }
}