using AgentBoard.Data.Models;

namespace AgentBoard.Contracts;

/// <summary>Partial-update payload for an <see cref="Agent"/>. Null fields are ignored.</summary>
public record AgentPatch(
    string? Name,
    string? Description,
    AgentType? Type,
    bool? IsAvailable);