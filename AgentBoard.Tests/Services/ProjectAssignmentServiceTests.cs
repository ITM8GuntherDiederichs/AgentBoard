using AgentBoard.Data.Models;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Services;

/// <summary>Unit tests for <see cref="ProjectAssignmentService"/>.</summary>
public class ProjectAssignmentServiceTests
{
    private static ProjectAssignmentService BuildService(string? dbName = null)
        => new(TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString()));

    /// <summary>Seeds a project and returns its ID via a shared db name.</summary>
    private static async Task<Guid> SeedProjectAsync(string dbName)
    {
        var svc = new ProjectService(TestDbFactory.Create(dbName));
        var project = await svc.CreateAsync(new Project { Name = "Test Project" });
        return project.Id;
    }

    // ── AssignAgentAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task AssignAgentAsync_ReturnsFalse_WhenProjectNotFound()
    {
        var svc = BuildService();
        var result = await svc.AssignAgentAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task AssignAgentAsync_ReturnsTrue_WhenProjectExists()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var result = await svc.AssignAgentAsync(projectId, Guid.NewGuid());
        Assert.True(result);
    }

    [Fact]
    public async Task AssignAgentAsync_IsIdempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var agentId = Guid.NewGuid();
        await svc.AssignAgentAsync(projectId, agentId);
        var result = await svc.AssignAgentAsync(projectId, agentId);
        Assert.True(result);
        var agents = await svc.GetProjectAgentsAsync(projectId);
        Assert.Single(agents);
    }

    // ── UnassignAgentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UnassignAgentAsync_ReturnsFalse_WhenNotAssigned()
    {
        var svc = BuildService();
        var result = await svc.UnassignAgentAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task UnassignAgentAsync_ReturnsTrue_WhenAssigned()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var agentId = Guid.NewGuid();
        await svc.AssignAgentAsync(projectId, agentId);
        var result = await svc.UnassignAgentAsync(projectId, agentId);
        Assert.True(result);
    }

    [Fact]
    public async Task UnassignAgentAsync_RemovesAssignment()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var agentId = Guid.NewGuid();
        await svc.AssignAgentAsync(projectId, agentId);
        await svc.UnassignAgentAsync(projectId, agentId);
        var agents = await svc.GetProjectAgentsAsync(projectId);
        Assert.Empty(agents);
    }

    // ── GetProjectAgentsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetProjectAgentsAsync_ReturnsEmpty_WhenNoneAssigned()
    {
        var svc = BuildService();
        var result = await svc.GetProjectAgentsAsync(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetProjectAgentsAsync_ReturnsAssignedAgentIds()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var agentId1 = Guid.NewGuid();
        var agentId2 = Guid.NewGuid();
        await svc.AssignAgentAsync(projectId, agentId1);
        await svc.AssignAgentAsync(projectId, agentId2);
        var agents = await svc.GetProjectAgentsAsync(projectId);
        Assert.Equal(2, agents.Count);
        Assert.Contains(agentId1, agents);
        Assert.Contains(agentId2, agents);
    }

    // ── AssignTeamAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task AssignTeamAsync_ReturnsFalse_WhenProjectNotFound()
    {
        var svc = BuildService();
        var result = await svc.AssignTeamAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task AssignTeamAsync_ReturnsTrue_WhenProjectExists()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var result = await svc.AssignTeamAsync(projectId, Guid.NewGuid());
        Assert.True(result);
    }

    [Fact]
    public async Task AssignTeamAsync_IsIdempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var teamId = Guid.NewGuid();
        await svc.AssignTeamAsync(projectId, teamId);
        var result = await svc.AssignTeamAsync(projectId, teamId);
        Assert.True(result);
        var teams = await svc.GetProjectTeamsAsync(projectId);
        Assert.Single(teams);
    }

    // ── UnassignTeamAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UnassignTeamAsync_ReturnsFalse_WhenNotAssigned()
    {
        var svc = BuildService();
        var result = await svc.UnassignTeamAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task UnassignTeamAsync_ReturnsTrue_WhenAssigned()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var teamId = Guid.NewGuid();
        await svc.AssignTeamAsync(projectId, teamId);
        var result = await svc.UnassignTeamAsync(projectId, teamId);
        Assert.True(result);
    }

    [Fact]
    public async Task UnassignTeamAsync_RemovesAssignment()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var teamId = Guid.NewGuid();
        await svc.AssignTeamAsync(projectId, teamId);
        await svc.UnassignTeamAsync(projectId, teamId);
        var teams = await svc.GetProjectTeamsAsync(projectId);
        Assert.Empty(teams);
    }

    // ── GetProjectTeamsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetProjectTeamsAsync_ReturnsEmpty_WhenNoneAssigned()
    {
        var svc = BuildService();
        var result = await svc.GetProjectTeamsAsync(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetProjectTeamsAsync_ReturnsAssignedTeamIds()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var teamId1 = Guid.NewGuid();
        var teamId2 = Guid.NewGuid();
        await svc.AssignTeamAsync(projectId, teamId1);
        await svc.AssignTeamAsync(projectId, teamId2);
        var teams = await svc.GetProjectTeamsAsync(projectId);
        Assert.Equal(2, teams.Count);
        Assert.Contains(teamId1, teams);
        Assert.Contains(teamId2, teams);
    }

    // ── GetAllProjectAgentIdsAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetAllProjectAgentIdsAsync_ReturnsEmpty_WhenNothingAssigned()
    {
        var svc = BuildService();
        var result = await svc.GetAllProjectAgentIdsAsync(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllProjectAgentIdsAsync_IncludesDirectAgents()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var agentId = Guid.NewGuid();
        await svc.AssignAgentAsync(projectId, agentId);
        var result = await svc.GetAllProjectAgentIdsAsync(projectId);
        Assert.Contains(agentId, result);
    }

    [Fact]
    public async Task GetAllProjectAgentIdsAsync_IncludesTeamMemberAgents()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);

        // Create a team with a member using TeamService sharing the same db
        var teamSvc = new TeamService(TestDbFactory.Create(dbName));
        var team = await teamSvc.CreateAsync(new Team { Name = "Dev Team" });
        var agentId = Guid.NewGuid();
        await teamSvc.AddMemberAsync(team.Id, agentId);

        await svc.AssignTeamAsync(projectId, team.Id);

        var result = await svc.GetAllProjectAgentIdsAsync(projectId);
        Assert.Contains(agentId, result);
    }

    [Fact]
    public async Task GetAllProjectAgentIdsAsync_DeduplicatesAgents()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);

        // Assign agent both directly and via team
        var teamSvc = new TeamService(TestDbFactory.Create(dbName));
        var team = await teamSvc.CreateAsync(new Team { Name = "Dev Team" });
        var agentId = Guid.NewGuid();
        await teamSvc.AddMemberAsync(team.Id, agentId);

        await svc.AssignAgentAsync(projectId, agentId);
        await svc.AssignTeamAsync(projectId, team.Id);

        var result = await svc.GetAllProjectAgentIdsAsync(projectId);
        Assert.Equal(1, result.Count(id => id == agentId));
    }

    // ── GetProjectAgentEntriesAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetProjectAgentEntriesAsync_DirectEntry_HasSourceDirect()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var agentId = Guid.NewGuid();
        await svc.AssignAgentAsync(projectId, agentId);

        var entries = await svc.GetProjectAgentEntriesAsync(projectId);
        var entry = Assert.Single(entries);
        Assert.Equal("direct", entry.Source);
        Assert.Equal(agentId, entry.AgentId);
    }

    [Fact]
    public async Task GetProjectAgentEntriesAsync_TeamEntry_HasSourceTeam()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);

        var teamSvc = new TeamService(TestDbFactory.Create(dbName));
        var team = await teamSvc.CreateAsync(new Team { Name = "Dev Team" });
        var agentId = Guid.NewGuid();
        await teamSvc.AddMemberAsync(team.Id, agentId);
        await svc.AssignTeamAsync(projectId, team.Id);

        var entries = await svc.GetProjectAgentEntriesAsync(projectId);
        var entry = Assert.Single(entries);
        Assert.Equal("team", entry.Source);
        Assert.Equal(agentId, entry.AgentId);
    }

    [Fact]
    public async Task GetProjectAgentEntriesAsync_DirectTakesPrecedenceOverTeam()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);

        var teamSvc = new TeamService(TestDbFactory.Create(dbName));
        var team = await teamSvc.CreateAsync(new Team { Name = "Dev Team" });
        var agentId = Guid.NewGuid();
        await teamSvc.AddMemberAsync(team.Id, agentId);

        await svc.AssignAgentAsync(projectId, agentId);
        await svc.AssignTeamAsync(projectId, team.Id);

        var entries = await svc.GetProjectAgentEntriesAsync(projectId);
        var entry = Assert.Single(entries, e => e.AgentId == agentId);
        Assert.Equal("direct", entry.Source);
    }

    // ── GetProjectTeamEntriesAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetProjectTeamEntriesAsync_ReturnsEmpty_WhenNoneAssigned()
    {
        var svc = BuildService();
        var result = await svc.GetProjectTeamEntriesAsync(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetProjectTeamEntriesAsync_ReturnsTeamEntries()
    {
        var dbName = Guid.NewGuid().ToString();
        var projectId = await SeedProjectAsync(dbName);
        var svc = BuildService(dbName);
        var teamId = Guid.NewGuid();
        await svc.AssignTeamAsync(projectId, teamId);

        var entries = await svc.GetProjectTeamEntriesAsync(projectId);
        var entry = Assert.Single(entries);
        Assert.Equal(teamId, entry.TeamId);
        Assert.True(entry.AssignedAt > DateTime.MinValue);
    }
}
