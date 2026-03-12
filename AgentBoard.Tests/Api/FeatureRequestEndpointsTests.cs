using System.Net;
using System.Net.Http.Json;
using AgentBoard.Data.Models;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Api;

/// <summary>
/// Integration tests for FeatureRequestEndpoints routes using WebApplicationFactory.
/// Each test uses the shared factory with an isolated InMemory database.
/// </summary>
public class FeatureRequestEndpointsTests : IClassFixture<AgentBoardWebFactory>
{
    private readonly HttpClient _client;

    public FeatureRequestEndpointsTests(AgentBoardWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // GET /api/projects/{projectId}/features
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAll_Returns200_WithList()
    {
        var projectId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/projects/{projectId}/features");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<FeatureRequestDto>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyFeaturesForProject()
    {
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();

        await CreateFeatureAsync(projectId, "Feature A");
        await CreateFeatureAsync(projectId, "Feature B");
        await CreateFeatureAsync(otherProjectId, "Other Feature");

        var response = await _client.GetAsync($"/api/projects/{projectId}/features");
        var result = await response.Content.ReadFromJsonAsync<List<FeatureRequestDto>>();

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.Equal(projectId, f.ProjectId));
    }

    // -------------------------------------------------------------------------
    // POST /api/projects/{projectId}/features
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Post_Returns201_WithLocationHeader_AndCorrectBody()
    {
        var projectId = Guid.NewGuid();
        var request = new { Title = "New Feature", Description = "Feature description", Priority = 2, Status = 0 };

        var response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/features", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains($"/api/projects/{projectId}/features/", response.Headers.Location!.ToString());

        var feature = await response.Content.ReadFromJsonAsync<FeatureRequestDto>();
        Assert.NotNull(feature);
        Assert.Equal("New Feature", feature.Title);
        Assert.Equal("Feature description", feature.Description);
        Assert.Equal(projectId, feature.ProjectId);
        Assert.NotEqual(Guid.Empty, feature.Id);
    }

    [Fact]
    public async Task Post_SetsCreatedAt_AndUpdatedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-2);
        var projectId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/features",
            new { Title = "Timestamp Test" });
        response.EnsureSuccessStatusCode();

        var feature = await response.Content.ReadFromJsonAsync<FeatureRequestDto>();
        Assert.NotNull(feature);
        Assert.True(feature.CreatedAt >= before);
        Assert.True(feature.UpdatedAt >= before);
    }

    [Fact]
    public async Task Post_SetsProjectId_FromRoute()
    {
        var projectId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/features",
            new { Title = "Route Test" });
        response.EnsureSuccessStatusCode();

        var feature = await response.Content.ReadFromJsonAsync<FeatureRequestDto>();
        Assert.NotNull(feature);
        Assert.Equal(projectId, feature.ProjectId);
    }

    // -------------------------------------------------------------------------
    // PATCH /api/projects/{projectId}/features/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Patch_Returns200_WithUpdatedFeature()
    {
        var projectId = Guid.NewGuid();
        var created = await CreateFeatureAsync(projectId, "Original");

        var response = await _client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/features/{created.Id}",
            new { Title = "Updated Feature", Status = 2 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<FeatureRequestDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Feature", updated.Title);
        Assert.Equal(2, updated.Status);
    }

    [Fact]
    public async Task Patch_Returns404_ForMissingId()
    {
        var projectId = Guid.NewGuid();
        var response = await _client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/features/{Guid.NewGuid()}",
            new { Title = "Ghost" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // DELETE /api/projects/{projectId}/features/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Returns204_ForExistingFeature()
    {
        var projectId = Guid.NewGuid();
        var created = await CreateFeatureAsync(projectId, "Delete Me");

        var response = await _client.DeleteAsync($"/api/projects/{projectId}/features/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify it is gone from the list
        var listResponse = await _client.GetAsync($"/api/projects/{projectId}/features");
        var remaining = await listResponse.Content.ReadFromJsonAsync<List<FeatureRequestDto>>();
        Assert.NotNull(remaining);
        Assert.DoesNotContain(remaining, f => f.Id == created.Id);
    }

    [Fact]
    public async Task Delete_Returns404_ForMissingId()
    {
        var projectId = Guid.NewGuid();
        var response = await _client.DeleteAsync($"/api/projects/{projectId}/features/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<FeatureRequestDto> CreateFeatureAsync(
        Guid projectId,
        string title,
        string? description = null,
        int priority = 1,
        int status = 0)
    {
        var response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/features",
            new { Title = title, Description = description, Priority = priority, Status = status });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FeatureRequestDto>())!;
    }

    /// <summary>Local DTO matching the FeatureRequest entity shape returned by the API.</summary>
    private sealed record FeatureRequestDto(
        Guid Id,
        Guid ProjectId,
        string Title,
        string? Description,
        int Priority,
        int Status,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
