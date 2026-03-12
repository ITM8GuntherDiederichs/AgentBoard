using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>
/// Service responsible for generating a deployable ZIP archive for a project,
/// containing configuration, team/agent markdown, skill definitions, and task snapshots.
/// </summary>
public class DeployService(
    IDbContextFactory<ApplicationDbContext> factory,
    SkillFileService skillFileService,
    IWebHostEnvironment env)
{
    /// <summary>
    /// Builds an in-memory ZIP containing all artefacts needed to deploy the project to an external agent system.
    /// </summary>
    /// <param name="projectId">The project to package.</param>
    /// <param name="boardBaseUrl">Base URL of this AgentBoard instance (e.g. <c>localhost:5227</c>).</param>
    /// <returns>The raw ZIP bytes, or <c>null</c> if the project does not exist.</returns>
    public async Task<byte[]?> GenerateDeployZipAsync(Guid projectId, string boardBaseUrl)
    {
        using var db = await factory.CreateDbContextAsync();

        // Load project
        var project = await db.Projects.FindAsync(projectId);
        if (project is null) return null;

        // Load todos for this project
        var todos = await db.Todos
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Status)
            .ThenBy(t => t.Priority)
            .ToListAsync();

        // Load feature requests for this project
        var features = await db.FeatureRequests
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Status)
            .ToListAsync();

        // Load assigned teams (first one is "the team" for team.md)
        var teamIds = await db.ProjectTeams
            .Where(pt => pt.ProjectId == projectId)
            .Select(pt => pt.TeamId)
            .ToListAsync();

        var teams = teamIds.Count > 0
            ? await db.Teams.Include(t => t.Members).Where(t => teamIds.Contains(t.Id)).ToListAsync()
            : new List<Team>();

        var primaryTeam = teams.FirstOrDefault();

        // Load directly-assigned agents
        var directAgentIds = await db.ProjectAgents
            .Where(pa => pa.ProjectId == projectId)
            .Select(pa => pa.AgentId)
            .ToListAsync();

        // Collect all agent IDs (direct + via teams), deduplicated
        var teamMemberAgentIds = teams
            .SelectMany(t => t.Members.Select(m => m.AgentId))
            .ToList();

        var allAgentIds = directAgentIds.Union(teamMemberAgentIds).Distinct().ToList();

        // Load agents
        var agents = allAgentIds.Count > 0
            ? await db.Agents.Where(a => allAgentIds.Contains(a.Id)).ToListAsync()
            : new List<Agent>();

        // Load all skill assignments for these agents
        var agentSkillRows = allAgentIds.Count > 0
            ? await db.AgentSkills.Where(s => allAgentIds.Contains(s.AgentId)).ToListAsync()
            : new List<AgentSkill>();

        var skillIds = agentSkillRows.Select(s => s.SkillId).Distinct().ToList();
        var skills = skillIds.Count > 0
            ? await db.Skills.Where(s => skillIds.Contains(s.Id)).ToListAsync()
            : new List<Skill>();

        // Load skill files for all skills
        var skillFiles = new Dictionary<Guid, List<SkillFile>>();
        foreach (var skillId in skillIds)
        {
            skillFiles[skillId] = await skillFileService.GetFilesForSkillAsync(skillId);
        }

        // Build the ZIP in-memory
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // ── agentboard.json ──────────────────────────────────────────────
            var agentboardJson = JsonSerializer.Serialize(new
            {
                projectId = project.Id.ToString(),
                projectName = project.Name,
                boardUrl = $"https://{boardBaseUrl}/projects/{project.Id}",
                apiBaseUrl = $"https://{boardBaseUrl}/api",
                integrationEndpoint = $"https://{boardBaseUrl}/api/projects/{project.Id}/integration"
            }, new JsonSerializerOptions { WriteIndented = true });
            WriteEntry(archive, "agentboard.json", agentboardJson);

            // ── team.md ──────────────────────────────────────────────────────
            if (primaryTeam is not null)
            {
                var teamMd = BuildTeamMarkdown(primaryTeam, agents);
                WriteEntry(archive, "team.md", teamMd);
            }
            else
            {
                WriteEntry(archive, "team.md", "# Team\n\nNo team assigned to this project.\n");
            }

            // ── agents/{agentName}.md ────────────────────────────────────────
            foreach (var agent in agents)
            {
                var agentSkillIds = agentSkillRows
                    .Where(s => s.AgentId == agent.Id)
                    .Select(s => s.SkillId)
                    .ToHashSet();
                var agentSkillNames = skills
                    .Where(s => agentSkillIds.Contains(s.Id))
                    .Select(s => s.Name)
                    .OrderBy(n => n)
                    .ToList();

                var agentMd = BuildAgentMarkdown(agent, agentSkillNames);
                var safeAgentName = SanitiseFileName(agent.Name);
                WriteEntry(archive, $"agents/{safeAgentName}.md", agentMd);
            }

            // ── skills/{skillName}/skill.md + ref files ──────────────────────
            foreach (var skill in skills)
            {
                var safeSkillName = SanitiseFileName(skill.Name);
                WriteEntry(archive, $"skills/{safeSkillName}/skill.md", skill.Content);

                // Copy each reference file
                var refFiles = skillFiles.TryGetValue(skill.Id, out var files) ? files : new List<SkillFile>();
                foreach (var refFile in refFiles)
                {
                    var physicalPath = Path.Combine(
                        env.WebRootPath,
                        refFile.FilePath.Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(physicalPath))
                    {
                        var refEntry = archive.CreateEntry($"skills/{safeSkillName}/{refFile.FileName}");
                        using var entryStream = refEntry.Open();
                        using var fileStream = File.OpenRead(physicalPath);
                        await fileStream.CopyToAsync(entryStream);
                    }
                }
            }

            // ── todos.md ─────────────────────────────────────────────────────
            WriteEntry(archive, "todos.md", BuildTodosMarkdown(project.Name, todos));

            // ── features.md ──────────────────────────────────────────────────
            WriteEntry(archive, "features.md", BuildFeaturesMarkdown(project.Name, features));
        }

        return ms.ToArray();
    }

    // ── Private builders ─────────────────────────────────────────────────────

    private static string BuildTeamMarkdown(Team team, List<Agent> agents)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Team: {team.Name}");
        sb.AppendLine();
        sb.AppendLine(team.Instructions ?? "No instructions provided.");
        sb.AppendLine();
        sb.AppendLine("## Members");

        var memberAgentIds = team.Members.Select(m => m.AgentId).ToHashSet();
        var teamAgents = agents.Where(a => memberAgentIds.Contains(a.Id)).OrderBy(a => a.Name).ToList();

        if (teamAgents.Count == 0)
        {
            sb.AppendLine("No members assigned.");
        }
        else
        {
            foreach (var agent in teamAgents)
            {
                var status = agent.IsAvailable ? "Available" : "Offline";
                sb.AppendLine($"- {agent.Name} ({agent.Type}) — {status}");
            }
        }

        return sb.ToString();
    }

    private static string BuildAgentMarkdown(Agent agent, List<string> skillNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Agent: {agent.Name}");
        sb.AppendLine($"**Type:** {agent.Type}");
        sb.AppendLine($"**Status:** {(agent.IsAvailable ? "Available" : "Offline")}");
        sb.AppendLine();
        sb.AppendLine("## Instructions");
        sb.AppendLine(agent.Instructions ?? "No instructions provided.");
        sb.AppendLine();
        sb.AppendLine("## Skills");

        if (skillNames.Count == 0)
        {
            sb.AppendLine("No skills assigned.");
        }
        else
        {
            foreach (var skill in skillNames)
                sb.AppendLine($"- {skill}");
        }

        return sb.ToString();
    }

    private static string BuildTodosMarkdown(string projectName, List<Todo> todos)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Todos: {projectName}");
        sb.AppendLine();

        if (todos.Count == 0)
        {
            sb.AppendLine("No todos.");
            return sb.ToString();
        }

        foreach (var group in todos.GroupBy(t => t.Status).OrderBy(g => g.Key))
        {
            sb.AppendLine($"## {group.Key}");
            foreach (var todo in group.OrderBy(t => t.Priority).ThenBy(t => t.Title))
            {
                var claimed = todo.ClaimedBy ?? "Unassigned";
                sb.AppendLine($"- [{todo.Priority}] {todo.Title} — {todo.Description ?? string.Empty} (Claimed: {claimed})");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildFeaturesMarkdown(string projectName, List<FeatureRequest> features)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Feature Requests: {projectName}");
        sb.AppendLine();

        if (features.Count == 0)
        {
            sb.AppendLine("No feature requests.");
            return sb.ToString();
        }

        foreach (var fr in features.OrderBy(f => f.Status).ThenBy(f => f.Title))
        {
            sb.AppendLine($"- [{fr.Status}] {fr.Title}: {fr.Description ?? string.Empty}");
        }

        return sb.ToString();
    }

    /// <summary>Writes a UTF-8 text entry into the archive.</summary>
    private static void WriteEntry(ZipArchive archive, string entryPath, string content)
    {
        var entry = archive.CreateEntry(entryPath);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    /// <summary>Strips characters that are invalid in ZIP entry path segments.</summary>
    private static string SanitiseFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
