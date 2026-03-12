using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>Patch payload for partial updates to a <see cref="FeatureRequest"/>.</summary>
public record FeatureRequestPatch(
    string? Title,
    string? Description,
    TodoPriority? Priority,
    FeatureRequestStatus? Status);

/// <summary>Service for managing feature requests scoped to a project.</summary>
public class FeatureRequestService(IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Returns all feature requests for a given project, ordered by priority descending then created ascending.</summary>
    public async Task<List<FeatureRequest>> GetByProjectAsync(Guid projectId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.FeatureRequests
            .Where(f => f.ProjectId == projectId)
            .OrderByDescending(f => f.Priority)
            .ThenBy(f => f.CreatedAt)
            .ToListAsync();
    }

    /// <summary>Returns a single feature request by ID, or null if not found.</summary>
    public async Task<FeatureRequest?> GetByIdAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.FeatureRequests.FindAsync(id);
    }

    /// <summary>Creates a new feature request, assigning a new ID and setting timestamps.</summary>
    public async Task<FeatureRequest> CreateAsync(FeatureRequest fr)
    {
        using var db = await factory.CreateDbContextAsync();
        fr.Id = Guid.NewGuid();
        fr.CreatedAt = DateTime.UtcNow;
        fr.UpdatedAt = DateTime.UtcNow;
        db.FeatureRequests.Add(fr);
        await db.SaveChangesAsync();
        return fr;
    }

    /// <summary>Applies a partial update to an existing feature request. Returns null if not found.</summary>
    public async Task<FeatureRequest?> PatchAsync(Guid id, FeatureRequestPatch patch)
    {
        using var db = await factory.CreateDbContextAsync();
        var fr = await db.FeatureRequests.FindAsync(id);
        if (fr is null) return null;

        if (patch.Title is not null) fr.Title = patch.Title;
        if (patch.Description is not null) fr.Description = patch.Description;
        if (patch.Priority.HasValue) fr.Priority = patch.Priority.Value;
        if (patch.Status.HasValue) fr.Status = patch.Status.Value;
        fr.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return fr;
    }

    /// <summary>Deletes a feature request. Returns true if deleted, false if not found.</summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        var fr = await db.FeatureRequests.FindAsync(id);
        if (fr is null) return false;
        db.FeatureRequests.Remove(fr);
        await db.SaveChangesAsync();
        return true;
    }
}