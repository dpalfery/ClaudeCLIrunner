namespace ClaudeCLIRunner.Models;

public class WorkItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string AreaPath { get; set; } = string.Empty;
    public string IterationPath { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; }
    public DateTime? ChangedDate { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> Fields { get; set; } = new();
    
    public bool HasClaudeTag => Tags.Any(t => t.Equals("@Claude", StringComparison.OrdinalIgnoreCase));
} 