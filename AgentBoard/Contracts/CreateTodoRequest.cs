using System.ComponentModel.DataAnnotations;
using AgentBoard.Data.Models;

namespace AgentBoard.Contracts;

public class CreateTodoRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public TodoPriority Priority { get; set; } = TodoPriority.Medium;

    [MaxLength(100)]
    public string? AssignedTo { get; set; }
}
