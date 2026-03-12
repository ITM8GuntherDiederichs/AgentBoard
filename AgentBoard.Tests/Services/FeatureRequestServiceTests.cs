using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Services;

public class FeatureRequestServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static FeatureRequestService BuildService(string? dbName = null)
        => new(TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString()));

    private static FeatureRequest MakeFeatureRequest(
        Guid? projectId = null,
        string title = "Test Feature",
        string? description = null,
        TodoPriority priority = TodoPriority.Medium,
        FeatureRequestStatus status = FeatureRequestStatus.Proposed)
        => new()
        {
            ProjectId = projectId ?? Guid.NewGuid(),
            Title = title,
            Description = description,
            Priority = priority,
            Status = status
        };

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_SetsId_ToNonEmpty()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeFeatureRequest());
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateAsync_SetsTitle()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeFeatureRequest(title: "My Feature"));
        Assert.Equal("My Feature", result.Title);
    }

    [Fact]
    public async Task CreateAsync_SetsProjectId()
    {
        var projectId = Guid.NewGuid();
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeFeatureRequest(projectId: projectId));
        Assert.Equal(projectId, result.ProjectId);
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeFeatureRequest());
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.CreatedAt, before, after);
    }

    [Fact]
    public async Task CreateAsync_SetsUpdatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeFeatureRequest());
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.UpdatedAt, before, after);
    }

    [Fact]
    public async Task CreateAsync_SetsDefaultStatus_ToProposed()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeFeatureRequest(status: FeatureRequestStatus.Proposed));
        Assert.Equal(FeatureRequestStatus.Proposed, result.Status);
    }

    // -------------------------------------------------------------------------
    // GetByProjectAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByProjectAsync_ReturnsOnlyItemsForProject()
    {
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var svc = BuildService();

        await svc.CreateAsync(MakeFeatureRequest(projectId: projectId, title: "Feature A"));
        await svc.CreateAsync(MakeFeatureRequest(projectId: projectId, title: "Feature B"));
        await svc.CreateAsync(MakeFeatureRequest(projectId: otherProjectId, title: "Other Feature"));

        var result = await svc.GetByProjectAsync(projectId);

        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.Equal(projectId, f.ProjectId));
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsEmptyList_WhenNoFeatures()
    {
        var svc = BuildService();
        var result = await svc.GetByProjectAsync(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByProjectAsync_OrdersByPriorityDescending_ThenCreatedAscending()
    {
        var projectId = Guid.NewGuid();
        var svc = BuildService();

        await svc.CreateAsync(MakeFeatureRequest(projectId: projectId, title: "Low", priority: TodoPriority.Low));
        await svc.CreateAsync(MakeFeatureRequest(projectId: projectId, title: "High", priority: TodoPriority.High));
        await svc.CreateAsync(MakeFeatureRequest(projectId: projectId, title: "Medium", priority: TodoPriority.Medium));

        var result = await svc.GetByProjectAsync(projectId);

        Assert.Equal(new[] { "High", "Medium", "Low" }, result.Select(f => f.Title).ToArray());
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ReturnsFeatureRequest_WhenFound()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeFeatureRequest(title: "Find Me"));

        var result = await svc.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal("Find Me", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var svc = BuildService();
        var result = await svc.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // PatchAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PatchAsync_UpdatesTitle_WhenProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeFeatureRequest(title: "Original"));

        var updated = await svc.PatchAsync(created.Id, new FeatureRequestPatch("Updated Title", null, null, null));

        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
    }

    [Fact]
    public async Task PatchAsync_UpdatesStatus_WhenProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeFeatureRequest(status: FeatureRequestStatus.Proposed));

        var updated = await svc.PatchAsync(created.Id, new FeatureRequestPatch(null, null, null, FeatureRequestStatus.InProgress));

        Assert.NotNull(updated);
        Assert.Equal(FeatureRequestStatus.InProgress, updated.Status);
    }

    [Fact]
    public async Task PatchAsync_UpdatesPriority_WhenProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeFeatureRequest(priority: TodoPriority.Low));

        var updated = await svc.PatchAsync(created.Id, new FeatureRequestPatch(null, null, TodoPriority.Critical, null));

        Assert.NotNull(updated);
        Assert.Equal(TodoPriority.Critical, updated.Priority);
    }

    [Fact]
    public async Task PatchAsync_DoesNotChangeTitle_WhenTitleIsNull()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeFeatureRequest(title: "Keep This"));

        var updated = await svc.PatchAsync(created.Id, new FeatureRequestPatch(null, null, null, null));

        Assert.NotNull(updated);
        Assert.Equal("Keep This", updated.Title);
    }

    [Fact]
    public async Task PatchAsync_SetsUpdatedAt_ToAfterCreatedAt()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeFeatureRequest());

        await Task.Delay(10);
        var updated = await svc.PatchAsync(created.Id, new FeatureRequestPatch(null, null, null, FeatureRequestStatus.Planned));

        Assert.NotNull(updated);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task PatchAsync_ReturnsNull_WhenNotFound()
    {
        var svc = BuildService();
        var result = await svc.PatchAsync(Guid.NewGuid(), new FeatureRequestPatch("X", null, null, null));
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesFeatureRequest_ReturnsTrue()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeFeatureRequest());

        var deleted = await svc.DeleteAsync(created.Id);

        Assert.True(deleted);
        Assert.Null(await svc.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        var svc = BuildService();
        var result = await svc.DeleteAsync(Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotAffectOtherFeatureRequests()
    {
        var projectId = Guid.NewGuid();
        var svc = BuildService();
        var f1 = await svc.CreateAsync(MakeFeatureRequest(projectId: projectId, title: "Keep Me"));
        var f2 = await svc.CreateAsync(MakeFeatureRequest(projectId: projectId, title: "Delete Me"));

        await svc.DeleteAsync(f2.Id);

        var remaining = await svc.GetByProjectAsync(projectId);
        Assert.Single(remaining);
        Assert.Equal("Keep Me", remaining[0].Title);
    }
}
