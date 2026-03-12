using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Services;

namespace AgentBoard.Api;

/// <summary>REST endpoints for managing agents.</summary>
public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/agents").WithTags("agents");

        // GET /api/agents?availableOnly=true
        group.MapGet("/", async (bool? availableOnly, AgentService svc) =>
            Results.Ok(await svc.GetAllAsync(availableOnly)));

        // POST /api/agents
        group.MapPost("/", async (Agent agent, AgentService svc) =>
        {
            var created = await svc.CreateAsync(agent);
            return Results.Created($"/api/agents/{created.Id}", created);
        });

        // GET /api/agents/{id}
        group.MapGet("/{id:guid}", async (Guid id, AgentService svc) =>
        {
            var agent = await svc.GetByIdAsync(id);
            return agent is null ? Results.NotFound() : Results.Ok(agent);
        });

        // PATCH /api/agents/{id}
        group.MapPatch("/{id:guid}", async (Guid id, AgentPatch patch, AgentService svc) =>
        {
            var updated = await svc.PatchAsync(id, patch);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        // DELETE /api/agents/{id}
        group.MapDelete("/{id:guid}", async (Guid id, AgentService svc) =>
        {
            var deleted = await svc.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }
}