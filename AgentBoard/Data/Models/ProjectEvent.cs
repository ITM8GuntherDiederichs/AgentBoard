namespace AgentBoard.Data.Models;

/// <summary>
/// A live activity event raised by an agent against a project.
/// </summary>
public class ProjectEvent
{
    /// <summary>Unique identifier for the event.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The project this event belongs to.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Optional name of the agent that raised the event.</summary>
    public string? AgentName { get; set; }

    /// <summary>Classification of the event.</summary>
    public ProjectEventType EventType { get; set; }

    /// <summary>Human-readable message describing the event.</summary>
    public string Message { get; set; } = "";

    /// <summary>Optional JSON blob carrying additional structured data.</summary>
    public string? Metadata { get; set; }

    /// <summary>UTC timestamp when the event was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
