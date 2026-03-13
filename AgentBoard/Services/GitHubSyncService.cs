using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AgentBoard.Services;

/// <summary>Synchronises AgentBoard todos and feature requests with GitHub Issues.</summary>
public class GitHubSyncService(
    IDbContextFactory<ApplicationDbContext> factory,
    IHttpClientFactory httpClientFactory,
    IConfiguration config)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // -------------------------------------------------------------------------
    // SyncToGitHubAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pushes all todos and feature requests for the given project to GitHub Issues.
    /// Items without an <c>ExternalIssueNumber</c> are created; items with one are updated.
    /// </summary>
    /// <param name="projectId">The AgentBoard project ID.</param>
    /// <returns>A <see cref="SyncResult"/> describing created, updated, and failed counts.</returns>
    public async Task<SyncResult> SyncToGitHubAsync(Guid projectId)
    {
        using var db = await factory.CreateDbContextAsync();

        var project = await db.Projects.FindAsync(projectId);
        if (project is null)
            return new SyncResult(0, 0, 1, ["Project not found"]);

        if (string.IsNullOrWhiteSpace(project.IntegrationToken) ||
            string.IsNullOrWhiteSpace(project.IntegrationRepoUrl))
            return new SyncResult(0, 0, 0, ["Project has no IntegrationToken or IntegrationRepoUrl configured"]);

        var (owner, repo) = ParseOwnerRepo(project.IntegrationRepoUrl);
        if (owner is null || repo is null)
            return new SyncResult(0, 0, 0, [$"Cannot parse owner/repo from IntegrationRepoUrl: {project.IntegrationRepoUrl}"]);

        var client = CreateGitHubClient(project.IntegrationToken);

        var todos = await db.Todos
            .Where(t => t.ProjectId == projectId)
            .ToListAsync();

        var features = await db.FeatureRequests
            .Where(f => f.ProjectId == projectId)
            .ToListAsync();

        var errors = new List<string>();
        int created = 0, updated = 0, failed = 0;

        // --- sync todos ---
        foreach (var todo in todos)
        {
            try
            {
                if (todo.ExternalIssueNumber is null)
                {
                    var issueNum = await CreateGitHubIssueAsync(client, owner, repo, todo.Title, todo.Description,
                        ["agentboard", "type:todo", $"priority:{todo.Priority.ToString().ToLower()}"]);
                    todo.ExternalIssueNumber = issueNum;
                    todo.ExternalSystem = "github";
                    todo.UpdatedAt = DateTime.UtcNow;
                    created++;
                }
                else
                {
                    await UpdateGitHubIssueAsync(client, owner, repo, todo.ExternalIssueNumber.Value,
                        todo.Title, todo.Description);
                    todo.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Todo {todo.Id} ({todo.Title}): {ex.Message}");
            }
        }

        // --- sync feature requests ---
        foreach (var fr in features)
        {
            try
            {
                if (fr.ExternalIssueNumber is null)
                {
                    var issueNum = await CreateGitHubIssueAsync(client, owner, repo, fr.Title, fr.Description,
                        ["agentboard", "type:feature-request"]);
                    fr.ExternalIssueNumber = issueNum;
                    fr.ExternalSystem = "github";
                    fr.UpdatedAt = DateTime.UtcNow;
                    created++;
                }
                else
                {
                    await UpdateGitHubIssueAsync(client, owner, repo, fr.ExternalIssueNumber.Value,
                        fr.Title, fr.Description);
                    fr.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"FeatureRequest {fr.Id} ({fr.Title}): {ex.Message}");
            }
        }

        await db.SaveChangesAsync();

        return new SyncResult(created, updated, failed, [.. errors]);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Handles an incoming GitHub webhook payload for a project.
    /// Supports <c>closed</c>, <c>reopened</c>, and <c>edited</c> actions.
    /// </summary>
    /// <param name="projectId">The AgentBoard project ID.</param>
    /// <param name="payload">The parsed GitHub webhook payload.</param>
    /// <returns><c>true</c> if the event was matched and handled; <c>false</c> otherwise.</returns>
    public async Task<bool> HandleWebhookAsync(Guid projectId, GitHubWebhookPayload payload)
    {
        if (payload.Issue is null) return false;

        var issueNumber = payload.Issue.Number;
        var action = payload.Action?.ToLower();

        using var db = await factory.CreateDbContextAsync();

        // Try to find a matching Todo first, then FeatureRequest.
        var todo = await db.Todos
            .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.ExternalIssueNumber == issueNumber);

        var feature = todo is null
            ? await db.FeatureRequests
                .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.ExternalIssueNumber == issueNumber)
            : null;

        if (todo is null && feature is null) return false;

        var handled = false;

        switch (action)
        {
            case "closed":
                if (todo is not null) { todo.Status = TodoStatus.Done; todo.UpdatedAt = DateTime.UtcNow; handled = true; }
                if (feature is not null) { feature.Status = FeatureRequestStatus.Done; feature.UpdatedAt = DateTime.UtcNow; handled = true; }
                break;

            case "reopened":
                if (todo is not null) { todo.Status = TodoStatus.Pending; todo.UpdatedAt = DateTime.UtcNow; handled = true; }
                if (feature is not null) { feature.Status = FeatureRequestStatus.Proposed; feature.UpdatedAt = DateTime.UtcNow; handled = true; }
                break;

            case "edited":
                if (todo is not null)
                {
                    todo.Title = payload.Issue.Title;
                    todo.Description = payload.Issue.Body;
                    todo.UpdatedAt = DateTime.UtcNow;
                    handled = true;
                }
                if (feature is not null)
                {
                    feature.Title = payload.Issue.Title;
                    feature.Description = payload.Issue.Body;
                    feature.UpdatedAt = DateTime.UtcNow;
                    handled = true;
                }
                break;
        }

        if (handled)
            await db.SaveChangesAsync();

        return handled;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private HttpClient CreateGitHubClient(string token)
    {
        var client = httpClientFactory.CreateClient("github");
        client.BaseAddress = new Uri("https://api.github.com");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("token", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AgentBoard/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private async Task<int> CreateGitHubIssueAsync(
        HttpClient client, string owner, string repo,
        string title, string? body, string[] labels)
    {
        var payload = new { title, body, labels };
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"/repos/{owner}/{repo}/issues", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("number").GetInt32();
    }

    private async Task UpdateGitHubIssueAsync(
        HttpClient client, string owner, string repo,
        int issueNumber, string title, string? body)
    {
        var payload = new { title, body };
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/repos/{owner}/{repo}/issues/{issueNumber}") { Content = content };
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Parses owner and repo from formats:
    /// <c>https://github.com/{owner}/{repo}</c> or <c>{owner}/{repo}</c>.
    /// </summary>
    public static (string? owner, string? repo) ParseOwnerRepo(string repoUrl)
    {
        if (string.IsNullOrWhiteSpace(repoUrl)) return (null, null);

        // Strip trailing slashes and .git suffix
        var url = repoUrl.TrimEnd('/').Replace(".git", "", StringComparison.OrdinalIgnoreCase);

        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            // e.g. https://github.com/owner/repo
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 2)
                return (segments[0], segments[1]);
            return (null, null);
        }

        // e.g. owner/repo
        var parts = url.Split('/');
        if (parts.Length >= 2)
            return (parts[0], parts[1]);

        return (null, null);
    }
}
