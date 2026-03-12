namespace AgentBoard.Data.Models;

/// <summary>A reusable skill definition that can be assigned to agents and teams.</summary>
public class Skill
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name of the skill. Required, max 200 characters.</summary>
    public string Name { get; set; } = "";

    /// <summary>Markdown content describing the skill.</summary>
    public string Content { get; set; } = "";

    /// <summary>UTC timestamp when the skill was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp when the skill was last updated.</summary>
    public DateTime UpdatedAt { get; set; }
}
