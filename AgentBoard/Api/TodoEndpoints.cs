using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Services;

namespace AgentBoard.Api;

public static class TodoEndpoints
{
    public static void MapTodoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/todos").WithTags("todos");

        group.MapGet("/", async (
                TodoService svc,
                TodoStatus? status,
                TodoPriority? priority,
                string? assignedTo,
                string? claimedBy,
                DateTime? dueBefore,
                int page = 1,
                int pageSize = 25)
            => Results.Ok(await svc.GetAllAsync(status, priority, assignedTo, claimedBy, dueBefore, page, pageSize)));

        group.MapGet("/{id:guid}", async (Guid id, TodoService svc) =>
        {
            var todo = await svc.GetByIdAsync(id);
            return todo is null ? Results.NotFound() : Results.Ok(todo);
        });

        group.MapPost("/", async (CreateTodoRequest request, TodoService svc) =>
        {
            var todo = await svc.CreateAsync(request);
            return Results.Created($"/api/todos/{todo.Id}", todo);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateTodoRequest request, TodoService svc) =>
        {
            var todo = await svc.UpdateAsync(id, request);
            return todo is null ? Results.NotFound() : Results.Ok(todo);
        });

        group.MapPatch("/{id:guid}", async (Guid id, PatchTodoRequest request, TodoService svc) =>
        {
            var todo = await svc.PatchAsync(id, request);
            return todo is null ? Results.NotFound() : Results.Ok(todo);
        });

        group.MapDelete("/{id:guid}", async (Guid id, TodoService svc) =>
        {
            var deleted = await svc.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/{id:guid}/claim", async (Guid id, ClaimRequest request, TodoService svc) =>
        {
            var (todo, conflict, conflictAgent) = await svc.ClaimAsync(id, request.AgentId, request.TtlMinutes);
            if (todo is null) return Results.NotFound();
            if (conflict) return Results.Conflict(new { error = $"Todo is already claimed by {conflictAgent}", claimedBy = conflictAgent, claimedAt = todo.ClaimedAt });
            return Results.Ok(todo);
        });

        group.MapDelete("/{id:guid}/claim", async (Guid id, TodoService svc) =>
        {
            var todo = await svc.ReleaseClaimAsync(id);
            return todo is null ? Results.NotFound() : Results.Ok(todo);
        });

        group.MapGet("/{id:guid}/events", async (Guid id, TodoService svc) =>
            Results.Ok(await svc.GetEventsAsync(id)));
    }
}