using AgentBoard.Contracts;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>Service for managing <see cref="Skill"/> records and their assignments to agents/teams.</summary>
public class SkillService(IDbContextFactory<ApplicationDbContext> factory)
{
    // ── CRUD ─────────────────────────────────────────────────────────────────

    /// <summary>Returns all skills ordered by name.</summary>
    public async Task<List<Skill>> GetAllAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Skills.OrderBy(s => s.Name).ToListAsync();
    }

    /// <summary>Returns the skill with the specified <paramref name="id"/>, or <c>null</c> if not found.</summary>
    public async Task<Skill?> GetByIdAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Skills.FindAsync(id);
    }

    /// <summary>Persists a new skill. Sets <see cref="Skill.Id"/>, <see cref="Skill.CreatedAt"/> and <see cref="Skill.UpdatedAt"/>.</summary>
    public async Task<Skill> CreateAsync(Skill skill)
    {
        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        skill.Id = Guid.NewGuid();
        skill.CreatedAt = now;
        skill.UpdatedAt = now;
        db.Skills.Add(skill);
        await db.SaveChangesAsync();
        return skill;
    }

    /// <summary>Applies a partial update to the skill identified by <paramref name="id"/>.</summary>
    /// <returns>The updated skill, or <c>null</c> if not found.</returns>
    public async Task<Skill?> UpdateAsync(Guid id, SkillPatch patch)
    {
        using var db = await factory.CreateDbContextAsync();
        var skill = await db.Skills.FindAsync(id);
        if (skill is null) return null;

        if (patch.Name is not null) skill.Name = patch.Name;
        if (patch.Content is not null) skill.Content = patch.Content;
        skill.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return skill;
    }

    /// <summary>Deletes the skill with the specified <paramref name="id"/>.</summary>
    /// <returns><c>true</c> if deleted; <c>false</c> if not found.</returns>
    public async Task<bool> DeleteAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        var skill = await db.Skills.FindAsync(id);
        if (skill is null) return false;
        db.Skills.Remove(skill);
        await db.SaveChangesAsync();
        return true;
    }

    // ── Agent skills ─────────────────────────────────────────────────────────

    /// <summary>Returns the skills assigned to the specified agent.</summary>
    public async Task<List<Skill>> GetAgentSkillsAsync(Guid agentId)
    {
        using var db = await factory.CreateDbContextAsync();
        var skillIds = await db.AgentSkills
            .Where(a => a.AgentId == agentId)
            .Select(a => a.SkillId)
            .ToListAsync();
        return await db.Skills
            .Where(s => skillIds.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Assigns a skill to an agent. Idempotent — returns <c>true</c> if the assignment already exists.
    /// Returns <c>false</c> if the skill does not exist.
    /// </summary>
    public async Task<bool> AddSkillToAgentAsync(Guid agentId, Guid skillId)
    {
        using var db = await factory.CreateDbContextAsync();
        var skillExists = await db.Skills.AnyAsync(s => s.Id == skillId);
        if (!skillExists) return false;

        var alreadyAssigned = await db.AgentSkills
            .AnyAsync(a => a.AgentId == agentId && a.SkillId == skillId);
        if (alreadyAssigned) return true;

        db.AgentSkills.Add(new AgentSkill { AgentId = agentId, SkillId = skillId });
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Removes the skill assignment from an agent.</summary>
    /// <returns><c>true</c> if removed; <c>false</c> if the assignment was not found.</returns>
    public async Task<bool> RemoveSkillFromAgentAsync(Guid agentId, Guid skillId)
    {
        using var db = await factory.CreateDbContextAsync();
        var assignment = await db.AgentSkills
            .FirstOrDefaultAsync(a => a.AgentId == agentId && a.SkillId == skillId);
        if (assignment is null) return false;

        db.AgentSkills.Remove(assignment);
        await db.SaveChangesAsync();
        return true;
    }

    // ── Team skills ───────────────────────────────────────────────────────────

    /// <summary>Returns the skills assigned to the specified team.</summary>
    public async Task<List<Skill>> GetTeamSkillsAsync(Guid teamId)
    {
        using var db = await factory.CreateDbContextAsync();
        var skillIds = await db.TeamSkills
            .Where(ts => ts.TeamId == teamId)
            .Select(ts => ts.SkillId)
            .ToListAsync();
        return await db.Skills
            .Where(s => skillIds.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Assigns a skill to a team. Idempotent — returns <c>true</c> if the assignment already exists.
    /// Returns <c>false</c> if the skill does not exist.
    /// </summary>
    public async Task<bool> AddSkillToTeamAsync(Guid teamId, Guid skillId)
    {
        using var db = await factory.CreateDbContextAsync();
        var skillExists = await db.Skills.AnyAsync(s => s.Id == skillId);
        if (!skillExists) return false;

        var alreadyAssigned = await db.TeamSkills
            .AnyAsync(ts => ts.TeamId == teamId && ts.SkillId == skillId);
        if (alreadyAssigned) return true;

        db.TeamSkills.Add(new TeamSkill { TeamId = teamId, SkillId = skillId });
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Removes the skill assignment from a team.</summary>
    /// <returns><c>true</c> if removed; <c>false</c> if the assignment was not found.</returns>
    public async Task<bool> RemoveSkillFromTeamAsync(Guid teamId, Guid skillId)
    {
        using var db = await factory.CreateDbContextAsync();
        var assignment = await db.TeamSkills
            .FirstOrDefaultAsync(ts => ts.TeamId == teamId && ts.SkillId == skillId);
        if (assignment is null) return false;

        db.TeamSkills.Remove(assignment);
        await db.SaveChangesAsync();
        return true;
    }
}
