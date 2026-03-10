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
        await NotifyAsync("updated", todo);
        return todo;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = await db.Todos.FindAsync(id);
        if (todo is null) return false;
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
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
        await NotifyAsync("claimed", todo);
        return (todo, false, null);
    }

    public async Task<Todo?> ReleaseClaimAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = await db.Todos.FindAsync(id);
        if (todo is null) return null;
        todo.ClaimedBy = null;
        todo.ClaimedAt = null;
        todo.ClaimExpiresAt = null;
        await db.SaveChangesAsync();
        await NotifyAsync("released", todo);
        return todo;
    }

    private async Task NotifyAsync(string eventType, Todo todo)
        => await hub.Clients.All.SendAsync("TodoUpdated", new { eventType, todo });
}

