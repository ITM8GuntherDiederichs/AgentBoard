namespace AgentBoard.Data.Models;

/// <summary>
/// Join entity that assigns a skill (by ID only — no FK constraint) to an agent.
/// Follows the loose-coupling pattern used by <see cref="ProjectAgent"/> and <see cref="ProjectTeam"/>.
/// </summary>
public class AgentSkill
{
    /// <summary>Agent identifier. Plain <see cref="Guid"/> — no FK constraint to the Agents table.</summary>
    public Guid AgentId { get; set; }

    /// <summary>Skill identifier. Plain <see cref="Guid"/> — no FK constraint to the Skills table.</summary>
    public Guid SkillId { get; set; }
}
