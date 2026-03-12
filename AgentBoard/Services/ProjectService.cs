using AgentBoard.Contracts;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>CRUD service for <see cref="Project"/> entities.</summary>
public class ProjectService(IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Returns all projects ordered by name.</summary>
    public async Task<List<Project>> GetAllAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Projects
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    /// <summary>Returns a single project by ID, or <c>null</c> if not found.</summary>
    public async Task<Project?> GetByIdAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Projects.FindAsync(id);
    }

    /// <summary>Creates a new project. Sets <see cref="Project.Id"/>, <see cref="Project.CreatedAt"/>, and <see cref="Project.UpdatedAt"/>.</summary>
    public async Task<Project> CreateAsync(Project project)
    {
        using var db = await factory.CreateDbContextAsync();
        project.Id = Guid.NewGuid();
        project.CreatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    /// <summary>Updates an existing project. Sets <see cref="Project.UpdatedAt"/>. Returns <c>null</c> if not found.</summary>
    public async Task<Project?> UpdateAsync(Project project)
    {
        using var db = await factory.CreateDbContextAsync();
        var existing = await db.Projects.FindAsync(project.Id);
        if (existing is null) return null;
        existing.Name = project.Name;
        existing.Description = project.Description;
        existing.Goals = project.Goals;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return existing;
    }

    /// <summary>Applies a partial update to the project identified by <paramref name="id"/>.</summary>
    /// <returns>The updated project, or <c>null</c> if not found.</returns>
    public async Task<Project?> PatchAsync(Guid id, ProjectPatch patch)
    {
        using var db = await factory.CreateDbContextAsync();
        var project = await db.Projects.FindAsync(id);
        if (project is null) return null;

        if (patch.Name is not null) project.Name = patch.Name;
        if (patch.Description is not null) project.Description = patch.Description;
        if (patch.Goals is not null) project.Goals = patch.Goals;
        if (patch.ClearIntegrationToken) project.IntegrationToken = null;
        else if (patch.IntegrationToken is not null) project.IntegrationToken = patch.IntegrationToken;
        if (patch.IntegrationRepoUrl is not null) project.IntegrationRepoUrl = patch.IntegrationRepoUrl;
        if (patch.ExternalProjectId is not null) project.ExternalProjectId = patch.ExternalProjectId;
        project.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return project;
    }

    /// <summary>Deletes a project by ID. Returns <c>true</c> if deleted, <c>false</c> if not found.</summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        var project = await db.Projects.FindAsync(id);
        if (project is null) return false;
        db.Projects.Remove(project);
        await db.SaveChangesAsync();
        return true;
    }
}
