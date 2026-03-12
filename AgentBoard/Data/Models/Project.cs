using System.ComponentModel.DataAnnotations;

namespace AgentBoard.Data.Models;

public class Project
{
    public Guid Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Goals { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Optional integration token (e.g. PAT for GitHub/Azure DevOps). Stored encrypted in production via Key Vault.</summary>
    public string? IntegrationToken { get; set; }

    /// <summary>Optional repository URL for integration.</summary>
    public string? IntegrationRepoUrl { get; set; }

    /// <summary>Optional external project ID (e.g. GitHub repo full name or Azure DevOps project ID). Max 500 characters.</summary>
    public string? ExternalProjectId { get; set; }
}
