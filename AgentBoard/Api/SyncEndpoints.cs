using AgentBoard.Data.Models;
using AgentBoard.Services;

namespace AgentBoard.Api;

/// <summary>Minimal API endpoints for GitHub sync operations.</summary>
public static class SyncEndpoints
{
    /// <summary>Registers sync-related API endpoints on the application.</summary>
    public static void MapSyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/projects/{id:guid}/sync").WithTags("sync");

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
    }
}
