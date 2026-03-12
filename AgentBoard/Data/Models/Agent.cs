namespace AgentBoard.Data.Models;

/// <summary>Represents an agent (AI or human) that can be listed as available on the board.</summary>
public class Agent
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name of the agent. Required, max 200 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the agent is AI or Human.</summary>
    public AgentType Type { get; set; }

    /// <summary>Optional description of the agent's role or capabilities.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the agent is currently available for work.</summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>UTC timestamp when the agent was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp when the agent was last updated.</summary>
    public DateTime UpdatedAt { get; set; }
}