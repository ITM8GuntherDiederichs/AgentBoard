using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>Synchronises AgentBoard todos and feature requests with Azure DevOps Work Items.</summary>
public class AzureDevOpsSyncService(
    IDbContextFactory<ApplicationDbContext> factory,
    IHttpClientFactory httpClientFactory,
    IConfiguration config)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ADO states that map to "done"
    private static readonly HashSet<string> DoneStates =
        new(StringComparer.OrdinalIgnoreCase) { "Done", "Closed", "Resolved", "Completed" };

    // ADO states that map to "active/pending"
    private static readonly HashSet<string> ActiveStates =
        new(StringComparer.OrdinalIgnoreCase) { "Active", "To Do", "New", "Open" };

    // -------------------------------------------------------------------------
    // SyncToAzureDevOpsAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pushes all todos and feature requests for the given project to Azure DevOps Work Items.
    /// Items without an <c>ExternalIssueNumber</c> are created; items with one are updated.
    /// </summary>
    /// <param name="projectId">The AgentBoard project ID.</param>
    /// <returns>A <see cref="SyncResult"/> describing created, updated, and failed counts.</returns>
    public async Task<SyncResult> SyncToAzureDevOpsAsync(Guid projectId)
    {
        using var db = await factory.CreateDbContextAsync();

        var project = await db.Projects.FindAsync(projectId);
        if (project is null)
            return new SyncResult(0, 0, 1, ["Project not found"]);

        if (string.IsNullOrWhiteSpace(project.IntegrationToken) ||
            string.IsNullOrWhiteSpace(project.IntegrationRepoUrl))
            return new SyncResult(0, 0, 0, ["Project has no IntegrationToken or IntegrationRepoUrl configured"]);

        var (org, adoProject) = ParseOrgProject(project.IntegrationRepoUrl);
        if (org is null || adoProject is null)
            return new SyncResult(0, 0, 0,
                [$"Cannot parse org/project from IntegrationRepoUrl: {project.IntegrationRepoUrl}"]);

        var client = CreateAdoClient(project.IntegrationToken);

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
                    var workItemId = await CreateWorkItemAsync(client, org, adoProject,
                        workItemType: "Task",
                        title: todo.Title,
                        description: todo.Description,
                        tags: "agentboard;type:todo");
                    todo.ExternalIssueNumber = workItemId;
                    todo.ExternalSystem = "azuredevops";
                    todo.UpdatedAt = DateTime.UtcNow;
                    created++;
                }
                else
                {
                    await UpdateWorkItemAsync(client, org, adoProject,
                        workItemId: todo.ExternalIssueNumber.Value,
                        title: todo.Title,
                        description: todo.Description);
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
                    var workItemId = await CreateWorkItemAsync(client, org, adoProject,
                        workItemType: "Feature",
                        title: fr.Title,
                        description: fr.Description,
                        tags: "agentboard;type:feature-request");
                    fr.ExternalIssueNumber = workItemId;
                    fr.ExternalSystem = "azuredevops";
                    fr.UpdatedAt = DateTime.UtcNow;
                    created++;
                }
                else
                {
                    await UpdateWorkItemAsync(client, org, adoProject,
                        workItemId: fr.ExternalIssueNumber.Value,
                        title: fr.Title,
                        description: fr.Description);
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
    /// Handles an incoming Azure DevOps webhook payload for a project.
    /// Supports <c>workitem.updated</c> events — maps ADO state changes to AgentBoard todo status.
    /// </summary>
    /// <param name="projectId">The AgentBoard project ID.</param>
    /// <param name="payload">The parsed Azure DevOps webhook payload.</param>
    /// <returns><c>true</c> if the event was matched and handled; <c>false</c> otherwise.</returns>
    public async Task<bool> HandleWebhookAsync(Guid projectId, AzureDevOpsWebhookPayload payload)
    {
        if (payload.EventType != "workitem.updated") return false;
        if (payload.Resource is null) return false;

        var workItemId = payload.Resource.Id;
        var state = payload.Resource.Fields?.State;

        if (string.IsNullOrWhiteSpace(state)) return false;

        using var db = await factory.CreateDbContextAsync();

        // Try to find a matching Todo first, then FeatureRequest
        var todo = await db.Todos
            .FirstOrDefaultAsync(t => t.ProjectId == projectId
                                      && t.ExternalIssueNumber == workItemId
                                      && t.ExternalSystem == "azuredevops");

        var feature = todo is null
            ? await db.FeatureRequests
                .FirstOrDefaultAsync(f => f.ProjectId == projectId
                                          && f.ExternalIssueNumber == workItemId
                                          && f.ExternalSystem == "azuredevops")
            : null;

        if (todo is null && feature is null) return false;

        var handled = false;

        if (DoneStates.Contains(state))
        {
            if (todo is not null)
            {
                todo.Status = TodoStatus.Done;
                todo.UpdatedAt = DateTime.UtcNow;
                handled = true;
            }
            if (feature is not null)
            {
                feature.Status = FeatureRequestStatus.Done;
                feature.UpdatedAt = DateTime.UtcNow;
                handled = true;
            }
        }
        else if (ActiveStates.Contains(state))
        {
            if (todo is not null)
            {
                todo.Status = TodoStatus.Pending;
                todo.UpdatedAt = DateTime.UtcNow;
                handled = true;
            }
            if (feature is not null)
            {
                feature.Status = FeatureRequestStatus.Proposed;
                feature.UpdatedAt = DateTime.UtcNow;
                handled = true;
            }
        }

        if (handled)
            await db.SaveChangesAsync();

        return handled;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private HttpClient CreateAdoClient(string token)
    {
        var client = httpClientFactory.CreateClient("azuredevops");
        // Basic auth: base64(:{PAT})
        var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", encoded);
        return client;
    }

    /// <summary>
    /// Creates a new Azure DevOps work item of the specified type.
    /// POST {org}/{project}/_apis/wit/workitems/${workItemType}?api-version=7.0
    /// </summary>
    private async Task<int> CreateWorkItemAsync(
        HttpClient client,
        string org,
        string adoProject,
        string workItemType,
        string title,
        string? description,
        string tags)
    {
        var patchDoc = new[]
        {
            new { op = "add", path = "/fields/System.Title",       value = (object)title },
            new { op = "add", path = "/fields/System.Description", value = (object)(description ?? "") },
            new { op = "add", path = "/fields/System.Tags",        value = (object)tags }
        };

        var json = JsonSerializer.Serialize(patchDoc, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");

        var url = $"https://dev.azure.com/{org}/{adoProject}/_apis/wit/workitems/${workItemType}?api-version=7.0";
        var response = await client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Updates an existing Azure DevOps work item.
    /// PATCH {org}/{project}/_apis/wit/workitems/{id}?api-version=7.0
    /// </summary>
    private async Task UpdateWorkItemAsync(
        HttpClient client,
        string org,
        string adoProject,
        int workItemId,
        string title,
        string? description)
    {
        var patchDoc = new[]
        {
            new { op = "add", path = "/fields/System.Title",       value = (object)title },
            new { op = "add", path = "/fields/System.Description", value = (object)(description ?? "") }
        };

        var json = JsonSerializer.Serialize(patchDoc, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");

        var url = $"https://dev.azure.com/{org}/{adoProject}/_apis/wit/workitems/{workItemId}?api-version=7.0";
        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Parses org and project from Azure DevOps URL formats:
    /// <c>https://dev.azure.com/{org}/{project}</c> or <c>{org}/{project}</c>.
    /// </summary>
    public static (string? org, string? project) ParseOrgProject(string repoUrl)
    {
        if (string.IsNullOrWhiteSpace(repoUrl)) return (null, null);

        var url = repoUrl.TrimEnd('/');

        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 2)
                return (segments[0], segments[1]);
            return (null, null);
        }

        // e.g. org/project
        var parts = url.Split('/');
        if (parts.Length >= 2)
            return (parts[0], parts[1]);

        return (null, null);
    }
}
