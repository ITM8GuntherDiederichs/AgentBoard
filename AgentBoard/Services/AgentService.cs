using AgentBoard.Contracts;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>Service for managing <see cref="Agent"/> records.</summary>
public class AgentService(IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>
    /// Returns all agents, optionally filtered by availability.
    /// </summary>
    /// <param name="availableOnly">
    /// When <c>true</c> only available agents are returned;
    /// when <c>false</c> only unavailable; when <c>null</c> all agents are returned.
    /// </param>
    public async Task<List<Agent>> GetAllAsync(bool? availableOnly = null)
    {
        using var db = await factory.CreateDbContextAsync();
        var q = db.Agents.AsQueryable();
        if (availableOnly.HasValue)
            q = q.Where(a => a.IsAvailable == availableOnly.Value);
        return await q.OrderBy(a => a.Name).ToListAsync();
    }

    /// <summary>Returns the agent with the specified <paramref name="id"/>, or <c>null</c> if not found.</summary>
    public async Task<Agent?> GetByIdAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Agents.FindAsync(id);
    }

    /// <summary>Persists a new agent. Sets <see cref="Agent.Id"/>, <see cref="Agent.CreatedAt"/> and <see cref="Agent.UpdatedAt"/>.</summary>
    public async Task<Agent> CreateAsync(Agent agent)
    {
        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        agent.Id = Guid.NewGuid();
        agent.CreatedAt = now;
        agent.UpdatedAt = now;
        db.Agents.Add(agent);
        await db.SaveChangesAsync();
        return agent;
    }

    /// <summary>Replaces the stored agent with the provided values. Sets <see cref="Agent.UpdatedAt"/>.</summary>
    /// <returns>The updated agent.</returns>
    public async Task<Agent> UpdateAsync(Agent agent)
    {
        using var db = await factory.CreateDbContextAsync();
        agent.UpdatedAt = DateTime.UtcNow;
        db.Agents.Update(agent);
        await db.SaveChangesAsync();
        return agent;
    }

    /// <summary>Applies a partial update to the agent identified by <paramref name="id"/>.</summary>
    /// <returns>The updated agent, or <c>null</c> if not found.</returns>
    public async Task<Agent?> PatchAsync(Guid id, AgentPatch patch)
    {
        using var db = await factory.CreateDbContextAsync();
        var agent = await db.Agents.FindAsync(id);
        if (agent is null) return null;

        if (patch.Name is not null) agent.Name = patch.Name;
        if (patch.Description is not null) agent.Description = patch.Description;
        if (patch.Type.HasValue) agent.Type = patch.Type.Value;
        if (patch.IsAvailable.HasValue) agent.IsAvailable = patch.IsAvailable.Value;
        agent.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return agent;
    }

    /// <summary>Deletes the agent with the specified <paramref name="id"/>.</summary>
    /// <returns><c>true</c> if the agent was deleted; <c>false</c> if it was not found.</returns>
    public async Task<bool> DeleteAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        var agent = await db.Agents.FindAsync(id);
        if (agent is null) return false;
        db.Agents.Remove(agent);
        await db.SaveChangesAsync();
        return true;
    }
}