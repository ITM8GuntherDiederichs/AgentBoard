using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>Service for managing <see cref="SkillFile"/> records and their associated on-disk files.</summary>
public class SkillFileService(IDbContextFactory<ApplicationDbContext> factory)
{
    /// <summary>Returns all file records attached to the given skill, ordered by upload time.</summary>
    public async Task<List<SkillFile>> GetFilesForSkillAsync(Guid skillId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.SkillFiles
            .Where(f => f.SkillId == skillId)
            .OrderBy(f => f.UploadedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Saves <paramref name="file"/> to
    /// <c>{wwwrootPath}/uploads/skills/{skillId}/{newGuid}{ext}</c> and persists a
    /// <see cref="SkillFile"/> record to the database.
    /// </summary>
    /// <returns>The saved <see cref="SkillFile"/> record.</returns>
    public async Task<SkillFile> UploadFileAsync(Guid skillId, IFormFile file, string wwwrootPath)
    {
        var skillDir = Path.Combine(wwwrootPath, "uploads", "skills", skillId.ToString());
        Directory.CreateDirectory(skillDir);

        var ext = Path.GetExtension(file.FileName);
        var uniqueFileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(skillDir, uniqueFileName);

        using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        // Normalize to forward slashes so the path is portable across OS.
        var relativePath = $"uploads/skills/{skillId}/{uniqueFileName}";

        var skillFile = new SkillFile
        {
            Id = Guid.NewGuid(),
            SkillId = skillId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            FilePath = relativePath,
            UploadedAt = DateTime.UtcNow
        };

        using var db = await factory.CreateDbContextAsync();
        db.SkillFiles.Add(skillFile);
        await db.SaveChangesAsync();
        return skillFile;
    }

    /// <summary>
    /// Deletes the physical file from disk and removes the <see cref="SkillFile"/> record
    /// from the database.
    /// </summary>
    /// <returns><c>true</c> if the record was found and deleted; <c>false</c> if not found.</returns>
    public async Task<bool> DeleteFileAsync(Guid fileId, string wwwrootPath)
    {
        using var db = await factory.CreateDbContextAsync();
        var skillFile = await db.SkillFiles.FindAsync(fileId);
        if (skillFile is null) return false;

        var fullPath = Path.Combine(
            wwwrootPath,
            skillFile.FilePath.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        db.SkillFiles.Remove(skillFile);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Returns the <see cref="SkillFile"/> with the specified <paramref name="fileId"/>,
    /// or <c>null</c> if not found.</summary>
    public async Task<SkillFile?> GetFileAsync(Guid fileId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.SkillFiles.FindAsync(fileId);
    }
}
