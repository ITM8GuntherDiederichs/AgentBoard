using System.Net;
using System.Net.Http.Json;
using AgentBoard.Data.Models;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Api;

/// <summary>
/// Integration tests for AgentEndpoints routes using WebApplicationFactory.
/// Each test uses the shared factory with an isolated InMemory database.
/// </summary>
public class AgentEndpointsTests : IClassFixture<AgentBoardWebFactory>
{
    private readonly HttpClient _client;

    public AgentEndpointsTests(AgentBoardWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_Returns200_WithList()
    {
        var response = await _client.GetAsync("/api/agents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<AgentDto>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAll_WithAvailableOnlyTrue_ReturnsOnlyAvailableAgents()
    {
        await CreateAgentAsync("Available Agent", isAvailable: true);
        await CreateAgentAsync("Unavailable Agent", isAvailable: false);
        var response = await _client.GetAsync("/api/agents?availableOnly=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<AgentDto>>();
        Assert.NotNull(result);
        Assert.All(result, a => Assert.True(a.IsAvailable));
    }

    [Fact]
    public async Task Post_Returns201_WithLocationHeader_AndCorrectBody()
    {
        var request = new { Name = "New Agent", Type = 0, Description = "An AI agent", IsAvailable = true };
        var response = await _client.PostAsJsonAsync("/api/agents", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/agents/", response.Headers.Location!.ToString());
        var agent = await response.Content.ReadFromJsonAsync<AgentDto>();
        Assert.NotNull(agent);
        Assert.Equal("New Agent", agent.Name);
        Assert.Equal("An AI agent", agent.Description);
        Assert.True(agent.IsAvailable);
        Assert.NotEqual(Guid.Empty, agent.Id);
    }

    [Fact]
    public async Task Post_SetsCreatedAt_AndUpdatedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-2);
        var response = await _client.PostAsJsonAsync("/api/agents",
            new { Name = "Timestamp Agent", Type = 0 });
        response.EnsureSuccessStatusCode();
        var agent = await response.Content.ReadFromJsonAsync<AgentDto>();
        Assert.NotNull(agent);
        Assert.True(agent.CreatedAt >= before);
        Assert.True(agent.UpdatedAt >= before);
    }

    [Fact]
    public async Task Post_SetsType_FromBody()
    {
        var response = await _client.PostAsJsonAsync("/api/agents",
            new { Name = "Human Agent", Type = 1 });
        response.EnsureSuccessStatusCode();
        var agent = await response.Content.ReadFromJsonAsync<AgentDto>();
        Assert.NotNull(agent);
        Assert.Equal(1, agent.Type);
    }

    [Fact]
    public async Task GetById_Returns200_ForExistingAgent()
    {
        var created = await CreateAgentAsync("Find Me");
        var response = await _client.GetAsync($"/api/agents/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var agent = await response.Content.ReadFromJsonAsync<AgentDto>();
        Assert.NotNull(agent);
        Assert.Equal("Find Me", agent.Name);
    }

    [Fact]
    public async Task GetById_Returns404_ForMissingId()
    {
        var response = await _client.GetAsync($"/api/agents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Patch_Returns200_WithUpdatedAgent()
    {
        var created = await CreateAgentAsync("Original");
        var response = await _client.PatchAsJsonAsync(
            $"/api/agents/{created.Id}",
            new { Name = "Updated Agent", IsAvailable = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AgentDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Agent", updated.Name);
        Assert.False(updated.IsAvailable);
    }

    [Fact]
    public async Task Patch_Returns404_ForMissingId()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/agents/{Guid.NewGuid()}",
            new { Name = "Ghost" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns204_ForExistingAgent()
    {
        var created = await CreateAgentAsync("Delete Me");
        var response = await _client.DeleteAsync($"/api/agents/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var getResponse = await _client.GetAsync($"/api/agents/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_ForMissingId()
    {
        var response = await _client.DeleteAsync($"/api/agents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<AgentDto> CreateAgentAsync(
        string name,
        int type = 0,
        string? description = null,
        bool isAvailable = true)
    {
        var response = await _client.PostAsJsonAsync("/api/agents",
            new { Name = name, Type = type, Description = description, IsAvailable = isAvailable });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentDto>())!;
    }

    private sealed record AgentDto(
        Guid Id,
        string Name,
        int Type,
        string? Description,
        bool IsAvailable,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}