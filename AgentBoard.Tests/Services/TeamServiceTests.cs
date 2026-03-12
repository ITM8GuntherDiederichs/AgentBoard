using AgentBoard.Data.Models;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Services;

/// <summary>Unit tests for <see cref="TeamService"/>.</summary>
public class TeamServiceTests
{
    private static TeamService BuildService(string? dbName = null)
        => new(TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString()));

    private static Team MakeTeam(string name = "Alpha Team", string? description = null)
        => new() { Name = name, Description = description };

    // ── CreateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_SetsId_ToNonEmpty()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeTeam());
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateAsync_SetsName()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeTeam("My Team"));
        Assert.Equal("My Team", result.Name);
    }

    [Fact]
    public async Task CreateAsync_SetsDescription()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeTeam(description: "A description"));
        Assert.Equal("A description", result.Description);
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeTeam());
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.CreatedAt, before, after);
    }

    [Fact]
    public async Task CreateAsync_SetsUpdatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeTeam());
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.UpdatedAt, before, after);
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoTeams()
    {
        var svc = BuildService();
        var result = await svc.GetAllAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllTeams()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        await svc.CreateAsync(MakeTeam("Team A"));
        await svc.CreateAsync(MakeTeam("Team B"));
        var result = await svc.GetAllAsync();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_IncludesMembers()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var team = await svc.CreateAsync(MakeTeam());
        var agentId = Guid.NewGuid();
        await svc.AddMemberAsync(team.Id, agentId);
        var result = await svc.GetAllAsync();
        Assert.Single(result[0].Members);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsTeam_WhenFound()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeTeam("Find Me"));
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
    public async Task GetByIdAsync_IncludesMembers()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var team = await svc.CreateAsync(MakeTeam());
        var agentId = Guid.NewGuid();
        await svc.AddMemberAsync(team.Id, agentId);
        var result = await svc.GetByIdAsync(team.Id);
        Assert.NotNull(result);
        Assert.Single(result.Members);
        Assert.Equal(agentId, result.Members.First().AgentId);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_UpdatesName()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeTeam("Old Name"));
        created.Name = "New Name";
        var updated = await svc.UpdateAsync(created);
        Assert.NotNull(updated);
        Assert.Equal("New Name", updated.Name);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesDescription()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeTeam(description: "Old"));
        created.Description = "New";
        var updated = await svc.UpdateAsync(created);
        Assert.NotNull(updated);
        Assert.Equal("New", updated.Description);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdatedAt()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeTeam());
        await Task.Delay(10);
        created.Name = "Updated";
        var updated = await svc.UpdateAsync(created);
        Assert.NotNull(updated);
        Assert.True(updated.UpdatedAt >= created.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotFound()
    {
        var svc = BuildService();
        var result = await svc.UpdateAsync(new Team { Id = Guid.NewGuid(), Name = "Ghost" });
        Assert.Null(result);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesTeam_ReturnsTrue()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeTeam());
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
    public async Task DeleteAsync_CascadesMembers()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var team = await svc.CreateAsync(MakeTeam());
        await svc.AddMemberAsync(team.Id, Guid.NewGuid());
        await svc.DeleteAsync(team.Id);
        // After delete, team is gone — members are cascaded
        var result = await svc.GetByIdAsync(team.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotAffectOtherTeams()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var t1 = await svc.CreateAsync(MakeTeam("Keep Me"));
        var t2 = await svc.CreateAsync(MakeTeam("Delete Me"));
        await svc.DeleteAsync(t2.Id);
        var remaining = await svc.GetAllAsync();
        Assert.Single(remaining);
        Assert.Equal("Keep Me", remaining[0].Name);
    }

    // ── AddMemberAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task AddMemberAsync_ReturnsFalse_WhenTeamNotFound()
    {
        var svc = BuildService();
        var result = await svc.AddMemberAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsTrue_WhenAdded()
    {
        var svc = BuildService();
        var team = await svc.CreateAsync(MakeTeam());
        var result = await svc.AddMemberAsync(team.Id, Guid.NewGuid());
        Assert.True(result);
    }

    [Fact]
    public async Task AddMemberAsync_IsIdempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var team = await svc.CreateAsync(MakeTeam());
        var agentId = Guid.NewGuid();
        await svc.AddMemberAsync(team.Id, agentId);
        var result = await svc.AddMemberAsync(team.Id, agentId);
        Assert.True(result);
        var found = await svc.GetByIdAsync(team.Id);
        Assert.Single(found!.Members);
    }

    [Fact]
    public async Task AddMemberAsync_StoresMember_WithAgentId()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var team = await svc.CreateAsync(MakeTeam());
        var agentId = Guid.NewGuid();
        await svc.AddMemberAsync(team.Id, agentId);
        var found = await svc.GetByIdAsync(team.Id);
        Assert.NotNull(found);
        Assert.Equal(agentId, found.Members.Single().AgentId);
    }

    // ── RemoveMemberAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveMemberAsync_ReturnsFalse_WhenTeamNotFound()
    {
        var svc = BuildService();
        var result = await svc.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsFalse_WhenMemberNotFound()
    {
        var svc = BuildService();
        var team = await svc.CreateAsync(MakeTeam());
        var result = await svc.RemoveMemberAsync(team.Id, Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsTrue_WhenRemoved()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var team = await svc.CreateAsync(MakeTeam());
        var agentId = Guid.NewGuid();
        await svc.AddMemberAsync(team.Id, agentId);
        var result = await svc.RemoveMemberAsync(team.Id, agentId);
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveMemberAsync_MemberIsGone_AfterRemoval()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var team = await svc.CreateAsync(MakeTeam());
        var agentId = Guid.NewGuid();
        await svc.AddMemberAsync(team.Id, agentId);
        await svc.RemoveMemberAsync(team.Id, agentId);
        var found = await svc.GetByIdAsync(team.Id);
        Assert.Empty(found!.Members);
    }

    // ── PatchAsync Instructions ───────────────────────────────────────────────

    [Fact]
    public async Task PatchAsync_SetsInstructions_WhenProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeTeam());
        var updated = await svc.PatchAsync(created.Id,
            new AgentBoard.Contracts.TeamPatch(null, null, Instructions: "# Team instructions"));
        Assert.NotNull(updated);
        Assert.Equal("# Team instructions", updated.Instructions);
    }

    [Fact]
    public async Task PatchAsync_ClearsInstructions_WhenClearInstructionsIsTrue()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeTeam());
        await svc.PatchAsync(created.Id,
            new AgentBoard.Contracts.TeamPatch(null, null, Instructions: "Some instructions"));
        var updated = await svc.PatchAsync(created.Id,
            new AgentBoard.Contracts.TeamPatch(null, null, ClearInstructions: true));
        Assert.NotNull(updated);
        Assert.Null(updated.Instructions);
    }

    [Fact]
    public async Task PatchAsync_UpdatesName_WhenProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeTeam("Original Name"));
        var updated = await svc.PatchAsync(created.Id,
            new AgentBoard.Contracts.TeamPatch("Updated Name", null));
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
    }

    [Fact]
    public async Task PatchAsync_ReturnsNull_WhenNotFound()
    {
        var svc = BuildService();
        var result = await svc.PatchAsync(Guid.NewGuid(),
            new AgentBoard.Contracts.TeamPatch("Ghost", null));
        Assert.Null(result);
    }
}
