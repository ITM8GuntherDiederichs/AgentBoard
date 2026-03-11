using AgentBoard.Data.Models;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Services;

public class ProjectServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ProjectService BuildService(string? dbName = null)
        => new(TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString()));

    private static Project MakeProject(
        string name = "Test Project",
        string? description = null,
        string? goals = null)
        => new() { Name = name, Description = description, Goals = goals };

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_SetsId_ToNonEmpty()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeProject());
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateAsync_SetsName()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeProject(name: "My Project"));
        Assert.Equal("My Project", result.Name);
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeProject());
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.CreatedAt, before, after);
    }

    [Fact]
    public async Task CreateAsync_SetsUpdatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeProject());
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.UpdatedAt, before, after);
    }

    [Fact]
    public async Task CreateAsync_SetsDescription_AndGoals()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeProject(description: "Desc", goals: "Goals"));
        Assert.Equal("Desc", result.Description);
        Assert.Equal("Goals", result.Goals);
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_ReturnsAllProjects()
    {
        var svc = BuildService();
        await svc.CreateAsync(MakeProject("Alpha"));
        await svc.CreateAsync(MakeProject("Beta"));

        var result = await svc.GetAllAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoProjects()
    {
        var svc = BuildService();
        var result = await svc.GetAllAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsProjectsOrderedByName()
    {
        var svc = BuildService();
        await svc.CreateAsync(MakeProject("Zebra"));
        await svc.CreateAsync(MakeProject("Alpha"));
        await svc.CreateAsync(MakeProject("Mango"));

        var result = await svc.GetAllAsync();

        Assert.Equal(new[] { "Alpha", "Mango", "Zebra" }, result.Select(p => p.Name).ToArray());
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ReturnsProject_WhenFound()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeProject("Find Me"));

        var result = await svc.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal("Find Me", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var svc = BuildService();
        var result = await svc.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_UpdatesAllFields()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeProject("Original"));

        var updated = await svc.UpdateAsync(new Project
        {
            Id = created.Id,
            Name = "Updated Name",
            Description = "Updated Desc",
            Goals = "Updated Goals"
        });

        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal("Updated Desc", updated.Description);
        Assert.Equal("Updated Goals", updated.Goals);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdatedAt_ToAfterCreatedAt()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeProject("Timing Test"));

        await Task.Delay(10); // ensure time difference
        var updated = await svc.UpdateAsync(new Project
        {
            Id = created.Id,
            Name = "Timing Test Updated"
        });

        Assert.NotNull(updated);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotFound()
    {
        var svc = BuildService();
        var result = await svc.UpdateAsync(new Project { Id = Guid.NewGuid(), Name = "Ghost" });
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesProject_ReturnsTrue()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeProject());

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
    public async Task DeleteAsync_DoesNotAffectOtherProjects()
    {
        var svc = BuildService();
        var p1 = await svc.CreateAsync(MakeProject("Keep Me"));
        var p2 = await svc.CreateAsync(MakeProject("Delete Me"));

        await svc.DeleteAsync(p2.Id);

        var all = await svc.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Keep Me", all[0].Name);
    }
}
