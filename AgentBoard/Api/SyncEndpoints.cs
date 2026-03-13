using AgentBoard.Data.Models;
using AgentBoard.Services;

namespace AgentBoard.Api;

/// <summary>Minimal API endpoints for GitHub and Azure DevOps sync operations.</summary>
public static class SyncEndpoints
{
    /// <summary>Registers sync-related API endpoints on the application.</summary>
    public static void MapSyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/projects/{id:guid}/sync").WithTags("sync");

        // ── GitHub ──────────────────────────────────────────────────────────

        /// <summary>Syncs all todos and feature requests for the project to GitHub Issues.</summary>
        group.MapPost("/github", async (Guid id, GitHubSyncService svc) =>
        {
            var result = await svc.SyncToGitHubAsync(id);
            return Results.Ok(result);
        });

        /// <summary>Handles an incoming GitHub webhook event for the project.</summary>
        group.MapPost("/github/webhook", async (Guid id, GitHubWebhookPayload payload, GitHubSyncService svc) =>
        {
            await svc.HandleWebhookAsync(id, payload);
            return Results.Ok();
        });

        // ── Azure DevOps ────────────────────────────────────────────────────

        /// <summary>Syncs all todos and feature requests for the project to Azure DevOps Work Items.</summary>
        group.MapPost("/azuredevops", async (Guid id, AzureDevOpsSyncService svc) =>
        {
            var result = await svc.SyncToAzureDevOpsAsync(id);
            return result.Failed > 0 && result.Created == 0 && result.Updated == 0
                ? Results.NotFound(result)
                : Results.Ok(result);
        });

        /// <summary>Handles an incoming Azure DevOps webhook event for the project.</summary>
        group.MapPost("/azuredevops/webhook", async (Guid id, AzureDevOpsWebhookPayload payload, AzureDevOpsSyncService svc) =>
        {
            await svc.HandleWebhookAsync(id, payload);
            return Results.Ok();
        });
    }
}
