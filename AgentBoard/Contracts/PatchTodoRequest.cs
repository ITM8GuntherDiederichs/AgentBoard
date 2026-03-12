using AgentBoard.Data.Models;

namespace AgentBoard.Contracts;

public class PatchTodoRequest
{
    public TodoStatus? Status { get; set; }
    public TodoPriority? Priority { get; set; }
    public DateTime? DueAt { get; set; }
    /// <summary>Set to a project Guid to assign the todo to that project.</summary>
    public Guid? ProjectId { get; set; }
    /// <summary>Set to true to unassign the todo from its current project (sets ProjectId to null).</summary>
    public bool ClearProjectId { get; set; }
    /// <summary>Set to an agent name to assign ClaimedBy. Use ClearClaimedBy to unassign.</summary>
    public string? ClaimedBy { get; set; }
    /// <summary>Set to true to clear the ClaimedBy field (releases the claim without a TTL check).</summary>
    public bool ClearClaimedBy { get; set; }
}
