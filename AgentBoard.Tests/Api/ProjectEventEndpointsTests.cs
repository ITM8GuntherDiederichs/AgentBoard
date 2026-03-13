using System.Net;
using System.Net.Http.Json;
using AgentBoard.Data.Models;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Api;

/// <summary>
/// Integration tests for <see cref="AgentBoard.Api.ProjectEventEndpoints"/> routes
/// using <see cref="AgentBoardWebFactory"/> with an isolated in-memory database.
/// </summary>
public class ProjectEventEndpointsTests : IClassFixture<AgentBoardWebFactory>
{
    private readonly HttpClient _client;

    public ProjectEventEndpointsTests(AgentBoardWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // POST /api/projects/{id}/events
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Post_Returns201_WithLocationHeader_AndEventBody()
    {
        var projectId = Guid.NewGuid();
        var request = new
        {
            AgentName = "backend-agent",
            EventType = "Progress",
            Message = "Running migrations",
            Metadata = (string?)null
        };

        var response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/events", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains($"/api/projects/{projectId}/events/", response.Headers.Location!.ToString());

        var ev = await response.Content.ReadFromJsonAsync<ProjectEventDto>();
        Assert.NotNull(ev);
        Assert.NotEqual(Guid.Empty, ev.Id);
        Assert.Equal(projectId, ev.ProjectId);
        Assert.Equal("backend-agent", ev.AgentName);
        Assert.Equal("Progress", ev.EventType);
        Assert.Equal("Running migrations", ev.Message);
    }

    [Fact]
    public async Task Post_SetsCreatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-2);
        var projectId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/events",
            new { AgentName = "a", EventType = "Note", Message = "hello" });
        response.EnsureSuccessStatusCode();

        var ev = await response.Content.ReadFromJsonAsync<ProjectEventDto>();
        Assert.NotNull(ev);
        Assert.True(ev.CreatedAt >= before);
    }

    [Fact]
    public async Task Post_AcceptsAllEventTypes()
    {
        var projectId = Guid.NewGuid();
        var types = new[] { "Progress", "Blocked", "Completed", "Error", "Note", "TestResult" };

        foreach (var eventType in types)
        {
            var response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/events",
                new { AgentName = "a", EventType = eventType, Message = $"msg for {eventType}" });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task Post_NullAgentName_IsAccepted()
    {
        var projectId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/events",
            new { AgentName = (string?)null, EventType = "Progress", Message = "no agent" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ev = await response.Content.ReadFromJsonAsync<ProjectEventDto>();
        Assert.NotNull(ev);
        Assert.Null(ev.AgentName);
    }

    // -------------------------------------------------------------------------
    // GET /api/projects/{id}/events
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_Returns200_WithList()
    {
        var projectId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/projects/{projectId}/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ProjectEventDto>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Get_ReturnsEmptyList_ForUnknownProjectId()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ProjectEventDto>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Get_ReturnsPostedEvents()
    {
        var projectId = Guid.NewGuid();

        await PostEventAsync(projectId, "Progress", "first event");
        await PostEventAsync(projectId, "Completed", "second event");

        var response = await _client.GetAsync($"/api/projects/{projectId}/events");
        var result = await response.Content.ReadFromJsonAsync<List<ProjectEventDto>>();

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Get_ReturnsEventsOrderedByDescendingCreatedAt()
    {
        var projectId = Guid.NewGuid();

        await PostEventAsync(projectId, "Progress", "first");
        await PostEventAsync(projectId, "Progress", "second");

        var response = await _client.GetAsync($"/api/projects/{projectId}/events");
        var result = await response.Content.ReadFromJsonAsync<List<ProjectEventDto>>();

        Assert.NotNull(result);
        Assert.True(result[0].CreatedAt >= result[1].CreatedAt);
    }

    [Fact]
    public async Task Get_RespectsLimitQueryParameter()
    {
        var projectId = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
            await PostEventAsync(projectId, "Progress", $"event-{i}");

        var response = await _client.GetAsync($"/api/projects/{projectId}/events?limit=3");
        var result = await response.Content.ReadFromJsonAsync<List<ProjectEventDto>>();

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<ProjectEventDto> PostEventAsync(
        Guid projectId,
        string eventType,
        string message,
        string? agentName = "test-agent",
        string? metadata = null)
    {
        var response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/events",
            new { AgentName = agentName, EventType = eventType, Message = message, Metadata = metadata });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectEventDto>())!;
    }

    /// <summary>Local DTO matching the ProjectEvent shape returned by the API.</summary>
    private sealed record ProjectEventDto(
        Guid Id,
        Guid ProjectId,
        string? AgentName,
        string EventType,
        string Message,
        string? Metadata,
        DateTime CreatedAt);
}
