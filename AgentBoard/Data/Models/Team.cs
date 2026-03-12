namespace AgentBoard.Data.Models;

/// <summary>Represents a named group of agents.</summary>
public class Team
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name of the team. Required, max 200 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description of the team's purpose.</summary>
    public string? Description { get; set; }

    /// <summary>UTC timestamp when this team was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp when this team was last updated.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Optional system prompt / instructions for this team (markdown).</summary>
    public string? Instructions { get; set; }

    /// <summary>External source control / CI integration type for this team.</summary>
    public IntegrationType IntegrationType { get; set; } = IntegrationType.None;

    /// <summary>Optional repository URL associated with this team's integration.</summary>
    public string? RepoUrl { get; set; }

    /// <summary>Members of this team.</summary>
    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}
