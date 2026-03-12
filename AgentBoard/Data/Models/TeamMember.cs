using System.Text.Json.Serialization;

namespace AgentBoard.Data.Models;

/// <summary>
/// Join entity that links an <see cref="Agent"/> (by ID only — no FK constraint) to a <see cref="Team"/>.
/// </summary>
public class TeamMember
{
    /// <summary>Foreign key to <see cref="Team"/>.</summary>
    public Guid TeamId { get; set; }

    /// <summary>
    /// The agent's identifier. Stored as a plain <see cref="Guid"/> with no FK constraint to the Agents table —
    /// same pattern as <c>FeatureRequest.ProjectId</c>. The constraint can be added once both entities are on main.
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>UTC timestamp when this member was added to the team.</summary>
    public DateTime AddedAt { get; set; }

    /// <summary>Navigation property to the parent team. Excluded from JSON serialization to avoid circular references.</summary>
    [JsonIgnore]
    public Team Team { get; set; } = null!;
}