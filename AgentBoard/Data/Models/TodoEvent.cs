namespace AgentBoard.Data.Models;

/// <summary>
/// Audit event recorded whenever a Todo is created, updated, claimed, released, or deleted.
/// Intentionally has no FK constraint to Todo so events survive todo deletion.
/// </summary>
public class TodoEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TodoId { get; set; }
    public string TodoTitle { get; set; } = "";
    public string EventType { get; set; } = "";
    public string? Actor { get; set; }
    public string? Details { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}