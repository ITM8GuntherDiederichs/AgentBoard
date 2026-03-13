using System.Net;
using System.Net.Http.Json;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using AgentBoard.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBoard.Tests.Api;

/// <summary>
/// Integration tests for the auth-callback endpoints:
/// <list type="bullet">
/// <item><c>POST /api/projects/{id}/integration</c> — token validation + ConnectedAt</item>
/// <item><c>GET  /api/projects/{id}/integration/status</c> — status shape</item>
/// </list>
/// Each test class instance gets its own factory to allow independent
/// mock-handler configuration without shared state.
/// </summary>
public class IntegrationEndpointsTests : IDisposable
{
    private readonly IntegrationWebFactory _factory;
    private readonly HttpClient _client;

    public IntegrationEndpointsTests()
    {
        _factory = new IntegrationWebFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── POST /api/projects/{id}/integration ───────────────────────────────────

    [Fact]
    public async Task PostIntegration_NoTeam_Returns200_WithWebhookUrl_AndConnectedAtSet()
    {
        // Arrange — project with no team assigned means IntegrationType.None → skip validation
        var project = await CreateProjectAsync("Auth-Connect NoTeam");

        var body = new
        {
            integrationToken = "any-token",
            integrationRepoUrl = "https://github.com/org/repo",
            externalProjectId = "org/repo"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/projects/{project.Id}/integration", body);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<IntegrationConnectResponseDto>();
        Assert.NotNull(result);
        Assert.NotNull(result.Project);
        Assert.NotNull(result.WebhookUrl);
        Assert.Contains("/sync/", result.WebhookUrl);
        Assert.Contains("/webhook", result.WebhookUrl);
        Assert.NotNull(result.Project.IntegrationConnectedAt);
    }

    [Fact]
    public async Task PostIntegration_GitHubTeam_ValidToken_Returns200_ConnectedAtSet()
    {
        // Arrange — create project + GitHub team + assign team to project
        const string validToken = "ghp_valid_token_abc";
        _factory.MockHandler.ValidTokenValue = validToken;

        var project = await CreateProjectAsync("Auth-Connect GitHub");
        await AssignGitHubTeamAsync(project.Id);

        var body = new
        {
            integrationToken = validToken,
            integrationRepoUrl = "https://github.com/org/repo",
            externalProjectId = "org/repo"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/projects/{project.Id}/integration", body);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<IntegrationConnectResponseDto>();
        Assert.NotNull(result?.Project);
        Assert.NotNull(result.Project.IntegrationConnectedAt);
        Assert.NotNull(result.WebhookUrl);
        Assert.Contains("github", result.WebhookUrl);

        // Token must NOT appear in response
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(validToken, raw);
    }

    [Fact]
    public async Task PostIntegration_GitHubTeam_InvalidToken_Returns400()
    {
        // Arrange — mock handler only accepts "correct-token"
        _factory.MockHandler.ValidTokenValue = "correct-token";

        var project = await CreateProjectAsync("Auth-Connect GitHub Bad Token");
        await AssignGitHubTeamAsync(project.Id);

        var body = new
        {
            integrationToken = "wrong-token",
            integrationRepoUrl = "https://github.com/org/repo",
            externalProjectId = "org/repo"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/projects/{project.Id}/integration", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("GitHub token validation failed", raw);
    }

    [Fact]
    public async Task PostIntegration_AzureDevOpsTeam_InvalidToken_Returns400()
    {
        // Arrange
        _factory.MockHandler.ValidTokenValue = "correct-ado-token";

        var project = await CreateProjectAsync("Auth-Connect ADO Bad Token");
        await AssignAzureDevOpsTeamAsync(project.Id);

        var body = new
        {
            integrationToken = "bad-ado-token",
            integrationRepoUrl = "https://dev.azure.com/myorg/myproject",
            externalProjectId = "myproject"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/projects/{project.Id}/integration", body);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("Azure DevOps token validation failed", raw);
    }

    [Fact]
    public async Task PostIntegration_UnknownProject_Returns404()
    {
        var body = new { integrationToken = "token", integrationRepoUrl = (string?)null, externalProjectId = (string?)null };
        var response = await _client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/integration", body);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /api/projects/{id}/integration/status ─────────────────────────────

    [Fact]
    public async Task GetIntegrationStatus_NotConnected_Returns_IsConnectedFalse()
    {
        // Arrange — project with no integration token set
        var project = await CreateProjectAsync("Status-NotConnected");

        // Act
        var response = await _client.GetAsync($"/api/projects/{project.Id}/integration/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<IntegrationStatusDto>();
        Assert.NotNull(status);
        Assert.False(status.IsConnected);
        Assert.Null(status.ConnectedAt);
        Assert.NotNull(status.WebhookUrl);
        Assert.Equal("None", status.IntegrationType);
    }

    [Fact]
    public async Task GetIntegrationStatus_AfterConnect_Returns_IsConnectedTrue()
    {
        // Arrange — connect integration first (no team → skip validation)
        var project = await CreateProjectAsync("Status-Connected");

        await _client.PostAsJsonAsync($"/api/projects/{project.Id}/integration", new
        {
            integrationToken = "any-token",
            integrationRepoUrl = "https://github.com/org/repo",
            externalProjectId = (string?)null
        });

        // Act
        var response = await _client.GetAsync($"/api/projects/{project.Id}/integration/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<IntegrationStatusDto>();
        Assert.NotNull(status);
        Assert.True(status.IsConnected);
        Assert.NotNull(status.ConnectedAt);
        Assert.Equal("https://github.com/org/repo", status.RepoUrl);
        Assert.Contains("/webhook", status.WebhookUrl);
    }

    [Fact]
    public async Task GetIntegrationStatus_GitHubTeam_ReturnsGitHubWebhookUrl()
    {
        // Arrange
        var project = await CreateProjectAsync("Status-GitHub");
        await AssignGitHubTeamAsync(project.Id);

        // Act
        var response = await _client.GetAsync($"/api/projects/{project.Id}/integration/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<IntegrationStatusDto>();
        Assert.NotNull(status);
        Assert.Equal("GitHub", status.IntegrationType);
        Assert.Contains("github", status.WebhookUrl);
        Assert.DoesNotContain("azuredevops", status.WebhookUrl);
    }

    [Fact]
    public async Task GetIntegrationStatus_AzureDevOpsTeam_ReturnsAzureDevOpsWebhookUrl()
    {
        // Arrange
        var project = await CreateProjectAsync("Status-ADO");
        await AssignAzureDevOpsTeamAsync(project.Id);

        // Act
        var response = await _client.GetAsync($"/api/projects/{project.Id}/integration/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<IntegrationStatusDto>();
        Assert.NotNull(status);
        Assert.Equal("AzureDevOps", status.IntegrationType);
        Assert.Contains("azuredevops", status.WebhookUrl);
    }

    [Fact]
    public async Task GetIntegrationStatus_UnknownProject_Returns404()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/integration/status");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetIntegrationStatus_NeverReturnsIntegrationToken()
    {
        // Arrange — connect with a known token
        const string secret = "super-secret-token-xyz";
        var project = await CreateProjectAsync("Status-NoToken");

        await _client.PostAsJsonAsync($"/api/projects/{project.Id}/integration", new
        {
            integrationToken = secret,
            integrationRepoUrl = (string?)null,
            externalProjectId = (string?)null
        });

        // Act
        var response = await _client.GetAsync($"/api/projects/{project.Id}/integration/status");
        var raw = await response.Content.ReadAsStringAsync();

        // Assert — token must never leak
        Assert.DoesNotContain(secret, raw);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<ProjectDto> CreateProjectAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new { Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    private async Task AssignGitHubTeamAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        using var db = await dbFactory.CreateDbContextAsync();

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = "GitHub Team",
            IntegrationType = IntegrationType.GitHub,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Teams.Add(team);

        db.ProjectTeams.Add(new ProjectTeam
        {
            ProjectId = projectId,
            TeamId = team.Id,
            AssignedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private async Task AssignAzureDevOpsTeamAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        using var db = await dbFactory.CreateDbContextAsync();

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = "ADO Team",
            IntegrationType = IntegrationType.AzureDevOps,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Teams.Add(team);

        db.ProjectTeams.Add(new ProjectTeam
        {
            ProjectId = projectId,
            TeamId = team.Id,
            AssignedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    // ── Local DTOs ────────────────────────────────────────────────────────────

    private sealed record ProjectDto(Guid Id, string Name, DateTime CreatedAt, DateTime UpdatedAt);

    private sealed record ProjectSafeDto(
        Guid Id,
        string Name,
        string? Description,
        string? Goals,
        string? IntegrationRepoUrl,
        string? ExternalProjectId,
        DateTime? IntegrationConnectedAt,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private sealed record IntegrationConnectResponseDto(ProjectSafeDto Project, string WebhookUrl);

    private sealed record IntegrationStatusDto(
        bool IsConnected,
        string IntegrationType,
        DateTime? ConnectedAt,
        string WebhookUrl,
        string? RepoUrl);
}
