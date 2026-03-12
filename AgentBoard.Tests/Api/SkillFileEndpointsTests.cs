using System.Net;
using System.Net.Http.Json;
using AgentBoard.Data.Models;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Api;

/// <summary>
/// Integration tests for <c>SkillFileEndpoints</c> routes.
/// Uses <see cref="SkillFileWebFactory"/> so that uploaded files land in a real temp directory.
/// </summary>
public class SkillFileEndpointsTests : IClassFixture<SkillFileWebFactory>
{
    private readonly HttpClient _client;

    public SkillFileEndpointsTests(SkillFileWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── GET /api/skills/{skillId}/files ──────────────────────────────────────

    [Fact]
    public async Task GetFiles_Returns200_WithEmptyList_WhenNoFilesExist()
    {
        var response = await _client.GetAsync($"/api/skills/{Guid.NewGuid()}/files");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<SkillFileDto>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFiles_Returns200_WithUploadedFiles()
    {
        var skill = await CreateSkillAsync();
        await UploadFileAsync(skill.Id, "notes.txt", "hello world");

        var response = await _client.GetAsync($"/api/skills/{skill.Id}/files");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<SkillFileDto>>();
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("notes.txt", result[0].FileName);
    }

    // ── POST /api/skills/{skillId}/files ──────────────────────────────────────

    [Fact]
    public async Task PostFile_Returns201_WithLocationHeader_AndCorrectBody()
    {
        var skill = await CreateSkillAsync();
        var response = await UploadFileAsync(skill.Id, "doc.pdf", "PDF content", "application/pdf");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains($"/api/skills/{skill.Id}/files/", response.Headers.Location!.ToString());

        var file = await response.Content.ReadFromJsonAsync<SkillFileDto>();
        Assert.NotNull(file);
        Assert.Equal("doc.pdf", file.FileName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.True(file.FileSize > 0);
        Assert.NotEqual(Guid.Empty, file.Id);
        Assert.Equal(skill.Id, file.SkillId);
    }

    [Fact]
    public async Task PostFile_Returns404_WhenSkillDoesNotExist()
    {
        var response = await UploadFileAsync(Guid.NewGuid(), "orphan.txt", "content");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostFile_SetsUploadedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-5);
        var skill = await CreateSkillAsync();
        var response = await UploadFileAsync(skill.Id, "ts.txt", "data");
        response.EnsureSuccessStatusCode();
        var file = await response.Content.ReadFromJsonAsync<SkillFileDto>();
        Assert.NotNull(file);
        Assert.True(file.UploadedAt >= before);
    }

    [Fact]
    public async Task PostFile_MultipleFiles_AllAppearInGetList()
    {
        var skill = await CreateSkillAsync();
        await UploadFileAsync(skill.Id, "a.txt", "aaa");
        await UploadFileAsync(skill.Id, "b.md", "bbb");

        var response = await _client.GetAsync($"/api/skills/{skill.Id}/files");
        var result = await response.Content.ReadFromJsonAsync<List<SkillFileDto>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    // ── DELETE /api/skills/{skillId}/files/{fileId} ───────────────────────────

    [Fact]
    public async Task DeleteFile_Returns204_ForExistingFile()
    {
        var skill = await CreateSkillAsync();
        var uploaded = await UploadAndGetAsync(skill.Id, "delete-me.txt");

        var response = await _client.DeleteAsync($"/api/skills/{skill.Id}/files/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_Returns404_ForNonExistentFileId()
    {
        var response = await _client.DeleteAsync($"/api/skills/{Guid.NewGuid()}/files/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_FileNoLongerAppearsInList_AfterDeletion()
    {
        var skill = await CreateSkillAsync();
        var uploaded = await UploadAndGetAsync(skill.Id, "gone.txt");

        await _client.DeleteAsync($"/api/skills/{skill.Id}/files/{uploaded.Id}");

        var listResponse = await _client.GetAsync($"/api/skills/{skill.Id}/files");
        var result = await listResponse.Content.ReadFromJsonAsync<List<SkillFileDto>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── GET /api/skills/{skillId}/files/{fileId}/download ────────────────────

    [Fact]
    public async Task DownloadFile_Returns200_WithFileContent()
    {
        var skill = await CreateSkillAsync();
        const string originalContent = "This is file content for download.";
        var uploaded = await UploadAndGetAsync(skill.Id, "download.txt", originalContent, "text/plain");

        var response = await _client.GetAsync($"/api/skills/{skill.Id}/files/{uploaded.Id}/download");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal(originalContent, content);
    }

    [Fact]
    public async Task DownloadFile_Returns_ContentDisposition_WithFileName()
    {
        var skill = await CreateSkillAsync();
        var uploaded = await UploadAndGetAsync(skill.Id, "myreport.pdf", "pdf data", "application/pdf");

        var response = await _client.GetAsync($"/api/skills/{skill.Id}/files/{uploaded.Id}/download");
        response.EnsureSuccessStatusCode();

        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("myreport.pdf", disposition!.FileNameStar ?? disposition.FileName?.Trim('"'));
    }

    [Fact]
    public async Task DownloadFile_Returns404_ForNonExistentFileId()
    {
        var response = await _client.GetAsync($"/api/skills/{Guid.NewGuid()}/files/{Guid.NewGuid()}/download");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DownloadFile_Returns_CorrectContentType()
    {
        var skill = await CreateSkillAsync();
        var uploaded = await UploadAndGetAsync(skill.Id, "archive.pdf", "pdf content", "application/pdf");

        var response = await _client.GetAsync($"/api/skills/{skill.Id}/files/{uploaded.Id}/download");
        response.EnsureSuccessStatusCode();

        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<SkillDto> CreateSkillAsync(string name = "Test Skill")
    {
        var response = await _client.PostAsJsonAsync("/api/skills", new { Name = name, Content = "content" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SkillDto>())!;
    }

    private async Task<HttpResponseMessage> UploadFileAsync(
        Guid skillId,
        string fileName = "test.txt",
        string content = "file content",
        string contentType = "text/plain")
    {
        using var form = new MultipartFormDataContent();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return await _client.PostAsync($"/api/skills/{skillId}/files", form);
    }

    private async Task<SkillFileDto> UploadAndGetAsync(
        Guid skillId,
        string fileName = "test.txt",
        string content = "file content",
        string contentType = "text/plain")
    {
        var response = await UploadFileAsync(skillId, fileName, content, contentType);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SkillFileDto>())!;
    }

    // ── local DTOs ────────────────────────────────────────────────────────────

    private sealed record SkillDto(Guid Id, string Name, string Content, DateTime CreatedAt, DateTime UpdatedAt);

    private sealed record SkillFileDto(
        Guid Id,
        Guid SkillId,
        string FileName,
        string ContentType,
        long FileSize,
        string FilePath,
        DateTime UploadedAt);
}
