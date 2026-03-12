using AgentBoard.Data.Models;

namespace AgentBoard.Contracts;

/// <summary>Partial-update payload for a <see cref="Project"/>. Null fields are ignored.</summary>
public record ProjectPatch(
    string? Name = null,
    string? Description = null,
    string? Goals = null,
    string? IntegrationToken = null,
    bool ClearIntegrationToken = false,
    string? IntegrationRepoUrl = null,
    string? ExternalProjectId = null);
