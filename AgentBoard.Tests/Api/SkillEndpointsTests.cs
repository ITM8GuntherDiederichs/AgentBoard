using System.Net;
using System.Net.Http.Json;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Api;

/// <summary>
/// Integration tests for SkillEndpoints routes using WebApplicationFactory.
/// Each test uses the shared factory with an isolated InMemory database.
/// </summary>
public class SkillEndpointsTests : IClassFixture<AgentBoardWebFactory>
{
    private readonly HttpClient _client;

    public SkillEndpointsTests(AgentBoardWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── GET /api/skills ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Returns200_WithList()
    {
        var response = await _client.GetAsync("/api/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<SkillDto>>();
        Assert.NotNull(result);
    }

    // ── POST /api/skills ──────────────────────────────────────────────────────

    [Fact]
    public async Task Post_Returns201_WithLocationHeader_AndCorrectBody()
    {
        var response = await _client.PostAsJsonAsync("/api/skills",
            new { Name = "New Skill", Content = "## Content" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/skills/", response.Headers.Location!.ToString());
        var skill = await response.Content.ReadFromJsonAsync<SkillDto>();
        Assert.NotNull(skill);
        Assert.Equal("New Skill", skill.Name);
        Assert.Equal("## Content", skill.Content);
        Assert.NotEqual(Guid.Empty, skill.Id);
    }

    [Fact]
    public async Task Post_SetsCreatedAt_AndUpdatedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-2);
        var response = await _client.PostAsJsonAsync("/api/skills",
            new { Name = "Timestamp Skill", Content = "" });
        response.EnsureSuccessStatusCode();
        var skill = await response.Content.ReadFromJsonAsync<SkillDto>();
        Assert.NotNull(skill);
        Assert.True(skill.CreatedAt >= before);
        Assert.True(skill.UpdatedAt >= before);
    }

    // ── GET /api/skills/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Returns200_ForExistingSkill()
    {
        var created = await CreateSkillAsync("Find Me");
        var response = await _client.GetAsync($"/api/skills/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var skill = await response.Content.ReadFromJsonAsync<SkillDto>();
        Assert.NotNull(skill);
        Assert.Equal("Find Me", skill.Name);
    }

    [Fact]
    public async Task GetById_Returns404_ForMissingId()
    {
        var response = await _client.GetAsync($"/api/skills/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PATCH /api/skills/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task Patch_Returns200_WithUpdatedSkill()
    {
        var created = await CreateSkillAsync("Original");
        var response = await _client.PatchAsJsonAsync(
            $"/api/skills/{created.Id}",
            new { Name = "Updated Skill", Content = "Updated content" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SkillDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Skill", updated.Name);
        Assert.Equal("Updated content", updated.Content);
    }

    [Fact]
    public async Task Patch_Returns404_ForMissingId()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/skills/{Guid.NewGuid()}",
            new { Name = "Ghost" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── DELETE /api/skills/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task Delete_Returns204_ForExistingSkill()
    {
        var created = await CreateSkillAsync("Delete Me");
        var response = await _client.DeleteAsync($"/api/skills/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var getResponse = await _client.GetAsync($"/api/skills/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_ForMissingId()
    {
        var response = await _client.DeleteAsync($"/api/skills/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /api/agents/{id}/skills ───────────────────────────────────────────

    [Fact]
    public async Task GetAgentSkills_Returns200_WithEmptyList()
    {
        var response = await _client.GetAsync($"/api/agents/{Guid.NewGuid()}/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<SkillDto>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── POST /api/agents/{id}/skills ──────────────────────────────────────────

    [Fact]
    public async Task AddAgentSkill_Returns200_WhenSkillExists()
    {
        var skill = await CreateSkillAsync("Agent Skill");
        var agentId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync(
            $"/api/agents/{agentId}/skills",
            new { SkillId = skill.Id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AddAgentSkill_Returns404_WhenSkillNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agents/{Guid.NewGuid()}/skills",
            new { SkillId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddAgentSkill_IsIdempotent_Returns200_OnDuplicate()
    {
        var skill = await CreateSkillAsync("Idempotent Skill");
        var agentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/api/agents/{agentId}/skills", new { SkillId = skill.Id });
        var response = await _client.PostAsJsonAsync($"/api/agents/{agentId}/skills", new { SkillId = skill.Id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAgentSkills_Returns_AssignedSkill_AfterAdd()
    {
        var skill = await CreateSkillAsync("Assigned Skill");
        var agentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/api/agents/{agentId}/skills", new { SkillId = skill.Id });
        var response = await _client.GetAsync($"/api/agents/{agentId}/skills");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<SkillDto>>();
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(skill.Id, result[0].Id);
    }

    // ── DELETE /api/agents/{id}/skills/{skillId} ──────────────────────────────

    [Fact]
    public async Task RemoveAgentSkill_Returns204_WhenAssigned()
    {
        var skill = await CreateSkillAsync("Remove Skill");
        var agentId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/api/agents/{agentId}/skills", new { SkillId = skill.Id });
        var response = await _client.DeleteAsync($"/api/agents/{agentId}/skills/{skill.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RemoveAgentSkill_Returns404_WhenNotAssigned()
    {
        var response = await _client.DeleteAsync($"/api/agents/{Guid.NewGuid()}/skills/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /api/teams/{id}/skills ────────────────────────────────────────────

    [Fact]
    public async Task GetTeamSkills_Returns200_WithEmptyList()
    {
        var response = await _client.GetAsync($"/api/teams/{Guid.NewGuid()}/skills");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<SkillDto>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── POST /api/teams/{id}/skills ───────────────────────────────────────────

    [Fact]
    public async Task AddTeamSkill_Returns200_WhenSkillExists()
    {
        var skill = await CreateSkillAsync("Team Skill");
        var teamId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync(
            $"/api/teams/{teamId}/skills",
            new { SkillId = skill.Id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AddTeamSkill_Returns404_WhenSkillNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/teams/{Guid.NewGuid()}/skills",
            new { SkillId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddTeamSkill_IsIdempotent_Returns200_OnDuplicate()
    {
        var skill = await CreateSkillAsync("Team Idempotent Skill");
        var teamId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/api/teams/{teamId}/skills", new { SkillId = skill.Id });
        var response = await _client.PostAsJsonAsync($"/api/teams/{teamId}/skills", new { SkillId = skill.Id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTeamSkills_Returns_AssignedSkill_AfterAdd()
    {
        var skill = await CreateSkillAsync("Team Assigned Skill");
        var teamId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/api/teams/{teamId}/skills", new { SkillId = skill.Id });
        var response = await _client.GetAsync($"/api/teams/{teamId}/skills");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<SkillDto>>();
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(skill.Id, result[0].Id);
    }

    // ── DELETE /api/teams/{id}/skills/{skillId} ───────────────────────────────

    [Fact]
    public async Task RemoveTeamSkill_Returns204_WhenAssigned()
    {
        var skill = await CreateSkillAsync("Team Remove Skill");
        var teamId = Guid.NewGuid();
        await _client.PostAsJsonAsync($"/api/teams/{teamId}/skills", new { SkillId = skill.Id });
        var response = await _client.DeleteAsync($"/api/teams/{teamId}/skills/{skill.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RemoveTeamSkill_Returns404_WhenNotAssigned()
    {
        var response = await _client.DeleteAsync($"/api/teams/{Guid.NewGuid()}/skills/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<SkillDto> CreateSkillAsync(string name, string content = "")
    {
        var response = await _client.PostAsJsonAsync("/api/skills",
            new { Name = name, Content = content });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SkillDto>())!;
    }

    private sealed record SkillDto(
        Guid Id,
        string Name,
        string Content,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
