using System.ComponentModel.DataAnnotations;

namespace AgentBoard.Data.Models;

public class Project
{
    public Guid Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Goals { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
