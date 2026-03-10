using System.ComponentModel.DataAnnotations;
using AgentBoard.Data.Models;

namespace AgentBoard.Contracts;

public class UpdateTodoRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public TodoStatus Status { get; set; }
    public TodoPriority Priority { get; set; }

    [MaxLength(100)]
    public string? AssignedTo { get; set; }
}
