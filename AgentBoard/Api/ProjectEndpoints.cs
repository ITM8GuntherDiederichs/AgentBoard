using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Services;
using Microsoft.Extensions.Configuration;

namespace AgentBoard.Api;

/// <summary>Minimal API endpoints for <see cref="Project"/> CRUD under <c>/api/projects</c>.</summary>
public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/projects").WithTags("projects");

        // GET /api/projects
        group.MapGet("/", async (ProjectService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        // POST /api/projects
        group.MapPost("/", async (Project project, ProjectService svc) =>
        {
            var created = await svc.CreateAsync(project);
            return Results.Created($"/api/projects/{created.Id}", created);
        });

        // GET /api/projects/{id}
        group.MapGet("/{id:guid}", async (Guid id, ProjectService svc) =>
        {
            var project = await svc.GetByIdAsync(id);
            return project is null ? Results.NotFound() : Results.Ok(project);
        });

        // PUT /api/projects/{id}
        group.MapPut("/{id:guid}", async (Guid id, Project project, ProjectService svc) =>
        {
            project.Id = id;
            var updated = await svc.UpdateAsync(project);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        // DELETE /api/projects/{id}
        group.MapDelete("/{id:guid}", async (Guid id, ProjectService svc) =>
        {
            var deleted = await svc.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // POST /api/projects/{id}/integration
        group.MapPost("/{id:guid}/integration", async (Guid id, IntegrationRequest body, ProjectService svc) =>
        {
            var patch = new ProjectPatch(
                IntegrationToken: body.IntegrationToken,
                IntegrationRepoUrl: body.IntegrationRepoUrl,
                ExternalProjectId: body.ExternalProjectId);
            var updated = await svc.PatchAsync(id, patch);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        // GET /api/projects/{id}/deploy
        group.MapGet("/{id:guid}/deploy", async (Guid id, DeployService deploySvc, IConfiguration config) =>
        {
            var boardBaseUrl = config["BoardBaseUrl"] ?? "localhost:5227";
            var zipBytes = await deploySvc.GenerateDeployZipAsync(id, boardBaseUrl);
            if (zipBytes is null) return Results.NotFound();

            // We need the project name for the filename; re-use what's in the zip
            // The zip already embeds project info — return a fixed filename pattern
            return Results.File(zipBytes, "application/zip", $"project-{id}-deploy.zip");
        });
    }
}

/// <summary>Request body for <c>POST /api/projects/{id}/integration</c>.</summary>
public sealed record IntegrationRequest(
    string? IntegrationToken,
    string? IntegrationRepoUrl,
    string? ExternalProjectId);
