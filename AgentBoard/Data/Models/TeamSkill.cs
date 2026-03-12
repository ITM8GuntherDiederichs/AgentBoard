namespace AgentBoard.Data.Models;

/// <summary>
/// Join entity that assigns a skill (by ID only — no FK constraint) to a team.
/// Follows the loose-coupling pattern used by <see cref="ProjectAgent"/> and <see cref="ProjectTeam"/>.
/// </summary>
public class TeamSkill
{
    /// <summary>Team identifier. Plain <see cref="Guid"/> — no FK constraint to the Teams table.</summary>
    public Guid TeamId { get; set; }

    /// <summary>Skill identifier. Plain <see cref="Guid"/> — no FK constraint to the Skills table.</summary>
    public Guid SkillId { get; set; }
}
