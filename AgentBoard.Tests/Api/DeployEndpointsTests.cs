using System.Net;
using System.Net.Http.Json;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using AgentBoard.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBoard.Tests.Api;

/// <summary>Integration tests for deploy-related endpoints.</summary>
public class DeployEndpointsTests : IClassFixture<SkillFileWebFactory>
{
    private readonly HttpClient _client;
    private readonly SkillFileWebFactory _factory;

    public DeployEndpointsTests(SkillFileWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Helper to create a project via the API ───────────────────────────────

    private async Task<ProjectDto> CreateProjectAsync(string name = "Deploy Test Project")
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new { Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    // ── GET /api/projects/{id}/deploy ────────────────────────────────────────

    [Fact]
    public async Task GetDeploy_Returns200_WithZipContentType_ForExistingProject()
    {
        var project = await CreateProjectAsync("Zip Test " + Guid.NewGuid().ToString("N")[..8]);

        var response = await _client.GetAsync($"/api/projects/{project.Id}/deploy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetDeploy_Returns404_ForUnknownProject()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/deploy");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDeploy_ZipContains_AgentboardJson()
    {
        var project = await CreateProjectAsync("AgentboardJson Test " + Guid.NewGuid().ToString("N")[..8]);

        var response = await _client.GetAsync($"/api/projects/{project.Id}/deploy");
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes), System.IO.Compression.ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("agentboard.json"));
    }

    [Fact]
    public async Task GetDeploy_ZipContains_TeamMd()
    {
        var project = await CreateProjectAsync("TeamMd Test " + Guid.NewGuid().ToString("N")[..8]);

        var response = await _client.GetAsync($"/api/projects/{project.Id}/deploy");
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes), System.IO.Compression.ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("team.md"));
    }

    // ── POST /api/projects/{id}/integration ──────────────────────────────────

    [Fact]
    public async Task PostIntegration_Returns200_AndStoresFields()
    {
        var project = await CreateProjectAsync("Integration Test " + Guid.NewGuid().ToString("N")[..8]);

        var body = new
        {
            integrationToken = "ghp_testtoken123",
            integrationRepoUrl = "https://github.com/org/repo",
            externalProjectId = "org/repo"
        };

        var response = await _client.PostAsJsonAsync($"/api/projects/{project.Id}/integration", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        Assert.NotNull(updated);
        Assert.Equal("https://github.com/org/repo", updated.IntegrationRepoUrl);
        Assert.Equal("org/repo", updated.ExternalProjectId);
    }

    [Fact]
    public async Task PostIntegration_Returns404_ForUnknownProject()
    {
        var body = new { integrationToken = "token", integrationRepoUrl = "https://github.com/x/y", externalProjectId = "x/y" };
        var response = await _client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/integration", body);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostIntegration_CanSetTokenAndRetrieveFromDb()
    {
        var project = await CreateProjectAsync("Token Store Test " + Guid.NewGuid().ToString("N")[..8]);

        var body = new { integrationToken = "secret-token-abc", integrationRepoUrl = (string?)null, externalProjectId = (string?)null };
        var response = await _client.PostAsJsonAsync($"/api/projects/{project.Id}/integration", body);
        response.EnsureSuccessStatusCode();

        // Verify the token was stored by querying the DB directly
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        using var db = await factory.CreateDbContextAsync();
        var stored = await db.Projects.FindAsync(project.Id);
        Assert.NotNull(stored);
        Assert.Equal("secret-token-abc", stored.IntegrationToken);
    }

    // ── Local DTOs ───────────────────────────────────────────────────────────

    private sealed record ProjectDto(Guid Id, string Name, string? Description, string? Goals, DateTime CreatedAt, DateTime UpdatedAt);

    private sealed record ProjectDetailDto(
        Guid Id,
        string Name,
        string? Description,
        string? Goals,
        string? IntegrationToken,
        string? IntegrationRepoUrl,
        string? ExternalProjectId,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
