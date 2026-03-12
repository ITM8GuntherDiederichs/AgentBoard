using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Services;

namespace AgentBoard.Api;

/// <summary>REST endpoints for managing teams and team membership.</summary>
public static class TeamEndpoints
{
    /// <summary>Maps all team-related routes onto the application.</summary>
    public static void MapTeamEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/teams").WithTags("teams");

        // GET /api/teams
        group.MapGet("/", async (TeamService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        // POST /api/teams
        group.MapPost("/", async (Team team, TeamService svc) =>
        {
            var created = await svc.CreateAsync(team);
            return Results.Created($"/api/teams/{created.Id}", created);
        });

        // GET /api/teams/{id}
        group.MapGet("/{id:guid}", async (Guid id, TeamService svc) =>
        {
            var team = await svc.GetByIdAsync(id);
            return team is null ? Results.NotFound() : Results.Ok(team);
        });

        // PUT /api/teams/{id}  — updates Name + Description only
        group.MapPut("/{id:guid}", async (Guid id, Team team, TeamService svc) =>
        {
            team.Id = id;
            var updated = await svc.UpdateAsync(team);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        // PATCH /api/teams/{id}
        group.MapPatch("/{id:guid}", async (Guid id, TeamPatch patch, TeamService svc) =>
        {
            var updated = await svc.PatchAsync(id, patch);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        // DELETE /api/teams/{id}
        group.MapDelete("/{id:guid}", async (Guid id, TeamService svc) =>
        {
            var deleted = await svc.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // POST /api/teams/{id}/members/{agentId}
        group.MapPost("/{id:guid}/members/{agentId:guid}", async (Guid id, Guid agentId, TeamService svc) =>
        {
            var added = await svc.AddMemberAsync(id, agentId);
            return added ? Results.Ok() : Results.NotFound();
        });

        // DELETE /api/teams/{id}/members/{agentId}
        group.MapDelete("/{id:guid}/members/{agentId:guid}", async (Guid id, Guid agentId, TeamService svc) =>
        {
            var removed = await svc.RemoveMemberAsync(id, agentId);
            return removed ? Results.NoContent() : Results.NotFound();
        });
    }
}
