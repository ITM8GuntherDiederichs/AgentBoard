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
            var integrationType = primaryTeam?.IntegrationType ?? IntegrationType.None;
            var syncEndpoint = integrationType switch
            {
                IntegrationType.GitHub => $"https://{boardBaseUrl}/api/projects/{project.Id}/sync/github",
                IntegrationType.AzureDevOps => $"https://{boardBaseUrl}/api/projects/{project.Id}/sync/azuredevops",
                _ => null
            };
            var webhookEndpoint = integrationType switch
            {
                IntegrationType.GitHub => $"https://{boardBaseUrl}/api/projects/{project.Id}/sync/github/webhook",
                IntegrationType.AzureDevOps => $"https://{boardBaseUrl}/api/projects/{project.Id}/sync/azuredevops/webhook",
                _ => null
            };

            var agentboardJson = JsonSerializer.Serialize(new
            {
                projectId = project.Id.ToString(),
                projectName = project.Name,
                boardUrl = $"https://{boardBaseUrl}/projects/{project.Id}",
                apiBaseUrl = $"https://{boardBaseUrl}/api",
                integrationEndpoint = $"https://{boardBaseUrl}/api/projects/{project.Id}/integration",
                integrationStatusEndpoint = $"https://{boardBaseUrl}/api/projects/{project.Id}/integration/status",
                integrationType = integrationType.ToString(),
                syncEndpoint,
                webhookEndpoint,
                todosEndpoint = $"https://{boardBaseUrl}/api/todos",
                featuresEndpoint = $"https://{boardBaseUrl}/api/feature-requests",
                eventsEndpoint = $"https://{boardBaseUrl}/api/projects/{project.Id}/events"
            }, new JsonSerializerOptions { WriteIndented = true });
            WriteEntry(archive, "agentboard.json", agentboardJson);

            // ── SETUP.md ──────────────────────────────────────────────────────
            WriteEntry(archive, "SETUP.md", BuildSetupMarkdown(
                project, boardBaseUrl, integrationType, syncEndpoint, webhookEndpoint));

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

    private static string BuildSetupMarkdown(
        Project project,
        string boardBaseUrl,
        IntegrationType integrationType,
        string? syncEndpoint,
        string? webhookEndpoint)
    {
        var apiBase = $"https://{boardBaseUrl}/api";
        var integrationEndpoint = $"{apiBase}/projects/{project.Id}/integration";
        var statusEndpoint = $"{integrationEndpoint}/status";
        var todosEndpoint = $"{apiBase}/todos";
        var featuresEndpoint = $"{apiBase}/feature-requests";
        var eventsEndpoint = $"{apiBase}/projects/{project.Id}/events";

        var sb = new StringBuilder();
        sb.AppendLine($"# AgentBoard Setup Guide — {project.Name}");
        sb.AppendLine();
        sb.AppendLine($"> Board URL: {$"https://{boardBaseUrl}/projects/{project.Id}"}");
        sb.AppendLine($"> Project ID: `{project.Id}`");
        sb.AppendLine($"> Integration type: **{integrationType}**");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // ── Step 1: Connect ──────────────────────────────────────────────────
        sb.AppendLine("## Step 1 — Connect this project to " + integrationType switch
        {
            IntegrationType.GitHub => "GitHub",
            IntegrationType.AzureDevOps => "Azure DevOps",
            _ => "your integration"
        });
        sb.AppendLine();

        if (integrationType == IntegrationType.GitHub)
        {
            sb.AppendLine("1. Create a GitHub Personal Access Token (classic) with `repo` scope:");
            sb.AppendLine("   - Go to GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)");
            sb.AppendLine("   - Grant: `repo` (full control of private repositories)");
            sb.AppendLine();
            sb.AppendLine("2. POST the connection to AgentBoard:");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine($"curl -X POST \"{integrationEndpoint}\" \\");
            sb.AppendLine("  -H \"Content-Type: application/json\" \\");
            sb.AppendLine("  -d '{");
            sb.AppendLine("    \"token\": \"ghp_YOUR_GITHUB_TOKEN\",");
            sb.AppendLine("    \"repoUrl\": \"https://github.com/owner/repo\",");
            sb.AppendLine("    \"externalProjectId\": \"\"");
            sb.AppendLine("  }'");
            sb.AppendLine("```");
        }
        else if (integrationType == IntegrationType.AzureDevOps)
        {
            sb.AppendLine("1. Create an Azure DevOps Personal Access Token:");
            sb.AppendLine("   - Go to Azure DevOps → User settings → Personal access tokens");
            sb.AppendLine("   - Scope: **Work Items** (Read & Write)");
            sb.AppendLine();
            sb.AppendLine("2. Find your ADO project URL and project name:");
            sb.AppendLine("   - Format: `https://dev.azure.com/{organisation}`");
            sb.AppendLine("   - External Project ID = your ADO project name (e.g. `MyProject`)");
            sb.AppendLine();
            sb.AppendLine("3. POST the connection to AgentBoard:");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine($"curl -X POST \"{integrationEndpoint}\" \\");
            sb.AppendLine("  -H \"Content-Type: application/json\" \\");
            sb.AppendLine("  -d '{");
            sb.AppendLine("    \"token\": \"YOUR_ADO_PAT\",");
            sb.AppendLine("    \"repoUrl\": \"https://dev.azure.com/your-organisation\",");
            sb.AppendLine("    \"externalProjectId\": \"YourADOProjectName\"");
            sb.AppendLine("  }'");
            sb.AppendLine("```");
        }
        else
        {
            sb.AppendLine("> No integration configured for this team. Assign a GitHub or Azure DevOps team in AgentBoard first.");
        }

        sb.AppendLine();
        sb.AppendLine("3. Verify the connection:");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine($"curl \"{statusEndpoint}\"");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Expected response when connected:");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"isConnected\": true,");
        sb.AppendLine("  \"connectedAt\": \"2024-01-15T14:32:00Z\",");
        sb.AppendLine("  \"repoUrl\": \"...\",");
        sb.AppendLine("  \"externalProjectId\": \"...\",");
        sb.AppendLine($"  \"webhookUrl\": \"{webhookEndpoint ?? "(not configured)"}\"");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // ── Step 2: Sync ─────────────────────────────────────────────────────
        if (syncEndpoint is not null)
        {
            sb.AppendLine("## Step 2 — Sync todos & features to " + (integrationType == IntegrationType.GitHub ? "GitHub Issues" : "ADO Work Items"));
            sb.AppendLine();
            sb.AppendLine("Run a full sync to push all todos and feature requests as external issues:");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine($"curl -X POST \"{syncEndpoint}\"");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("Response shape:");
            sb.AppendLine("```json");
            sb.AppendLine("{ \"created\": 5, \"updated\": 2, \"failed\": 0, \"errors\": [] }");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // ── Step 3: Webhooks ─────────────────────────────────────────────────
        if (webhookEndpoint is not null)
        {
            sb.AppendLine("## Step 3 — Configure webhook for live events");
            sb.AppendLine();
            sb.AppendLine($"Webhook URL: `{webhookEndpoint}`");
            sb.AppendLine();

            if (integrationType == IntegrationType.GitHub)
            {
                sb.AppendLine("In your GitHub repo:");
                sb.AppendLine("- Settings → Webhooks → Add webhook");
                sb.AppendLine($"- Payload URL: `{webhookEndpoint}`");
                sb.AppendLine("- Content type: `application/json`");
                sb.AppendLine("- Events: **Issues** (opened, closed, edited, reopened)");
            }
            else if (integrationType == IntegrationType.AzureDevOps)
            {
                sb.AppendLine("In your Azure DevOps project:");
                sb.AppendLine("- Project Settings → Service hooks → Create subscription");
                sb.AppendLine("- Service: **Web Hooks**");
                sb.AppendLine("- Trigger: **Work item updated**");
                sb.AppendLine($"- URL: `{webhookEndpoint}`");
            }

            sb.AppendLine();
            sb.AppendLine("Webhooks allow AgentBoard to automatically update todo/feature status when issues are closed or reopened externally.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // ── Step 4: Report progress (live API calls) ─────────────────────────
        sb.AppendLine("## Step 4 — Report work progress to AgentBoard");
        sb.AppendLine();
        sb.AppendLine("Agents should call these endpoints to keep the board live:");
        sb.AppendLine();
        sb.AppendLine("### Claim a todo");
        sb.AppendLine("```bash");
        sb.AppendLine($"curl -X PATCH \"{todosEndpoint}/{{todoId}}\" \\");
        sb.AppendLine("  -H \"Content-Type: application/json\" \\");
        sb.AppendLine("  -d '{ \"claimedBy\": \"agent-name\" }'");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### Update todo status");
        sb.AppendLine("Valid statuses: `Pending`, `InProgress`, `Done`, `Blocked`");
        sb.AppendLine("```bash");
        sb.AppendLine($"curl -X PATCH \"{todosEndpoint}/{{todoId}}\" \\");
        sb.AppendLine("  -H \"Content-Type: application/json\" \\");
        sb.AppendLine("  -d '{ \"status\": \"InProgress\" }'");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### Create a new todo");
        sb.AppendLine("```bash");
        sb.AppendLine($"curl -X POST \"{todosEndpoint}\" \\");
        sb.AppendLine("  -H \"Content-Type: application/json\" \\");
        sb.AppendLine("  -d '{");
        sb.AppendLine($"    \"projectId\": \"{project.Id}\",");
        sb.AppendLine("    \"title\": \"Task title\",");
        sb.AppendLine("    \"description\": \"What needs doing\",");
        sb.AppendLine("    \"priority\": \"Medium\",");
        sb.AppendLine("    \"status\": \"Pending\"");
        sb.AppendLine("  }'");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### List all todos for this project");
        sb.AppendLine("```bash");
        sb.AppendLine($"curl \"{todosEndpoint}?projectId={project.Id}\"");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### Submit a feature request");
        sb.AppendLine("```bash");
        sb.AppendLine($"curl -X POST \"{featuresEndpoint}\" \\");
        sb.AppendLine("  -H \"Content-Type: application/json\" \\");
        sb.AppendLine("  -d '{");
        sb.AppendLine($"    \"projectId\": \"{project.Id}\",");
        sb.AppendLine("    \"title\": \"Feature title\",");
        sb.AppendLine("    \"description\": \"Details of the request\"");
        sb.AppendLine("  }'");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### Push a live progress event");
        sb.AppendLine("Valid EventType values: `Progress`, `Blocked`, `Completed`, `Error`, `Note`, `TestResult`");
        sb.AppendLine("```bash");
        sb.AppendLine($"curl -X POST \"{eventsEndpoint}\" \\");
        sb.AppendLine("  -H \"Content-Type: application/json\" \\");
        sb.AppendLine("  -d '{");
        sb.AppendLine("    \"agentName\": \"backend-agent\",");
        sb.AppendLine("    \"eventType\": \"Progress\",");
        sb.AppendLine("    \"message\": \"Running database migrations\",");
        sb.AppendLine("    \"metadata\": null");
        sb.AppendLine("  }'");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Quick reference");
        sb.AppendLine();
        sb.AppendLine($"| Endpoint | Method | Purpose |");
        sb.AppendLine($"|----------|--------|---------|");
        sb.AppendLine($"| `{integrationEndpoint}` | POST | Connect GitHub/ADO token |");
        sb.AppendLine($"| `{statusEndpoint}` | GET | Check connection + get webhook URL |");
        if (syncEndpoint is not null)
            sb.AppendLine($"| `{syncEndpoint}` | POST | Push todos & features to external tracker |");
        if (webhookEndpoint is not null)
            sb.AppendLine($"| `{webhookEndpoint}` | POST | Receive live events from external tracker |");
        sb.AppendLine($"| `{todosEndpoint}` | GET/POST | List or create todos |");
        sb.AppendLine($"| `{todosEndpoint}/{{id}}` | PATCH | Update todo status / claim |");
        sb.AppendLine($"| `{featuresEndpoint}` | GET/POST | List or submit feature requests |");
        sb.AppendLine($"| `{eventsEndpoint}` | POST | Push a live agent activity event |");
        sb.AppendLine($"| `{eventsEndpoint}` | GET | Fetch recent events for project |");

        return sb.ToString();
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
