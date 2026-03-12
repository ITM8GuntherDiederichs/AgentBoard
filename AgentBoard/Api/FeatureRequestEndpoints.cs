using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Services;

namespace AgentBoard.Api;

/// <summary>REST endpoints for the feature request backlog under a project.</summary>
public static class FeatureRequestEndpoints
{
    public static void MapFeatureRequestEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/projects/{projectId:guid}/features")
                       .WithTags("feature-requests");

        // GET /api/projects/{projectId}/features
        group.MapGet("/", async (Guid projectId, FeatureRequestService svc) =>
            Results.Ok(await svc.GetByProjectAsync(projectId)));

        // POST /api/projects/{projectId}/features
        group.MapPost("/", async (Guid projectId, FeatureRequest request, FeatureRequestService svc) =>
        {
            request.ProjectId = projectId;
            var created = await svc.CreateAsync(request);
            return Results.Created($"/api/projects/{projectId}/features/{created.Id}", created);
        });

        // PATCH /api/projects/{projectId}/features/{id}
        group.MapPatch("/{id:guid}", async (Guid projectId, Guid id, FeatureRequestPatch patch, FeatureRequestService svc) =>
        {
            var updated = await svc.PatchAsync(id, patch);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        // DELETE /api/projects/{projectId}/features/{id}
        group.MapDelete("/{id:guid}", async (Guid projectId, Guid id, FeatureRequestService svc) =>
        {
            var deleted = await svc.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }
}
