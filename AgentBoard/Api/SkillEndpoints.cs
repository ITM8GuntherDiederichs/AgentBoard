using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Services;

namespace AgentBoard.Api;

/// <summary>REST endpoints for managing skills and their assignments to agents and teams.</summary>
public static class SkillEndpoints
{
    public static void MapSkillEndpoints(this WebApplication app)
    {
        // ── Skill CRUD ────────────────────────────────────────────────────────

        var skills = app.MapGroup("/api/skills").WithTags("skills");

        // GET /api/skills
        skills.MapGet("/", async (SkillService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        // GET /api/skills/{id}
        skills.MapGet("/{id:guid}", async (Guid id, SkillService svc) =>
        {
            var skill = await svc.GetByIdAsync(id);
            return skill is null ? Results.NotFound() : Results.Ok(skill);
        });

        // POST /api/skills
        skills.MapPost("/", async (Skill skill, SkillService svc) =>
        {
            var created = await svc.CreateAsync(skill);
            return Results.Created($"/api/skills/{created.Id}", created);
        });

        // PATCH /api/skills/{id}
        skills.MapPatch("/{id:guid}", async (Guid id, SkillPatch patch, SkillService svc) =>
        {
            var updated = await svc.UpdateAsync(id, patch);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        // DELETE /api/skills/{id}
        skills.MapDelete("/{id:guid}", async (Guid id, SkillService svc) =>
        {
            var deleted = await svc.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // ── Agent skills ──────────────────────────────────────────────────────

        var agents = app.MapGroup("/api/agents").WithTags("agents");

        // GET /api/agents/{id}/skills
        agents.MapGet("/{id:guid}/skills", async (Guid id, SkillService svc) =>
            Results.Ok(await svc.GetAgentSkillsAsync(id)));

        // POST /api/agents/{id}/skills   body: { "skillId": "guid" }
        agents.MapPost("/{id:guid}/skills", async (Guid id, SkillAssignRequest req, SkillService svc) =>
        {
            var added = await svc.AddSkillToAgentAsync(id, req.SkillId);
            return added ? Results.Ok() : Results.NotFound();
        });

        // DELETE /api/agents/{id}/skills/{skillId}
        agents.MapDelete("/{id:guid}/skills/{skillId:guid}", async (Guid id, Guid skillId, SkillService svc) =>
        {
            var removed = await svc.RemoveSkillFromAgentAsync(id, skillId);
            return removed ? Results.NoContent() : Results.NotFound();
        });

        // ── Team skills ───────────────────────────────────────────────────────

        var teams = app.MapGroup("/api/teams").WithTags("teams");

        // GET /api/teams/{id}/skills
        teams.MapGet("/{id:guid}/skills", async (Guid id, SkillService svc) =>
            Results.Ok(await svc.GetTeamSkillsAsync(id)));

        // POST /api/teams/{id}/skills   body: { "skillId": "guid" }
        teams.MapPost("/{id:guid}/skills", async (Guid id, SkillAssignRequest req, SkillService svc) =>
        {
            var added = await svc.AddSkillToTeamAsync(id, req.SkillId);
            return added ? Results.Ok() : Results.NotFound();
        });

        // DELETE /api/teams/{id}/skills/{skillId}
        teams.MapDelete("/{id:guid}/skills/{skillId:guid}", async (Guid id, Guid skillId, SkillService svc) =>
        {
            var removed = await svc.RemoveSkillFromTeamAsync(id, skillId);
            return removed ? Results.NoContent() : Results.NotFound();
        });
    }
}

/// <summary>Request body for assigning a skill to an agent or team.</summary>
public sealed record SkillAssignRequest(Guid SkillId);
