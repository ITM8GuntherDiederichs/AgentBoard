using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Services;

public class AgentServiceTests
{
    private static AgentService BuildService(string? dbName = null)
        => new(TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString()));

    private static Agent MakeAgent(
        string name = "Test Agent",
        AgentType type = AgentType.AI,
        string? description = null,
        bool isAvailable = true)
        => new()
        {
            Name = name,
            Type = type,
            Description = description,
            IsAvailable = isAvailable
        };

    [Fact]
    public async Task CreateAsync_SetsId_ToNonEmpty()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeAgent());
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateAsync_SetsName()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeAgent(name: "My Agent"));
        Assert.Equal("My Agent", result.Name);
    }

    [Fact]
    public async Task CreateAsync_SetsType()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeAgent(type: AgentType.Human));
        Assert.Equal(AgentType.Human, result.Type);
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeAgent());
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.CreatedAt, before, after);
    }

    [Fact]
    public async Task CreateAsync_SetsUpdatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeAgent());
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.UpdatedAt, before, after);
    }

    [Fact]
    public async Task CreateAsync_DefaultsIsAvailable_ToTrue()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeAgent());
        Assert.True(result.IsAvailable);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllAgents_WhenNoFilter()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        await svc.CreateAsync(MakeAgent(name: "Agent A", isAvailable: true));
        await svc.CreateAsync(MakeAgent(name: "Agent B", isAvailable: false));
        var result = await svc.GetAllAsync();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_FiltersAvailable_WhenAvailableOnlyTrue()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        await svc.CreateAsync(MakeAgent(name: "Available", isAvailable: true));
        await svc.CreateAsync(MakeAgent(name: "Unavailable", isAvailable: false));
        var result = await svc.GetAllAsync(availableOnly: true);
        Assert.Single(result);
        Assert.True(result[0].IsAvailable);
    }

    [Fact]
    public async Task GetAllAsync_FiltersUnavailable_WhenAvailableOnlyFalse()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        await svc.CreateAsync(MakeAgent(name: "Available", isAvailable: true));
        await svc.CreateAsync(MakeAgent(name: "Unavailable", isAvailable: false));
        var result = await svc.GetAllAsync(availableOnly: false);
        Assert.Single(result);
        Assert.False(result[0].IsAvailable);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoAgents()
    {
        var svc = BuildService();
        var result = await svc.GetAllAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsAgent_WhenFound()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeAgent(name: "Find Me"));
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

    [Fact]
    public async Task UpdateAsync_SetsUpdatedAt_ToAfterCreatedAt()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeAgent());
        await Task.Delay(10);
        created.Name = "Updated Name";
        var updated = await svc.UpdateAsync(created);
        Assert.True(updated.UpdatedAt >= created.CreatedAt);
        Assert.Equal("Updated Name", updated.Name);
    }

    [Fact]
    public async Task PatchAsync_UpdatesName_WhenProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeAgent(name: "Original"));
        var updated = await svc.PatchAsync(created.Id, new AgentPatch("Updated Name", null, null, null));
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
    }

    [Fact]
    public async Task PatchAsync_UpdatesType_WhenProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeAgent(type: AgentType.AI));
        var updated = await svc.PatchAsync(created.Id, new AgentPatch(null, null, AgentType.Human, null));
        Assert.NotNull(updated);
        Assert.Equal(AgentType.Human, updated.Type);
    }

    [Fact]
    public async Task PatchAsync_UpdatesIsAvailable_WhenProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeAgent(isAvailable: true));
        var updated = await svc.PatchAsync(created.Id, new AgentPatch(null, null, null, false));
        Assert.NotNull(updated);
        Assert.False(updated.IsAvailable);
    }

    [Fact]
    public async Task PatchAsync_DoesNotChangeName_WhenNameIsNull()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeAgent(name: "Keep This"));
        var updated = await svc.PatchAsync(created.Id, new AgentPatch(null, null, null, null));
        Assert.NotNull(updated);
        Assert.Equal("Keep This", updated.Name);
    }

    [Fact]
    public async Task PatchAsync_SetsUpdatedAt_ToAfterCreatedAt()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeAgent());
        await Task.Delay(10);
        var updated = await svc.PatchAsync(created.Id, new AgentPatch(null, null, null, false));
        Assert.NotNull(updated);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task PatchAsync_ReturnsNull_WhenNotFound()
    {
        var svc = BuildService();
        var result = await svc.PatchAsync(Guid.NewGuid(), new AgentPatch("X", null, null, null));
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAgent_ReturnsTrue()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeAgent());
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
    public async Task DeleteAsync_DoesNotAffectOtherAgents()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var a1 = await svc.CreateAsync(MakeAgent(name: "Keep Me"));
        var a2 = await svc.CreateAsync(MakeAgent(name: "Delete Me"));
        await svc.DeleteAsync(a2.Id);
        var remaining = await svc.GetAllAsync();
        Assert.Single(remaining);
        Assert.Equal("Keep Me", remaining[0].Name);
    }
}