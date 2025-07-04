namespace ClaudeCLIRunner.Configuration;

public class ClaudeCliConfig
{
    public string ClaudeCodeCliPath { get; set; } = string.Empty;
    public string McpEndpoint { get; set; } = string.Empty;
    public string AzureDevOpsOrg { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public int PollIntervalSeconds { get; set; } = 60;
    public int MaxRetries { get; set; } = 3;
    public int MaxTaskDurationMinutes { get; set; } = 60;
    
    // Additional settings
    public string? AzureDevOpsPat { get; set; }
    public bool UseWebhook { get; set; } = false;
    public int WebhookPort { get; set; } = 5000;
    public string LogLevel { get; set; } = "Information";
} 