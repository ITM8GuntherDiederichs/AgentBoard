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
    public int? ExternalIssueNumber { get; set; }

    [MaxLength(50)]
    public string? ExternalSystem { get; set; }  // "github" | "azuredevops" | null

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}