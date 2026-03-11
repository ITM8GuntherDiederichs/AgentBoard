using AgentBoard.Contracts;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using AgentBoard.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

public class TodoService(IDbContextFactory<ApplicationDbContext> factory, IHubContext<AgentBoardHub> hub)
{
    public async Task<List<Todo>> GetAllAsync(TodoStatus? status, TodoPriority? priority, string? assignedTo, string? claimedBy, DateTime? dueBefore = null)
    {
        using var db = await factory.CreateDbContextAsync();
        var q = db.Todos.AsQueryable();
        if (status.HasValue) q = q.Where(t => t.Status == status.Value);
        if (priority.HasValue) q = q.Where(t => t.Priority == priority.Value);
        if (!string.IsNullOrEmpty(assignedTo)) q = q.Where(t => t.AssignedTo == assignedTo);
        if (!string.IsNullOrEmpty(claimedBy)) q = q.Where(t => t.ClaimedBy == claimedBy);
        if (dueBefore.HasValue) q = q.Where(t => t.DueAt.HasValue && t.DueAt.Value <= dueBefore.Value);
        return await q.OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt).ToListAsync();
    }

    public async Task<Todo?> GetByIdAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Todos.FindAsync(id);
    }

    /// <summary>Returns audit events for a todo ordered by OccurredAt descending, max <paramref name="max"/> events.</summary>
    public async Task<List<TodoEvent>> GetEventsAsync(Guid todoId, int max = 20)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.TodoEvents
            .Where(e => e.TodoId == todoId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(max)
            .ToListAsync();
    }

    public async Task<Todo> CreateAsync(CreateTodoRequest request)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = new Todo
        {
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            AssignedTo = request.AssignedTo,
            DueAt = request.DueAt
        };
        db.Todos.Add(todo);
        await db.SaveChangesAsync();
        await LogEventAsync(db, todo.Id, todo.Title, "Created",
            details: "Priority=" + todo.Priority + ", AssignedTo=" + (todo.AssignedTo ?? "none"));
        await NotifyAsync("created", todo);
        return todo;
    }

    public async Task<Todo?> UpdateAsync(Guid id, UpdateTodoRequest request)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = await db.Todos.FindAsync(id);
        if (todo is null) return null;
        todo.Title = request.Title;
        todo.Description = request.Description;
        todo.Status = request.Status;
        todo.Priority = request.Priority;
        todo.AssignedTo = request.AssignedTo;
        todo.DueAt = request.DueAt;
        await db.SaveChangesAsync();
        await LogEventAsync(db, todo.Id, todo.Title, "Updated",
            details: "Status=" + todo.Status + ", Priority=" + todo.Priority);
        await NotifyAsync("updated", todo);
        return todo;
    }

    public async Task<Todo?> PatchAsync(Guid id, PatchTodoRequest request)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = await db.Todos.FindAsync(id);
        if (todo is null) return null;
        if (request.Status.HasValue) todo.Status = request.Status.Value;
        if (request.Priority.HasValue) todo.Priority = request.Priority.Value;
        if (request.DueAt.HasValue) todo.DueAt = request.DueAt.Value;
        await db.SaveChangesAsync();
        await LogEventAsync(db, todo.Id, todo.Title, "Patched",
            details: "Status=" + todo.Status + ", Priority=" + todo.Priority);
        await NotifyAsync("updated", todo);
        return todo;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = await db.Todos.FindAsync(id);
        if (todo is null) return false;
        var title = todo.Title;
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
        await LogEventAsync(db, id, title, "Deleted");
        await NotifyAsync("deleted", new Todo { Id = id });
        return true;
    }

    public async Task<(Todo? todo, bool conflict, string? conflictAgent)> ClaimAsync(Guid id, string agentId, int ttlMinutes = 30)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = await db.Todos.FindAsync(id);
        if (todo is null) return (null, false, null);
        if (!string.IsNullOrEmpty(todo.ClaimedBy) && todo.ClaimedBy != agentId)
            return (todo, true, todo.ClaimedBy);
        todo.ClaimedBy = agentId;
        todo.ClaimedAt = DateTime.UtcNow;
        todo.ClaimExpiresAt = DateTime.UtcNow.AddMinutes(ttlMinutes);
        await db.SaveChangesAsync();
        await LogEventAsync(db, todo.Id, todo.Title, "Claimed",
            actor: agentId,
            details: "TtlMinutes=" + ttlMinutes);
        await NotifyAsync("claimed", todo);
        return (todo, false, null);
    }

    public async Task<Todo?> ReleaseClaimAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = await db.Todos.FindAsync(id);
        if (todo is null) return null;
        var previousClaimant = todo.ClaimedBy;
        todo.ClaimedBy = null;
        todo.ClaimedAt = null;
        todo.ClaimExpiresAt = null;
        await db.SaveChangesAsync();
        await LogEventAsync(db, todo.Id, todo.Title, "Released",
            details: previousClaimant != null ? "ReleasedFrom=" + previousClaimant : null);
        await NotifyAsync("released", todo);
        return todo;
    }

    /// <summary>Records a <see cref="TodoEvent"/> to the audit log using an existing DbContext.</summary>
    private static async Task LogEventAsync(
        ApplicationDbContext db,
        Guid todoId,
        string todoTitle,
        string eventType,
        string? actor = null,
        string? details = null)
    {
        db.TodoEvents.Add(new TodoEvent
        {
            TodoId = todoId,
            TodoTitle = todoTitle,
            EventType = eventType,
            Actor = actor,
            Details = details
        });
        await db.SaveChangesAsync();
    }

    private async Task NotifyAsync(string eventType, Todo todo)
        => await hub.Clients.All.SendAsync("TodoUpdated", new { eventType, todo });
}
