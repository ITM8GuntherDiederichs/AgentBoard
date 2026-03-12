using AgentBoard.Data.Models;

namespace AgentBoard.Contracts;

/// <summary>Partial-update payload for a <see cref="Team"/>. Null fields are ignored.</summary>
public record TeamPatch(
    string? Name,
    string? Description,
    string? Instructions = null,
    bool ClearInstructions = false);
