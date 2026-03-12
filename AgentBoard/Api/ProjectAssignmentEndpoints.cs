using AgentBoard.Services;

namespace AgentBoard.Api;

/// <summary>
/// REST endpoints for assigning agents and teams to projects.
/// Routes are nested under <c>/api/projects/{id}/agents</c> and <c>/api/projects/{id}/teams</c>.
/// </summary>
public static class ProjectAssignmentEndpoints
{
    /// <summary>Maps all project-assignment routes onto the application.</summary>
    public static void MapProjectAssignmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/projects/{id:guid}").WithTags("project-assignments");

        // GET /api/projects/{id}/agents
        // Returns all agent IDs assigned to this project (direct + via team), with source info.
        group.MapGet("/agents", async (Guid id, ProjectAssignmentService svc) =>
        {
            var entries = await svc.GetProjectAgentEntriesAsync(id);
            return Results.Ok(entries);
        });

        // POST /api/projects/{id}/agents/{agentId}
        // Directly assigns an agent to the project. Idempotent.
        group.MapPost("/agents/{agentId:guid}", async (Guid id, Guid agentId, ProjectAssignmentService svc) =>
        {
            var assigned = await svc.AssignAgentAsync(id, agentId);
            return assigned ? Results.Ok() : Results.NotFound();
        });

        // DELETE /api/projects/{id}/agents/{agentId}
        // Removes a direct agent assignment from the project.
        group.MapDelete("/agents/{agentId:guid}", async (Guid id, Guid agentId, ProjectAssignmentService svc) =>
        {
            var removed = await svc.UnassignAgentAsync(id, agentId);
            return removed ? Results.NoContent() : Results.NotFound();
        });

        // GET /api/projects/{id}/teams
        // Returns all teams assigned to this project with their assignment timestamps.
        group.MapGet("/teams", async (Guid id, ProjectAssignmentService svc) =>
        {
            var entries = await svc.GetProjectTeamEntriesAsync(id);
            return Results.Ok(entries);
        });

        // POST /api/projects/{id}/teams/{teamId}
        // Assigns a team to the project. Idempotent.
        group.MapPost("/teams/{teamId:guid}", async (Guid id, Guid teamId, ProjectAssignmentService svc) =>
        {
            var assigned = await svc.AssignTeamAsync(id, teamId);
            return assigned ? Results.Ok() : Results.NotFound();
        });

        // DELETE /api/projects/{id}/teams/{teamId}
        // Removes a team assignment from the project.
        group.MapDelete("/teams/{teamId:guid}", async (Guid id, Guid teamId, ProjectAssignmentService svc) =>
        {
            var removed = await svc.UnassignTeamAsync(id, teamId);
            return removed ? Results.NoContent() : Results.NotFound();
        });
    }
}
