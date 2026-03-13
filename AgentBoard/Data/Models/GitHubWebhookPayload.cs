using System.Text.Json.Serialization;

namespace AgentBoard.Data.Models;

/// <summary>GitHub webhook payload for issue events.</summary>
public class GitHubWebhookPayload
{
    /// <summary>Webhook action (e.g. "opened", "closed", "reopened", "edited").</summary>
    public string Action { get; set; } = "";

    /// <summary>The GitHub issue that triggered the event.</summary>
    public GitHubIssue? Issue { get; set; }
}

/// <summary>Represents a GitHub issue returned from the API or webhook.</summary>
public class GitHubIssue
{
    /// <summary>GitHub issue number.</summary>
    public int Number { get; set; }

    /// <summary>Issue title.</summary>
    public string Title { get; set; } = "";

    /// <summary>Issue body / description.</summary>
    public string? Body { get; set; }

    /// <summary>Issue state ("open" or "closed").</summary>
    public string State { get; set; } = "";
}
