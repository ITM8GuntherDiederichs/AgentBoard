using AgentBoard.Data.Models;
using AgentBoard.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentBoard.Api;

/// <summary>REST endpoints for uploading and downloading skill reference files.</summary>
public static class SkillFileEndpoints
{
    /// <summary>Registers skill-file endpoints under <c>/api/skills/{skillId}/files</c>.</summary>
    public static void MapSkillFileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/skills/{skillId:guid}/files").WithTags("skill-files");

        // GET /api/skills/{skillId}/files
        group.MapGet("/", async (Guid skillId, SkillFileService fileSvc) =>
            Results.Ok(await fileSvc.GetFilesForSkillAsync(skillId)));

        // POST /api/skills/{skillId}/files  (multipart form upload)
        group.MapPost("/", async (
            Guid skillId,
            IFormFile file,
            SkillService skillSvc,
            SkillFileService fileSvc,
            IWebHostEnvironment env) =>
        {
            var skill = await skillSvc.GetByIdAsync(skillId);
            if (skill is null) return Results.NotFound();

            var wwwrootPath = env.WebRootPath ?? Path.GetTempPath();
            var skillFile = await fileSvc.UploadFileAsync(skillId, file, wwwrootPath);
            return Results.Created($"/api/skills/{skillId}/files/{skillFile.Id}", skillFile);
        }).DisableAntiforgery();

        // DELETE /api/skills/{skillId}/files/{fileId}
        group.MapDelete("/{fileId:guid}", async (
            Guid skillId,
            Guid fileId,
            SkillFileService fileSvc,
            IWebHostEnvironment env) =>
        {
            var wwwrootPath = env.WebRootPath ?? Path.GetTempPath();
            var deleted = await fileSvc.DeleteFileAsync(fileId, wwwrootPath);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // GET /api/skills/{skillId}/files/{fileId}/download
        // Extracted to a static method to give an explicit Task<IResult> return type,
        // avoiding C# lambda-type-inference issues when mixing NotFound with PhysicalFileHttpResult.
        group.MapGet("/{fileId:guid}/download", HandleDownload);
    }

    private static async Task<IResult> HandleDownload(
        Guid skillId,
        Guid fileId,
        SkillFileService fileSvc,
        IWebHostEnvironment env)
    {
        var skillFile = await fileSvc.GetFileAsync(fileId);
        if (skillFile is null) return Results.NotFound();

        var wwwrootPath = env.WebRootPath ?? Path.GetTempPath();
        var fullPath = Path.Combine(
            wwwrootPath,
            skillFile.FilePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath)) return Results.NotFound();

        return TypedResults.PhysicalFile(fullPath, skillFile.ContentType,
            fileDownloadName: skillFile.FileName);
    }
}
