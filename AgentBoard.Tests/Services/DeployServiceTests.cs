using System.IO.Compression;
using System.Text.Json;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace AgentBoard.Tests.Services;

/// <summary>Unit tests for <see cref="DeployService"/>.</summary>
public class DeployServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(), "AgentBoardDeploy_" + Guid.NewGuid().ToString("N"));

    public DeployServiceTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
        }
    }

    private DeployService BuildService(string? dbName = null)
    {
        var factory = TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString());
        var skillFileSvc = new SkillFileService(factory);
        var env = Substitute.For<IWebHostEnvironment>();
        env.WebRootPath.Returns(_tempRoot);
        return new DeployService(factory, skillFileSvc, env);
    }

    private static async Task<Project> SeedProjectAsync(IDbContextFactory<ApplicationDbContext> factory, string name = "Test Project")
    {
        using var db = await factory.CreateDbContextAsync();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    private static ZipArchive OpenZip(byte[] bytes) =>
        new(new MemoryStream(bytes), ZipArchiveMode.Read);

    // ── Returns null for unknown project ─────────────────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_ReturnsNull_ForUnknownProject()
    {
        var svc = BuildService();
        var result = await svc.GenerateDeployZipAsync(Guid.NewGuid(), "localhost:5227");
        Assert.Null(result);
    }

    // ── ZIP is non-empty for a valid project ─────────────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_ReturnsBytes_ForExistingProject()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        var result = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    // ── ZIP contains agentboard.json ─────────────────────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_ZipContains_AgentboardJson()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        var entry = archive.GetEntry("agentboard.json");
        Assert.NotNull(entry);
    }

    // ── agentboard.json has correct JSON structure ────────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_AgentboardJson_HasCorrectFields()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory, "My Project");

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "myboard.example.com");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        var entry = archive.GetEntry("agentboard.json");
        Assert.NotNull(entry);

        using var reader = new StreamReader(entry.Open());
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(project.Id.ToString(), root.GetProperty("projectId").GetString());
        Assert.Equal("My Project", root.GetProperty("projectName").GetString());
        Assert.Contains(project.Id.ToString(), root.GetProperty("boardUrl").GetString());
        Assert.NotNull(root.GetProperty("apiBaseUrl").GetString());
        Assert.Contains(project.Id.ToString(), root.GetProperty("integrationEndpoint").GetString());
    }

    // ── ZIP contains team.md ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_ZipContains_TeamMd()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        Assert.NotNull(archive.GetEntry("team.md"));
    }

    // ── team.md has team name when a team is assigned ────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_TeamMd_ContainsTeamName_WhenTeamAssigned()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        // Seed a team and assign it to the project
        using var db = await factory.CreateDbContextAsync();
        var team = new Team { Id = Guid.NewGuid(), Name = "Alpha Team", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Teams.Add(team);
        db.ProjectTeams.Add(new ProjectTeam { ProjectId = project.Id, TeamId = team.Id, AssignedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        var entry = archive.GetEntry("team.md");
        Assert.NotNull(entry);

        using var reader = new StreamReader(entry.Open());
        var content = await reader.ReadToEndAsync();
        Assert.Contains("Alpha Team", content);
    }

    // ── ZIP contains agent file when an agent is assigned ────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_ContainsAgentFile_WhenDirectAgentAssigned()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        using var db = await factory.CreateDbContextAsync();
        var agent = new Agent { Id = Guid.NewGuid(), Name = "TestBot", Type = AgentType.AI, IsAvailable = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Agents.Add(agent);
        db.ProjectAgents.Add(new ProjectAgent { ProjectId = project.Id, AgentId = agent.Id, AssignedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        Assert.True(archive.Entries.Any(e => e.FullName.StartsWith("agents/") && e.FullName.EndsWith(".md")),
            "Expected at least one agent .md file in the ZIP");
    }

    // ── ZIP contains skill folder for assigned skill ──────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_ContainsSkillFolder_ForAgentSkill()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        using var db = await factory.CreateDbContextAsync();
        var agent = new Agent { Id = Guid.NewGuid(), Name = "SkillBot", Type = AgentType.AI, IsAvailable = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var skill = new Skill { Id = Guid.NewGuid(), Name = "CodeReview", Content = "# Code Review\nReview code carefully.", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Agents.Add(agent);
        db.Skills.Add(skill);
        db.ProjectAgents.Add(new ProjectAgent { ProjectId = project.Id, AgentId = agent.Id, AssignedAt = DateTime.UtcNow });
        db.AgentSkills.Add(new AgentSkill { AgentId = agent.Id, SkillId = skill.Id });
        await db.SaveChangesAsync();

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        var skillEntry = archive.GetEntry("skills/CodeReview/skill.md");
        Assert.NotNull(skillEntry);
    }

    // ── skill.md contains skill content ─────────────────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_SkillMd_ContainsContent()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        using var db = await factory.CreateDbContextAsync();
        var agent = new Agent { Id = Guid.NewGuid(), Name = "Bot", Type = AgentType.AI, IsAvailable = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var skill = new Skill { Id = Guid.NewGuid(), Name = "Testing", Content = "## Test with xUnit", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Agents.Add(agent);
        db.Skills.Add(skill);
        db.ProjectAgents.Add(new ProjectAgent { ProjectId = project.Id, AgentId = agent.Id, AssignedAt = DateTime.UtcNow });
        db.AgentSkills.Add(new AgentSkill { AgentId = agent.Id, SkillId = skill.Id });
        await db.SaveChangesAsync();

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        var entry = archive.GetEntry("skills/Testing/skill.md");
        Assert.NotNull(entry);

        using var reader = new StreamReader(entry.Open());
        var content = await reader.ReadToEndAsync();
        Assert.Contains("## Test with xUnit", content);
    }

    // ── ZIP contains todos.md ────────────────────────────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_ZipContains_TodosMd()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        Assert.NotNull(archive.GetEntry("todos.md"));
    }

    // ── ZIP contains features.md ─────────────────────────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_ZipContains_FeaturesMd()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        Assert.NotNull(archive.GetEntry("features.md"));
    }

    // ── todos.md lists todos when they exist ─────────────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_TodosMd_ListsTodos()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        using var db = await factory.CreateDbContextAsync();
        db.Todos.Add(new Todo
        {
            Id = Guid.NewGuid(),
            Title = "Fix the bug",
            Description = "Critical",
            Status = TodoStatus.Pending,
            Priority = TodoPriority.High,
            ProjectId = project.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        var entry = archive.GetEntry("todos.md");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry.Open());
        var content = await reader.ReadToEndAsync();
        Assert.Contains("Fix the bug", content);
    }

    // ── features.md lists feature requests ───────────────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_FeaturesMd_ListsFeatureRequests()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        using var db = await factory.CreateDbContextAsync();
        db.FeatureRequests.Add(new FeatureRequest
        {
            Id = Guid.NewGuid(),
            Title = "Dark mode support",
            Description = "Add a dark theme",
            Status = FeatureRequestStatus.Planned,
            Priority = TodoPriority.Medium,
            ProjectId = project.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        var entry = archive.GetEntry("features.md");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry.Open());
        var content = await reader.ReadToEndAsync();
        Assert.Contains("Dark mode support", content);
    }

    // ── Agent instructions are included in agent file ────────────────────────

    [Fact]
    public async Task GenerateDeployZipAsync_AgentMd_ContainsInstructions()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        var svc = BuildServiceFromFactory(factory);
        var project = await SeedProjectAsync(factory);

        using var db = await factory.CreateDbContextAsync();
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = "InstructBot",
            Type = AgentType.Human,
            IsAvailable = false,
            Instructions = "# Special instructions",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Agents.Add(agent);
        db.ProjectAgents.Add(new ProjectAgent { ProjectId = project.Id, AgentId = agent.Id, AssignedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var zipBytes = await svc.GenerateDeployZipAsync(project.Id, "localhost:5227");
        Assert.NotNull(zipBytes);

        using var archive = OpenZip(zipBytes);
        var entry = archive.GetEntry("agents/InstructBot.md");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry.Open());
        var content = await reader.ReadToEndAsync();
        Assert.Contains("# Special instructions", content);
        Assert.Contains("Offline", content);
        Assert.Contains("Human", content);
    }

    // ── Helper: builds DeployService from an existing factory ─────────────────

    private DeployService BuildServiceFromFactory(IDbContextFactory<ApplicationDbContext> factory)
    {
        var skillFileSvc = new SkillFileService(factory);
        var env = Substitute.For<IWebHostEnvironment>();
        env.WebRootPath.Returns(_tempRoot);
        return new DeployService(factory, skillFileSvc, env);
    }
}
