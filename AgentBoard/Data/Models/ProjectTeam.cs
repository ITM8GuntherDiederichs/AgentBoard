namespace AgentBoard.Data.Models;

/// <summary>
/// Join entity that assigns a team (by ID only — no FK constraint) to a <see cref="Project"/>.
/// </summary>
public class ProjectTeam
{
    /// <summary>Foreign key to <see cref="Project"/>.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// The team's identifier. Stored as a plain <see cref="Guid"/> with no FK constraint to the Teams table —
    /// loose-coupling pattern used throughout this codebase.
    /// </summary>
    public Guid TeamId { get; set; }

    /// <summary>UTC timestamp when this team was assigned to the project.</summary>
    public DateTime AssignedAt { get; set; }
}
