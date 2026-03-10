using AgentBoard.Data.Models;

namespace AgentBoard.Contracts;

public class PatchTodoRequest
{
    public TodoStatus? Status { get; set; }
    public TodoPriority? Priority { get; set; }
}
