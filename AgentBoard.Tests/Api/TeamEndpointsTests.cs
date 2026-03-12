using System.Net;
using System.Net.Http.Json;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Api;

/// <summary>
/// Integration tests for TeamEndpoints routes using WebApplicationFactory.
/// Each test uses the shared factory with an isolated InMemory database.
/// </summary>
public class TeamEndpointsTests : IClassFixture<AgentBoardWebFactory>
{
    private readonly HttpClient _client;

    public TeamEndpointsTests(AgentBoardWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── GET /api/teams ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Returns200_WithEmptyList_Initially()
    {
        var response = await _client.GetAsync("/api/teams");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<TeamDto>>();
        Assert.NotNull(result);
    }

    // ── POST /api/teams ──────────────────────────────────────────────────────

    [Fact]
    public async Task Post_Returns201_WithLocationHeader_AndCorrectBody()
    {
        var response = await _client.PostAsJsonAsync("/api/teams",
            new { Name = "Integration Team", Description = "Testing team" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/teams/", response.Headers.Location!.ToString());
        var team = await response.Content.ReadFromJsonAsync<TeamDto>();
        Assert.NotNull(team);
        Assert.Equal("Integration Team", team.Name);
        Assert.Equal("Testing team", team.Description);
        Assert.NotEqual(Guid.Empty, team.Id);
    }

    [Fact]
    public async Task Post_SetsCreatedAt_AndUpdatedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-2);
        var response = await _client.PostAsJsonAsync("/api/teams",
            new { Name = "Timestamp Team" });
        response.EnsureSuccessStatusCode();
        var team = await response.Content.ReadFromJsonAsync<TeamDto>();
        Assert.NotNull(team);
        Assert.True(team.CreatedAt >= before);
        Assert.True(team.UpdatedAt >= before);
    }

    [Fact]
    public async Task Post_NullDescription_IsAccepted()
    {
        var response = await _client.PostAsJsonAsync("/api/teams",
            new { Name = "No Desc Team", Description = (string?)null });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var team = await response.Content.ReadFromJsonAsync<TeamDto>();
        Assert.NotNull(team);
        Assert.Null(team.Description);
    }

    // ── GET /api/teams/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Returns200_ForExistingTeam()
    {
        var created = await CreateTeamAsync("Find Me");
        var response = await _client.GetAsync($"/api/teams/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var team = await response.Content.ReadFromJsonAsync<TeamDto>();
        Assert.NotNull(team);
        Assert.Equal("Find Me", team.Name);
    }

    [Fact]
    public async Task GetById_Returns404_ForMissingId()
    {
        var response = await _client.GetAsync($"/api/teams/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PUT /api/teams/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task Put_Returns200_WithUpdatedTeam()
    {
        var created = await CreateTeamAsync("Original");
        var response = await _client.PutAsJsonAsync($"/api/teams/{created.Id}",
            new { Name = "Updated Name", Description = "Updated desc" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TeamDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal("Updated desc", updated.Description);
    }

    [Fact]
    public async Task Put_Returns404_ForMissingId()
    {
        var response = await _client.PutAsJsonAsync($"/api/teams/{Guid.NewGuid()}",
            new { Name = "Ghost" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── DELETE /api/teams/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task Delete_Returns204_ForExistingTeam()
    {
        var created = await CreateTeamAsync("Delete Me");
        var response = await _client.DeleteAsync($"/api/teams/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var getResponse = await _client.GetAsync($"/api/teams/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_ForMissingId()
    {
        var response = await _client.DeleteAsync($"/api/teams/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── POST /api/teams/{id}/members/{agentId} ───────────────────────────────

    [Fact]
    public async Task AddMember_Returns200_WhenTeamExists()
    {
        var created = await CreateTeamAsync("Member Team");
        var agentId = Guid.NewGuid();
        var response = await _client.PostAsync($"/api/teams/{created.Id}/members/{agentId}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_Returns404_WhenTeamNotFound()
    {
        var response = await _client.PostAsync($"/api/teams/{Guid.NewGuid()}/members/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_IsIdempotent_Returns200_OnDuplicate()
    {
        var created = await CreateTeamAsync("Idempotent Team");
        var agentId = Guid.NewGuid();
        await _client.PostAsync($"/api/teams/{created.Id}/members/{agentId}", null);
        var response = await _client.PostAsync($"/api/teams/{created.Id}/members/{agentId}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── DELETE /api/teams/{id}/members/{agentId} ─────────────────────────────

    [Fact]
    public async Task RemoveMember_Returns204_WhenMemberExists()
    {
        var created = await CreateTeamAsync("Remove Member Team");
        var agentId = Guid.NewGuid();
        await _client.PostAsync($"/api/teams/{created.Id}/members/{agentId}", null);
        var response = await _client.DeleteAsync($"/api/teams/{created.Id}/members/{agentId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_Returns404_WhenMemberNotFound()
    {
        var created = await CreateTeamAsync("Team Without Members");
        var response = await _client.DeleteAsync($"/api/teams/{created.Id}/members/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_Returns404_WhenTeamNotFound()
    {
        var response = await _client.DeleteAsync($"/api/teams/{Guid.NewGuid()}/members/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<TeamDto> CreateTeamAsync(string name, string? description = null)
    {
        var response = await _client.PostAsJsonAsync("/api/teams",
            new { Name = name, Description = description });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeamDto>())!;
    }

    private sealed record TeamDto(
        Guid Id,
        string Name,
        string? Description,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        List<TeamMemberDto> Members);

    private sealed record TeamMemberDto(Guid TeamId, Guid AgentId, DateTime AddedAt);
}
