namespace AgentBoard.Contracts;

/// <summary>Partial-update payload for a <see cref="AgentBoard.Data.Models.Skill"/>. Null fields are ignored.</summary>
public record SkillPatch(string? Name, string? Content);
