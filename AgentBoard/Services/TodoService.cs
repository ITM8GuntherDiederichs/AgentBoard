using AgentBoard.Contracts;
using AgentBoard.Data;
using AgentBoard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

public class TodoService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<List<Todo>> GetAllAsync(TodoStatus? status, TodoPriority? priority, string? assignedTo, string? claimedBy)
    {
        using var db = await factory.CreateDbContextAsync();
        var q = db.Todos.AsQueryable();
        if (status.HasValue) q = q.Where(t => t.Status == status.Value);
        if (priority.HasValue) q = q.Where(t => t.Priority == priority.Value);
        if (!string.IsNullOrEmpty(assignedTo)) q = q.Where(t => t.AssignedTo == assignedTo);
        if (!string.IsNullOrEmpty(claimedBy)) q = q.Where(t => t.ClaimedBy == claimedBy);
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
            AssignedTo = request.AssignedTo
        };
        db.Todos.Add(todo);
        await db.SaveChangesAsync();
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
        await db.SaveChangesAsync();
        return todo;
    }

    public async Task<Todo?> PatchAsync(Guid id, PatchTodoRequest request)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = await db.Todos.FindAsync(id);
        if (todo is null) return null;
        if (request.Status.HasValue) todo.Status = request.Status.Value;
        if (request.Priority.HasValue) todo.Priority = request.Priority.Value;
        await db.SaveChangesAsync();
        return todo;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = await db.Todos.FindAsync(id);
        if (todo is null) return false;
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<(Todo? todo, bool conflict, string? conflictAgent)> ClaimAsync(Guid id, string agentId)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = await db.Todos.FindAsync(id);
        if (todo is null) return (null, false, null);
        if (!string.IsNullOrEmpty(todo.ClaimedBy) && todo.ClaimedBy != agentId)
            return (todo, true, todo.ClaimedBy);
        todo.ClaimedBy = agentId;
        todo.ClaimedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (todo, false, null);
    }

    public async Task<Todo?> ReleaseClaimAsync(Guid id)
    {
        using var db = await factory.CreateDbContextAsync();
        var todo = await db.Todos.FindAsync(id);
        if (todo is null) return null;
        todo.ClaimedBy = null;
        todo.ClaimedAt = null;
        await db.SaveChangesAsync();
        return todo;
    }
}
