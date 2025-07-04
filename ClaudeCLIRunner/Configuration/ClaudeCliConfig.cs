using System.ComponentModel.DataAnnotations;

namespace ClaudeCLIRunner.Configuration;

public class ClaudeCliConfig
{
    [Required]
    public string ClaudeCodeCliPath { get; set; } = string.Empty;
    
    [Required]
    [Url]
    public string McpEndpoint { get; set; } = string.Empty;
    
    [Required]
    public string AzureDevOpsOrg { get; set; } = string.Empty;
    
    [Required]
    public string Project { get; set; } = string.Empty;
    
    [Required]
    public string Repo { get; set; } = string.Empty;
    
    public string DefaultBranch { get; set; } = "main";
    
    [Range(10, 3600)]
    public int PollIntervalSeconds { get; set; } = 60;
    
    [Range(1, 10)]
    public int MaxRetries { get; set; } = 3;
    
    [Range(1, 1440)] // Max 24 hours
    public int MaxTaskDurationMinutes { get; set; } = 60;
    
    // Security: PAT should not be stored in config files - use environment variables or secure storage
    [Obsolete("For security reasons, use environment variable CLAUDECLI_AZURE_DEVOPS_PAT or secure credential storage instead")]
    public string? AzureDevOpsPat { get; set; }
    
    public bool UseWebhook { get; set; } = false;
    
    [Range(1024, 65535)] // Valid port range, avoid privileged ports
    public int WebhookPort { get; set; } = 5000;
    
    public string LogLevel { get; set; } = "Information";
    
    // Security: Additional security settings
    public bool RequireHttps { get; set; } = true;
    public int MaxConcurrentProcesses { get; set; } = 1;
    public bool EnableAuditLogging { get; set; } = true;
    public string? AuditLogPath { get; set; }
} 