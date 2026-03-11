using System.ComponentModel.DataAnnotations;

namespace AgentBoard.Data.Models;

public class Todo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public TodoStatus Status { get; set; } = TodoStatus.Pending;
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;

    [MaxLength(100)]
    public string? AssignedTo { get; set; }

    [MaxLength(100)]
    public string? ClaimedBy { get; set; }

    public DateTime? ClaimedAt { get; set; }
    public DateTime? ClaimExpiresAt { get; set; }
    public DateTime? DueAt { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
