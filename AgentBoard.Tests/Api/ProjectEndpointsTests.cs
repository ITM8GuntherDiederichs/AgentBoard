using System.Net;
using System.Net.Http.Json;
using AgentBoard.Data.Models;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Api;

/// <summary>
/// Integration tests for all 5 ProjectEndpoints routes using WebApplicationFactory.
/// Each test uses the shared factory with an isolated InMemory database.
/// </summary>
public class ProjectEndpointsTests : IClassFixture<AgentBoardWebFactory>
{
    private readonly HttpClient _client;

    public ProjectEndpointsTests(AgentBoardWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // GET /api/projects
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAll_Returns200_WithList()
    {
        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ProjectDto>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAll_IncludesCreatedProject()
    {
        var uniqueName = "GetAll-" + Guid.NewGuid().ToString("N");
        await CreateProjectAsync(uniqueName);

        var response = await _client.GetAsync("/api/projects");
        var result = await response.Content.ReadFromJsonAsync<List<ProjectDto>>();

        Assert.NotNull(result);
        Assert.Contains(result, p => p.Name == uniqueName);
    }

    // -------------------------------------------------------------------------
    // POST /api/projects
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Post_Returns201_WithLocationHeader_AndCorrectBody()
    {
        var request = new { Name = "New Project", Description = "A description", Goals = "Some goals" };

        var response = await _client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/projects/", response.Headers.Location!.ToString());

        var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);
        Assert.Equal("New Project", project.Name);
        Assert.Equal("A description", project.Description);
        Assert.Equal("Some goals", project.Goals);
        Assert.NotEqual(Guid.Empty, project.Id);
    }

    [Fact]
    public async Task Post_SetsCreatedAt_AndUpdatedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-2);

        var response = await _client.PostAsJsonAsync("/api/projects", new { Name = "Timestamps Test" });
        response.EnsureSuccessStatusCode();

        var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);
        Assert.True(project.CreatedAt >= before);
        Assert.True(project.UpdatedAt >= before);
    }

    // -------------------------------------------------------------------------
    // GET /api/projects/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetById_Returns200_ForExistingProject()
    {
        var created = await CreateProjectAsync("GetById Project");

        var response = await _client.GetAsync($"/api/projects/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);
        Assert.Equal("GetById Project", project.Name);
    }

    [Fact]
    public async Task GetById_Returns404_ForMissingId()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PUT /api/projects/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Put_Returns200_WithUpdatedProject()
    {
        var created = await CreateProjectAsync("Original Project");
        var updateRequest = new { Name = "Updated Project", Description = "New desc", Goals = "New goals" };

        var response = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);
        Assert.Equal("Updated Project", project.Name);
        Assert.Equal("New desc", project.Description);
        Assert.Equal("New goals", project.Goals);
    }

    [Fact]
    public async Task Put_Returns404_ForMissingId()
    {
        var response = await _client.PutAsJsonAsync($"/api/projects/{Guid.NewGuid()}",
            new { Name = "Ghost Project" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // DELETE /api/projects/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Returns204_ForExistingProject()
    {
        var created = await CreateProjectAsync("Delete Me");

        var response = await _client.DeleteAsync($"/api/projects/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify it is gone
        var getResponse = await _client.GetAsync($"/api/projects/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_ForMissingId()
    {
        var response = await _client.DeleteAsync($"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<ProjectDto> CreateProjectAsync(string name, string? description = null, string? goals = null)
    {
        var response = await _client.PostAsJsonAsync("/api/projects",
            new { Name = name, Description = description, Goals = goals });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    /// <summary>Local DTO matching the Project entity shape returned by the API.</summary>
    private sealed record ProjectDto(
        Guid Id,
        string Name,
        string? Description,
        string? Goals,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
