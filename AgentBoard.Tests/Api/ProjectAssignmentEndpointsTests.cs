using System.Net;
using System.Net.Http.Json;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Api;

/// <summary>
/// Integration tests for <see cref="AgentBoard.Api.ProjectAssignmentEndpoints"/> routes.
/// Each test uses the shared factory with an isolated InMemory database.
/// </summary>
public class ProjectAssignmentEndpointsTests : IClassFixture<AgentBoardWebFactory>
{
    private readonly HttpClient _client;

    public ProjectAssignmentEndpointsTests(AgentBoardWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── GET /api/projects/{id}/agents ─────────────────────────────────────────

    [Fact]
    public async Task GetAgents_Returns200_WithEmptyList_WhenNoneAssigned()
    {
        var project = await CreateProjectAsync("Proj A");
        var response = await _client.GetAsync($"/api/projects/{project.Id}/agents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<AgentEntryDto>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAgents_Returns200_WithAssignedAgents()
    {
        var project = await CreateProjectAsync("Proj B");
        var agentId = Guid.NewGuid();
        await _client.PostAsync($"/api/projects/{project.Id}/agents/{agentId}", null);

        var response = await _client.GetAsync($"/api/projects/{project.Id}/agents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<AgentEntryDto>>();
        Assert.NotNull(result);
        var entry = Assert.Single(result);
        Assert.Equal(agentId, entry.AgentId);
        Assert.Equal("direct", entry.Source);
    }

    // ── POST /api/projects/{id}/agents/{agentId} ──────────────────────────────

    [Fact]
    public async Task AssignAgent_Returns200_WhenProjectExists()
    {
        var project = await CreateProjectAsync("Proj C");
        var response = await _client.PostAsync($"/api/projects/{project.Id}/agents/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AssignAgent_Returns404_WhenProjectNotFound()
    {
        var response = await _client.PostAsync($"/api/projects/{Guid.NewGuid()}/agents/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignAgent_IsIdempotent_Returns200_OnDuplicate()
    {
        var project = await CreateProjectAsync("Proj D");
        var agentId = Guid.NewGuid();
        await _client.PostAsync($"/api/projects/{project.Id}/agents/{agentId}", null);
        var response = await _client.PostAsync($"/api/projects/{project.Id}/agents/{agentId}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── DELETE /api/projects/{id}/agents/{agentId} ────────────────────────────

    [Fact]
    public async Task UnassignAgent_Returns204_WhenAssigned()
    {
        var project = await CreateProjectAsync("Proj E");
        var agentId = Guid.NewGuid();
        await _client.PostAsync($"/api/projects/{project.Id}/agents/{agentId}", null);
        var response = await _client.DeleteAsync($"/api/projects/{project.Id}/agents/{agentId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UnassignAgent_Returns404_WhenNotAssigned()
    {
        var project = await CreateProjectAsync("Proj F");
        var response = await _client.DeleteAsync($"/api/projects/{project.Id}/agents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnassignAgent_Returns404_WhenProjectNotFound()
    {
        var response = await _client.DeleteAsync($"/api/projects/{Guid.NewGuid()}/agents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /api/projects/{id}/teams ──────────────────────────────────────────

    [Fact]
    public async Task GetTeams_Returns200_WithEmptyList_WhenNoneAssigned()
    {
        var project = await CreateProjectAsync("Proj G");
        var response = await _client.GetAsync($"/api/projects/{project.Id}/teams");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<TeamEntryDto>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTeams_Returns200_WithAssignedTeams()
    {
        var project = await CreateProjectAsync("Proj H");
        var teamId = Guid.NewGuid();
        await _client.PostAsync($"/api/projects/{project.Id}/teams/{teamId}", null);

        var response = await _client.GetAsync($"/api/projects/{project.Id}/teams");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<TeamEntryDto>>();
        Assert.NotNull(result);
        var entry = Assert.Single(result);
        Assert.Equal(teamId, entry.TeamId);
        Assert.True(entry.AssignedAt > DateTime.MinValue);
    }

    // ── POST /api/projects/{id}/teams/{teamId} ────────────────────────────────

    [Fact]
    public async Task AssignTeam_Returns200_WhenProjectExists()
    {
        var project = await CreateProjectAsync("Proj I");
        var response = await _client.PostAsync($"/api/projects/{project.Id}/teams/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AssignTeam_Returns404_WhenProjectNotFound()
    {
        var response = await _client.PostAsync($"/api/projects/{Guid.NewGuid()}/teams/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignTeam_IsIdempotent_Returns200_OnDuplicate()
    {
        var project = await CreateProjectAsync("Proj J");
        var teamId = Guid.NewGuid();
        await _client.PostAsync($"/api/projects/{project.Id}/teams/{teamId}", null);
        var response = await _client.PostAsync($"/api/projects/{project.Id}/teams/{teamId}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── DELETE /api/projects/{id}/teams/{teamId} ──────────────────────────────

    [Fact]
    public async Task UnassignTeam_Returns204_WhenAssigned()
    {
        var project = await CreateProjectAsync("Proj K");
        var teamId = Guid.NewGuid();
        await _client.PostAsync($"/api/projects/{project.Id}/teams/{teamId}", null);
        var response = await _client.DeleteAsync($"/api/projects/{project.Id}/teams/{teamId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UnassignTeam_Returns404_WhenNotAssigned()
    {
        var project = await CreateProjectAsync("Proj L");
        var response = await _client.DeleteAsync($"/api/projects/{project.Id}/teams/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnassignTeam_Returns404_WhenProjectNotFound()
    {
        var response = await _client.DeleteAsync($"/api/projects/{Guid.NewGuid()}/teams/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<ProjectDto> CreateProjectAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new { Name = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    private sealed record ProjectDto(Guid Id, string Name);

    private sealed record AgentEntryDto(Guid AgentId, DateTime AssignedAt, string Source);

    private sealed record TeamEntryDto(Guid TeamId, DateTime AssignedAt);
}
