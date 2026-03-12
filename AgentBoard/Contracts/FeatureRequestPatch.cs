using AgentBoard.Data.Models;

namespace AgentBoard.Contracts;

/// <summary>Patch payload for partial updates to a <see cref="FeatureRequest"/>.</summary>
public record FeatureRequestPatch(
    string? Title,
    string? Description,
    TodoPriority? Priority,
    FeatureRequestStatus? Status);
