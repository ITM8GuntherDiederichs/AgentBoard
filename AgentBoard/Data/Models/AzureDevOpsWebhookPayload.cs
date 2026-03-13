using System.Text.Json.Serialization;

namespace AgentBoard.Data.Models;

/// <summary>Azure DevOps webhook payload for work item events.</summary>
public class AzureDevOpsWebhookPayload
{
    /// <summary>Webhook event type (e.g. "workitem.updated", "workitem.created").</summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = "";

    /// <summary>The work item resource that triggered the event.</summary>
    [JsonPropertyName("resource")]
    public AzureDevOpsWorkItemResource? Resource { get; set; }
}

/// <summary>Work item resource from an Azure DevOps webhook.</summary>
public class AzureDevOpsWorkItemResource
{
    /// <summary>Azure DevOps work item ID.</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Work item field values.</summary>
    [JsonPropertyName("fields")]
    public AzureDevOpsWorkItemFields? Fields { get; set; }
}

/// <summary>Relevant fields from an Azure DevOps work item.</summary>
public class AzureDevOpsWorkItemFields
{
    /// <summary>Work item state (e.g. "Active", "To Do", "Done", "Closed").</summary>
    [JsonPropertyName("System.State")]
    public string? State { get; set; }

    /// <summary>Work item title.</summary>
    [JsonPropertyName("System.Title")]
    public string? Title { get; set; }

    /// <summary>Work item description (HTML).</summary>
    [JsonPropertyName("System.Description")]
    public string? Description { get; set; }
}
