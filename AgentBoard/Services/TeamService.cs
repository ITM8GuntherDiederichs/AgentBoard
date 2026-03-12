using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>Service for managing <see cref="Team"/> records and their members.</summary>
public class TeamService(IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Returns all teams, including their members, ordered by name.</summary>
    public async Task<List<Team>> GetAllAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Teams
            .Include(t => t.Members)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    /// <summary>Returns the team with the specified <paramref name="id"/> (including members), or <c>null</c> if not found.</summary>
    public async Task<Team?> GetByIdAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>
    /// Creates a new team. Sets <see cref="Team.Id"/>, <see cref="Team.CreatedAt"/>, and <see cref="Team.UpdatedAt"/>.
    /// </summary>
    public async Task<Team> CreateAsync(Team team)
    {
        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        team.Id = Guid.NewGuid();
        team.CreatedAt = now;
        team.UpdatedAt = now;
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team;
    }

    /// <summary>
    /// Updates the <see cref="Team.Name"/> and <see cref="Team.Description"/> of an existing team.
    /// Sets <see cref="Team.UpdatedAt"/>. Returns <c>null</c> if not found.
    /// </summary>
    public async Task<Team?> UpdateAsync(Team team)
    {
        using var db = await factory.CreateDbContextAsync();
        var existing = await db.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == team.Id);
        if (existing is null) return null;

        existing.Name = team.Name;
        existing.Description = team.Description;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Deletes the team with the specified <paramref name="id"/> (cascades members).
    /// Returns <c>true</c> if deleted; <c>false</c> if not found.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        var team = await db.Teams.FindAsync(id);
        if (team is null) return false;
        db.Teams.Remove(team);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Adds an agent to a team. Idempotent — does nothing if the member already exists.
    /// Returns <c>true</c> if successful; <c>false</c> if the team was not found.
    /// </summary>
    public async Task<bool> AddMemberAsync(Guid teamId, Guid agentId)
    {
        using var db = await factory.CreateDbContextAsync();
        var teamExists = await db.Teams.AnyAsync(t => t.Id == teamId);
        if (!teamExists) return false;

        var alreadyMember = await db.TeamMembers
            .AnyAsync(m => m.TeamId == teamId && m.AgentId == agentId);
        if (alreadyMember) return true;

        db.TeamMembers.Add(new TeamMember
        {
            TeamId = teamId,
            AgentId = agentId,
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Removes an agent from a team.
    /// Returns <c>true</c> if the member was removed; <c>false</c> if the team or member was not found.
    /// </summary>
    public async Task<bool> RemoveMemberAsync(Guid teamId, Guid agentId)
    {
        using var db = await factory.CreateDbContextAsync();
        var member = await db.TeamMembers
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.AgentId == agentId);
        if (member is null) return false;

        db.TeamMembers.Remove(member);
        await db.SaveChangesAsync();
        return true;
    }
}
