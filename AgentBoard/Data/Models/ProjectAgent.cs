namespace AgentBoard.Data.Models;

/// <summary>
/// Join entity that assigns an agent (by ID only — no FK constraint) directly to a <see cref="Project"/>.
/// </summary>
public class ProjectAgent
{
    /// <summary>Foreign key to <see cref="Project"/>.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// The agent's identifier. Stored as a plain <see cref="Guid"/> with no FK constraint to the Agents table —
    /// loose-coupling pattern used throughout this codebase.
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>UTC timestamp when this agent was assigned to the project.</summary>
    public DateTime AssignedAt { get; set; }
}
