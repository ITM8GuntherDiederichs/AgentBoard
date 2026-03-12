using AgentBoard.Data.Models;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace AgentBoard.Tests.Services;

/// <summary>Unit tests for <see cref="SkillFileService"/>.</summary>
public class SkillFileServiceTests : IDisposable
{
    // Each test class instance gets its own isolated temp directory.
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(), "AgentBoardTests_" + Guid.NewGuid().ToString("N"));

    public SkillFileServiceTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    private static SkillFileService BuildService(string? dbName = null)
        => new(TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString()));

    /// <summary>Creates a mock <see cref="IFormFile"/> backed by in-memory content.</summary>
    private static IFormFile MakeFormFile(string fileName = "test.txt", string content = "hello", string contentType = "text/plain")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(fileName);
        file.ContentType.Returns(contentType);
        file.Length.Returns(bytes.Length);
        file.CopyToAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                stream.Position = 0;
                return stream.CopyToAsync(ci.Arg<Stream>(), ci.Arg<CancellationToken>());
            });
        return file;
    }

    // ── GetFilesForSkillAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetFilesForSkillAsync_ReturnsEmpty_WhenNoFiles()
    {
        var svc = BuildService();
        var result = await svc.GetFilesForSkillAsync(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFilesForSkillAsync_ReturnsOnlyFilesForGivenSkill()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skillA = Guid.NewGuid();
        var skillB = Guid.NewGuid();

        await svc.UploadFileAsync(skillA, MakeFormFile("a.txt"), _tempRoot);
        await svc.UploadFileAsync(skillA, MakeFormFile("b.txt"), _tempRoot);
        await svc.UploadFileAsync(skillB, MakeFormFile("c.txt"), _tempRoot);

        var result = await svc.GetFilesForSkillAsync(skillA);
        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.Equal(skillA, f.SkillId));
    }

    [Fact]
    public async Task GetFilesForSkillAsync_ReturnsFilesOrderedByUploadedAt()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skillId = Guid.NewGuid();

        var f1 = await svc.UploadFileAsync(skillId, MakeFormFile("first.txt"), _tempRoot);
        await Task.Delay(5); // ensure different timestamps
        var f2 = await svc.UploadFileAsync(skillId, MakeFormFile("second.txt"), _tempRoot);

        var result = await svc.GetFilesForSkillAsync(skillId);
        Assert.Equal(2, result.Count);
        Assert.True(result[0].UploadedAt <= result[1].UploadedAt);
    }

    // ── UploadFileAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UploadFileAsync_ReturnsSavedRecord_WithCorrectMetadata()
    {
        var svc = BuildService();
        var skillId = Guid.NewGuid();
        var formFile = MakeFormFile("report.pdf", "pdf-content", "application/pdf");

        var result = await svc.UploadFileAsync(skillId, formFile, _tempRoot);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(skillId, result.SkillId);
        Assert.Equal("report.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.True(result.FileSize > 0);
        Assert.False(string.IsNullOrEmpty(result.FilePath));
    }

    [Fact]
    public async Task UploadFileAsync_CreatesPhysicalFile_OnDisk()
    {
        var svc = BuildService();
        var skillId = Guid.NewGuid();
        var formFile = MakeFormFile("notes.txt", "important notes");

        var result = await svc.UploadFileAsync(skillId, formFile, _tempRoot);

        var fullPath = Path.Combine(_tempRoot, result.FilePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Expected file at {fullPath}");
    }

    [Fact]
    public async Task UploadFileAsync_FilePathUsesForwardSlashes()
    {
        var svc = BuildService();
        var skillId = Guid.NewGuid();

        var result = await svc.UploadFileAsync(skillId, MakeFormFile(), _tempRoot);

        Assert.DoesNotContain('\\', result.FilePath);
    }

    [Fact]
    public async Task UploadFileAsync_SetsUploadedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.UploadFileAsync(Guid.NewGuid(), MakeFormFile(), _tempRoot);
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.UploadedAt, before, after);
    }

    [Fact]
    public async Task UploadFileAsync_PersistsRecord_InDatabase()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skillId = Guid.NewGuid();

        var uploaded = await svc.UploadFileAsync(skillId, MakeFormFile("stored.txt"), _tempRoot);
        var retrieved = await svc.GetFileAsync(uploaded.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("stored.txt", retrieved.FileName);
    }

    // ── DeleteFileAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteFileAsync_ReturnsFalse_WhenFileRecordNotFound()
    {
        var svc = BuildService();
        var result = await svc.DeleteFileAsync(Guid.NewGuid(), _tempRoot);
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteFileAsync_ReturnsTrue_WhenFileExists()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var uploaded = await svc.UploadFileAsync(Guid.NewGuid(), MakeFormFile(), _tempRoot);

        var result = await svc.DeleteFileAsync(uploaded.Id, _tempRoot);

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesRecord_FromDatabase()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var uploaded = await svc.UploadFileAsync(Guid.NewGuid(), MakeFormFile(), _tempRoot);

        await svc.DeleteFileAsync(uploaded.Id, _tempRoot);

        var retrieved = await svc.GetFileAsync(uploaded.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesPhysicalFile_FromDisk()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var uploaded = await svc.UploadFileAsync(Guid.NewGuid(), MakeFormFile(), _tempRoot);

        var fullPath = Path.Combine(_tempRoot, uploaded.FilePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath)); // sanity check

        await svc.DeleteFileAsync(uploaded.Id, _tempRoot);

        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task DeleteFileAsync_ReturnsTrue_EvenIfPhysicalFileAlreadyMissing()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var uploaded = await svc.UploadFileAsync(Guid.NewGuid(), MakeFormFile(), _tempRoot);

        // Remove the physical file manually before calling delete
        var fullPath = Path.Combine(_tempRoot, uploaded.FilePath.Replace('/', Path.DirectorySeparatorChar));
        File.Delete(fullPath);

        var result = await svc.DeleteFileAsync(uploaded.Id, _tempRoot);

        Assert.True(result);
        Assert.Null(await svc.GetFileAsync(uploaded.Id));
    }

    // ── GetFileAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFileAsync_ReturnsRecord_WhenFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var uploaded = await svc.UploadFileAsync(Guid.NewGuid(), MakeFormFile("readme.md"), _tempRoot);

        var result = await svc.GetFileAsync(uploaded.Id);

        Assert.NotNull(result);
        Assert.Equal("readme.md", result.FileName);
    }

    [Fact]
    public async Task GetFileAsync_ReturnsNull_WhenNotFound()
    {
        var svc = BuildService();
        var result = await svc.GetFileAsync(Guid.NewGuid());
        Assert.Null(result);
    }
}
