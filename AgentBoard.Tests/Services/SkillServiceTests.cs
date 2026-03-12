using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Services;

/// <summary>Unit tests for <see cref="SkillService"/>.</summary>
public class SkillServiceTests
{
    private static SkillService BuildService(string? dbName = null)
        => new(TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString()));

    private static Skill MakeSkill(string name = "Test Skill", string content = "# Content")
        => new() { Name = name, Content = content };

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_SetsId_ToNonEmpty()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeSkill());
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateAsync_SetsName()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeSkill(name: "My Skill"));
        Assert.Equal("My Skill", result.Name);
    }

    [Fact]
    public async Task CreateAsync_SetsContent()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeSkill(content: "## Markdown"));
        Assert.Equal("## Markdown", result.Content);
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeSkill());
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.CreatedAt, before, after);
    }

    [Fact]
    public async Task CreateAsync_SetsUpdatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeSkill());
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.UpdatedAt, before, after);
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoSkills()
    {
        var svc = BuildService();
        var result = await svc.GetAllAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllSkills()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        await svc.CreateAsync(MakeSkill("Skill A"));
        await svc.CreateAsync(MakeSkill("Skill B"));
        var result = await svc.GetAllAsync();
        Assert.Equal(2, result.Count);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsSkill_WhenFound()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeSkill(name: "Find Me"));
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

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_UpdatesName_WhenProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeSkill(name: "Original"));
        var updated = await svc.UpdateAsync(created.Id, new SkillPatch("Updated", null));
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesContent_WhenProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeSkill(content: "Old content"));
        var updated = await svc.UpdateAsync(created.Id, new SkillPatch(null, "New content"));
        Assert.NotNull(updated);
        Assert.Equal("New content", updated.Content);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeName_WhenNameIsNull()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeSkill(name: "Keep This"));
        var updated = await svc.UpdateAsync(created.Id, new SkillPatch(null, null));
        Assert.NotNull(updated);
        Assert.Equal("Keep This", updated.Name);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdatedAt()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeSkill());
        await Task.Delay(10);
        var updated = await svc.UpdateAsync(created.Id, new SkillPatch("Changed", null));
        Assert.NotNull(updated);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotFound()
    {
        var svc = BuildService();
        var result = await svc.UpdateAsync(Guid.NewGuid(), new SkillPatch("X", null));
        Assert.Null(result);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesSkill_ReturnsTrue()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeSkill());
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

    // ── Agent skills ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddSkillToAgentAsync_ReturnsTrue_WhenSkillExists()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skill = await svc.CreateAsync(MakeSkill());
        var agentId = Guid.NewGuid();
        var result = await svc.AddSkillToAgentAsync(agentId, skill.Id);
        Assert.True(result);
    }

    [Fact]
    public async Task AddSkillToAgentAsync_ReturnsFalse_WhenSkillNotFound()
    {
        var svc = BuildService();
        var result = await svc.AddSkillToAgentAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task AddSkillToAgentAsync_IsIdempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skill = await svc.CreateAsync(MakeSkill());
        var agentId = Guid.NewGuid();
        await svc.AddSkillToAgentAsync(agentId, skill.Id);
        var result = await svc.AddSkillToAgentAsync(agentId, skill.Id);
        Assert.True(result);
        var agentSkills = await svc.GetAgentSkillsAsync(agentId);
        Assert.Single(agentSkills);
    }

    [Fact]
    public async Task GetAgentSkillsAsync_ReturnsAssignedSkills()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skill1 = await svc.CreateAsync(MakeSkill("Skill A"));
        var skill2 = await svc.CreateAsync(MakeSkill("Skill B"));
        var agentId = Guid.NewGuid();
        await svc.AddSkillToAgentAsync(agentId, skill1.Id);
        await svc.AddSkillToAgentAsync(agentId, skill2.Id);
        var result = await svc.GetAgentSkillsAsync(agentId);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAgentSkillsAsync_ReturnsEmpty_WhenNoSkillsAssigned()
    {
        var svc = BuildService();
        var result = await svc.GetAgentSkillsAsync(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public async Task RemoveSkillFromAgentAsync_ReturnsTrue_WhenRemoved()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skill = await svc.CreateAsync(MakeSkill());
        var agentId = Guid.NewGuid();
        await svc.AddSkillToAgentAsync(agentId, skill.Id);
        var result = await svc.RemoveSkillFromAgentAsync(agentId, skill.Id);
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveSkillFromAgentAsync_ReturnsFalse_WhenNotAssigned()
    {
        var svc = BuildService();
        var result = await svc.RemoveSkillFromAgentAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveSkillFromAgentAsync_SkillIsGone_AfterRemoval()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skill = await svc.CreateAsync(MakeSkill());
        var agentId = Guid.NewGuid();
        await svc.AddSkillToAgentAsync(agentId, skill.Id);
        await svc.RemoveSkillFromAgentAsync(agentId, skill.Id);
        var result = await svc.GetAgentSkillsAsync(agentId);
        Assert.Empty(result);
    }

    // ── Team skills ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AddSkillToTeamAsync_ReturnsTrue_WhenSkillExists()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skill = await svc.CreateAsync(MakeSkill());
        var teamId = Guid.NewGuid();
        var result = await svc.AddSkillToTeamAsync(teamId, skill.Id);
        Assert.True(result);
    }

    [Fact]
    public async Task AddSkillToTeamAsync_ReturnsFalse_WhenSkillNotFound()
    {
        var svc = BuildService();
        var result = await svc.AddSkillToTeamAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task AddSkillToTeamAsync_IsIdempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skill = await svc.CreateAsync(MakeSkill());
        var teamId = Guid.NewGuid();
        await svc.AddSkillToTeamAsync(teamId, skill.Id);
        var result = await svc.AddSkillToTeamAsync(teamId, skill.Id);
        Assert.True(result);
        var teamSkills = await svc.GetTeamSkillsAsync(teamId);
        Assert.Single(teamSkills);
    }

    [Fact]
    public async Task GetTeamSkillsAsync_ReturnsAssignedSkills()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skill1 = await svc.CreateAsync(MakeSkill("Skill A"));
        var skill2 = await svc.CreateAsync(MakeSkill("Skill B"));
        var teamId = Guid.NewGuid();
        await svc.AddSkillToTeamAsync(teamId, skill1.Id);
        await svc.AddSkillToTeamAsync(teamId, skill2.Id);
        var result = await svc.GetTeamSkillsAsync(teamId);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetTeamSkillsAsync_ReturnsEmpty_WhenNoSkillsAssigned()
    {
        var svc = BuildService();
        var result = await svc.GetTeamSkillsAsync(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public async Task RemoveSkillFromTeamAsync_ReturnsTrue_WhenRemoved()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skill = await svc.CreateAsync(MakeSkill());
        var teamId = Guid.NewGuid();
        await svc.AddSkillToTeamAsync(teamId, skill.Id);
        var result = await svc.RemoveSkillFromTeamAsync(teamId, skill.Id);
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveSkillFromTeamAsync_ReturnsFalse_WhenNotAssigned()
    {
        var svc = BuildService();
        var result = await svc.RemoveSkillFromTeamAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveSkillFromTeamAsync_SkillIsGone_AfterRemoval()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        var skill = await svc.CreateAsync(MakeSkill());
        var teamId = Guid.NewGuid();
        await svc.AddSkillToTeamAsync(teamId, skill.Id);
        await svc.RemoveSkillFromTeamAsync(teamId, skill.Id);
        var result = await svc.GetTeamSkillsAsync(teamId);
        Assert.Empty(result);
    }
}
