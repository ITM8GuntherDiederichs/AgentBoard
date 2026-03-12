using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>
/// Service for managing direct agent and team assignments on projects.
/// Uses loose coupling — no FK constraints are enforced at the DB level.
/// </summary>
public class ProjectAssignmentService(IDbContextFactory<ApplicationDbContext> factory)
{
    // ── Agents ────────────────────────────────────────────────────────────────

    /// <summary>Returns the IDs of agents directly assigned to the specified project.</summary>
    public async Task<List<Guid>> GetProjectAgentsAsync(Guid projectId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ProjectAgents
            .Where(pa => pa.ProjectId == projectId)
            .Select(pa => pa.AgentId)
            .ToListAsync();
    }

    /// <summary>Returns the IDs of teams assigned to the specified project.</summary>
    public async Task<List<Guid>> GetProjectTeamsAsync(Guid projectId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ProjectTeams
            .Where(pt => pt.ProjectId == projectId)
            .Select(pt => pt.TeamId)
            .ToListAsync();
    }

    /// <summary>
    /// Returns a deduplicated union of all agent IDs for the project:
    /// directly assigned agents plus all members of assigned teams.
    /// </summary>
    public async Task<List<Guid>> GetAllProjectAgentIdsAsync(Guid projectId)
    {
        using var db = await factory.CreateDbContextAsync();

        var directAgents = await db.ProjectAgents
            .Where(pa => pa.ProjectId == projectId)
            .Select(pa => pa.AgentId)
            .ToListAsync();

        var teamIds = await db.ProjectTeams
            .Where(pt => pt.ProjectId == projectId)
            .Select(pt => pt.TeamId)
            .ToListAsync();

        var teamMemberAgents = await db.TeamMembers
            .Where(tm => teamIds.Contains(tm.TeamId))
            .Select(tm => tm.AgentId)
            .ToListAsync();

        return directAgents.Union(teamMemberAgents).Distinct().ToList();
    }

    /// <summary>
    /// Assigns an agent directly to a project. Idempotent — does nothing if already assigned.
    /// Returns <c>true</c> on success; <c>false</c> if the project is not found.
    /// </summary>
    public async Task<bool> AssignAgentAsync(Guid projectId, Guid agentId)
    {
        using var db = await factory.CreateDbContextAsync();
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists) return false;

        var alreadyAssigned = await db.ProjectAgents
            .AnyAsync(pa => pa.ProjectId == projectId && pa.AgentId == agentId);
        if (alreadyAssigned) return true;

        db.ProjectAgents.Add(new ProjectAgent
        {
            ProjectId = projectId,
            AgentId = agentId,
            AssignedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Removes a direct agent assignment from a project.
    /// Returns <c>true</c> if removed; <c>false</c> if the assignment was not found.
    /// </summary>
    public async Task<bool> UnassignAgentAsync(Guid projectId, Guid agentId)
    {
        using var db = await factory.CreateDbContextAsync();
        var assignment = await db.ProjectAgents
            .FirstOrDefaultAsync(pa => pa.ProjectId == projectId && pa.AgentId == agentId);
        if (assignment is null) return false;

        db.ProjectAgents.Remove(assignment);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Assigns a team to a project. Idempotent — does nothing if already assigned.
    /// Returns <c>true</c> on success; <c>false</c> if the project is not found.
    /// </summary>
    public async Task<bool> AssignTeamAsync(Guid projectId, Guid teamId)
    {
        using var db = await factory.CreateDbContextAsync();
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists) return false;

        var alreadyAssigned = await db.ProjectTeams
            .AnyAsync(pt => pt.ProjectId == projectId && pt.TeamId == teamId);
        if (alreadyAssigned) return true;

        db.ProjectTeams.Add(new ProjectTeam
        {
            ProjectId = projectId,
            TeamId = teamId,
            AssignedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Removes a team assignment from a project.
    /// Returns <c>true</c> if removed; <c>false</c> if the assignment was not found.
    /// </summary>
    public async Task<bool> UnassignTeamAsync(Guid projectId, Guid teamId)
    {
        using var db = await factory.CreateDbContextAsync();
        var assignment = await db.ProjectTeams
            .FirstOrDefaultAsync(pt => pt.ProjectId == projectId && pt.TeamId == teamId);
        if (assignment is null) return false;

        db.ProjectTeams.Remove(assignment);
        await db.SaveChangesAsync();
        return true;
    }

    // ── Rich list helpers for the API layer ───────────────────────────────────

    /// <summary>
    /// Returns a rich list of agent assignments for the project,
    /// including direct assignments and agents reached via team membership.
    /// Each entry carries the agent ID, the assigned-at timestamp, and the source ("direct" or "team").
    /// </summary>
    public async Task<List<ProjectAgentEntry>> GetProjectAgentEntriesAsync(Guid projectId)
    {
        using var db = await factory.CreateDbContextAsync();

        var directEntries = await db.ProjectAgents
            .Where(pa => pa.ProjectId == projectId)
            .Select(pa => new ProjectAgentEntry(pa.AgentId, pa.AssignedAt, "direct"))
            .ToListAsync();

        var teamAssignments = await db.ProjectTeams
            .Where(pt => pt.ProjectId == projectId)
            .ToListAsync();

        var teamIds = teamAssignments.Select(pt => pt.TeamId).ToList();

        var teamMemberEntries = await db.TeamMembers
            .Where(tm => teamIds.Contains(tm.TeamId))
            .ToListAsync();

        // Build team-sourced entries; use team's AssignedAt as the timestamp.
        var teamEntries = teamMemberEntries
            .Select(tm => new ProjectAgentEntry(
                tm.AgentId,
                teamAssignments.First(ta => ta.TeamId == tm.TeamId).AssignedAt,
                "team"))
            .ToList();

        // Merge: direct takes precedence; deduplicate by agentId keeping the "direct" entry.
        var directIds = directEntries.Select(e => e.AgentId).ToHashSet();
        var merged = directEntries
            .Concat(teamEntries.Where(te => !directIds.Contains(te.AgentId)))
            .ToList();

        return merged;
    }

    /// <summary>Returns a list of team assignment entries for the project.</summary>
    public async Task<List<ProjectTeamEntry>> GetProjectTeamEntriesAsync(Guid projectId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ProjectTeams
            .Where(pt => pt.ProjectId == projectId)
            .Select(pt => new ProjectTeamEntry(pt.TeamId, pt.AssignedAt))
            .ToListAsync();
    }
}

/// <summary>Represents a single agent entry in a project assignment list.</summary>
/// <param name="AgentId">The agent identifier.</param>
/// <param name="AssignedAt">UTC timestamp of the assignment.</param>
/// <param name="Source">Either "direct" or "team".</param>
public sealed record ProjectAgentEntry(Guid AgentId, DateTime AssignedAt, string Source);

/// <summary>Represents a single team entry in a project assignment list.</summary>
/// <param name="TeamId">The team identifier.</param>
/// <param name="AssignedAt">UTC timestamp of the assignment.</param>
public sealed record ProjectTeamEntry(Guid TeamId, DateTime AssignedAt);
