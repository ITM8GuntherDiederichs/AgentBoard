using AgentBoard.Data.Models;
using AgentBoard.Services;

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
    }
}
