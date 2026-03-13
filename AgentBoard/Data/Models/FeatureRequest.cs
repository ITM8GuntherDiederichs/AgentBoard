using System.ComponentModel.DataAnnotations;

namespace AgentBoard.Data.Models;

public class FeatureRequest
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TodoPriority Priority { get; set; }
    public FeatureRequestStatus Status { get; set; }

    /// <summary>External issue number for GitHub/Azure DevOps integration tracking.</summary>
    public int? ExternalIssueNumber { get; set; }

    /// <summary>Identifies the external system for this item (e.g. "github" | "azuredevops").</summary>
    [MaxLength(50)]
    public string? ExternalSystem { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
