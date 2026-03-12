using System.Net.Http.Headers;
using System.Text;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>
/// Handles external source-control integration for projects: token validation,
/// storing the connection, and reporting integration status.
/// </summary>
public class IntegrationService(
    IDbContextFactory<ApplicationDbContext> factory,
    IHttpClientFactory httpClientFactory,
    IConfiguration config)
{
    private string BoardBaseUrl => config["BoardBaseUrl"] ?? "http://localhost:5227";

    // ── Connect ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates <paramref name="token"/> against the provider inferred from the project's
    /// assigned team, then stores the token and sets <see cref="Project.IntegrationConnectedAt"/>.
    /// </summary>
    /// <returns>
    /// <c>(project, null)</c> on success; <c>(null, null)</c> if the project was not found;
    /// <c>(project, errorMessage)</c> if token validation fails.
    /// </returns>
    public async Task<(Project? project, string? error)> ConnectAsync(
        Guid projectId,
        string? token,
        string? repoUrl,
        string? externalProjectId)
    {
        using var db = await factory.CreateDbContextAsync();

        var project = await db.Projects.FindAsync(projectId);
        if (project is null) return (null, null);

        // Resolve team integration type via ProjectTeams → Teams
        var integrationType = await ResolveIntegrationTypeAsync(db, projectId);

        // Validate token if one is being provided
        if (!string.IsNullOrWhiteSpace(token))
        {
            var (valid, errorMessage) = await ValidateTokenAsync(integrationType, token, repoUrl ?? project.IntegrationRepoUrl);
            if (!valid)
                return (project, errorMessage);
        }

        // Persist fields
        if (token is not null) project.IntegrationToken = token;
        if (repoUrl is not null) project.IntegrationRepoUrl = repoUrl;
        if (externalProjectId is not null) project.ExternalProjectId = externalProjectId;
        project.IntegrationConnectedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (project, null);
    }

    // ── Status ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current integration status for a project.
    /// Returns <c>null</c> if the project was not found.
    /// </summary>
    public async Task<IntegrationStatusDto?> GetStatusAsync(Guid projectId)
    {
        using var db = await factory.CreateDbContextAsync();

        var project = await db.Projects.FindAsync(projectId);
        if (project is null) return null;

        var integrationType = await ResolveIntegrationTypeAsync(db, projectId);

        var webhookSuffix = integrationType == IntegrationType.AzureDevOps
            ? "azuredevops"
            : "github";

        var webhookUrl = $"{BoardBaseUrl.TrimEnd('/')}/api/projects/{projectId}/sync/{webhookSuffix}/webhook";

        return new IntegrationStatusDto(
            IsConnected: project.IntegrationConnectedAt is not null,
            IntegrationType: integrationType.ToString(),
            ConnectedAt: project.IntegrationConnectedAt,
            WebhookUrl: webhookUrl,
            RepoUrl: project.IntegrationRepoUrl);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Builds a safe project DTO, deliberately omitting <see cref="Project.IntegrationToken"/>.</summary>
    public static ProjectIntegrationDto ToSafeDto(Project project) => new(
        Id: project.Id,
        Name: project.Name,
        Description: project.Description,
        Goals: project.Goals,
        IntegrationRepoUrl: project.IntegrationRepoUrl,
        ExternalProjectId: project.ExternalProjectId,
        IntegrationConnectedAt: project.IntegrationConnectedAt,
        CreatedAt: project.CreatedAt,
        UpdatedAt: project.UpdatedAt);

    private static async Task<IntegrationType> ResolveIntegrationTypeAsync(
        ApplicationDbContext db, Guid projectId)
    {
        var teamId = await db.ProjectTeams
            .Where(pt => pt.ProjectId == projectId)
            .Select(pt => (Guid?)pt.TeamId)
            .FirstOrDefaultAsync();

        if (teamId is null) return IntegrationType.None;

        var team = await db.Teams.FindAsync(teamId.Value);
        return team?.IntegrationType ?? IntegrationType.None;
    }

    private async Task<(bool valid, string? error)> ValidateTokenAsync(
        IntegrationType integrationType, string token, string? repoUrl)
    {
        var client = httpClientFactory.CreateClient("IntegrationValidator");

        switch (integrationType)
        {
            case IntegrationType.GitHub:
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
                request.Headers.Authorization = new AuthenticationHeaderValue("token", token);
                request.Headers.UserAgent.ParseAdd("AgentBoard/1.0");

                using var response = await client.SendAsync(request);
                return response.IsSuccessStatusCode
                    ? (true, null)
                    : (false, "GitHub token validation failed");
            }

            case IntegrationType.AzureDevOps when !string.IsNullOrWhiteSpace(repoUrl):
            {
                var url = $"{repoUrl.TrimEnd('/')}/_apis/projects?api-version=7.0";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Basic auth: empty username, token as password
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                using var response = await client.SendAsync(request);
                return response.IsSuccessStatusCode
                    ? (true, null)
                    : (false, "Azure DevOps token validation failed");
            }

            default:
                // IntegrationType.None or AzureDevOps with no repoUrl → skip validation
                return (true, null);
        }
    }
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

/// <summary>Safe project representation that never includes <c>IntegrationToken</c>.</summary>
public sealed record ProjectIntegrationDto(
    Guid Id,
    string Name,
    string? Description,
    string? Goals,
    string? IntegrationRepoUrl,
    string? ExternalProjectId,
    DateTime? IntegrationConnectedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Response shape for <c>GET /api/projects/{id}/integration/status</c>.</summary>
public sealed record IntegrationStatusDto(
    bool IsConnected,
    string IntegrationType,
    DateTime? ConnectedAt,
    string WebhookUrl,
    string? RepoUrl);
