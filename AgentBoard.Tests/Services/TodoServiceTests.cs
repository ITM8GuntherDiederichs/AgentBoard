using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Services;

public class TodoServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TodoService BuildService(string? dbName = null)
        => new(TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString()));

    private static CreateTodoRequest MakeCreateRequest(
        string title = "Test Todo",
        string? description = null,
        TodoPriority priority = TodoPriority.Medium,
        string? assignedTo = null)
        => new()
        {
            Title = title,
            Description = description,
            Priority = priority,
            AssignedTo = assignedTo
        };

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_SetsCorrectTitle()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeCreateRequest(title: "My Task"));
        Assert.Equal("My Task", result.Title);
    }

    [Fact]
    public async Task CreateAsync_DefaultStatusIsPending()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeCreateRequest());
        Assert.Equal(TodoStatus.Pending, result.Status);
    }

    [Fact]
    public async Task CreateAsync_DefaultPriorityIsMedium()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeCreateRequest());
        Assert.Equal(TodoPriority.Medium, result.Priority);
    }

    [Fact]
    public async Task CreateAsync_AssignsNonEmptyId()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeCreateRequest());
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedAt_ToApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeCreateRequest());
        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(result.CreatedAt, before, after);
    }

    [Fact]
    public async Task CreateAsync_StoresRequestedPriority()
    {
        var svc = BuildService();
        var result = await svc.CreateAsync(MakeCreateRequest(priority: TodoPriority.Critical));
        Assert.Equal(TodoPriority.Critical, result.Priority);
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_ReturnsAllTodos_WhenNoFilters()
    {
        var svc = BuildService();
        await svc.CreateAsync(MakeCreateRequest("A"));
        await svc.CreateAsync(MakeCreateRequest("B"));

        var result = await svc.GetAllAsync(null, null, null, null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByStatus_ReturnsMatchingOnly()
    {
        var dbName = Guid.NewGuid().ToString();
        var svc = BuildService(dbName);
        await svc.CreateAsync(MakeCreateRequest("Pending task"));
        var created = await svc.CreateAsync(MakeCreateRequest("InProgress task"));
        await svc.PatchAsync(created.Id, new PatchTodoRequest { Status = TodoStatus.InProgress });

        var result = await svc.GetAllAsync(TodoStatus.InProgress, null, null, null);

        Assert.Single(result);
        Assert.Equal(TodoStatus.InProgress, result[0].Status);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByPriority_ReturnsMatchingOnly()
    {
        var svc = BuildService();
        await svc.CreateAsync(MakeCreateRequest("High task", priority: TodoPriority.High));
        await svc.CreateAsync(MakeCreateRequest("Low task", priority: TodoPriority.Low));

        var result = await svc.GetAllAsync(null, TodoPriority.High, null, null);

        Assert.Single(result);
        Assert.Equal(TodoPriority.High, result[0].Priority);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByAssignedTo_ReturnsMatchingOnly()
    {
        var svc = BuildService();
        await svc.CreateAsync(MakeCreateRequest("Alice task", assignedTo: "alice"));
        await svc.CreateAsync(MakeCreateRequest("Bob task", assignedTo: "bob"));

        var result = await svc.GetAllAsync(null, null, "alice", null);

        Assert.Single(result);
        Assert.Equal("alice", result[0].AssignedTo);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByClaimedBy_ReturnsMatchingOnly()
    {
        var svc = BuildService();
        var t1 = await svc.CreateAsync(MakeCreateRequest("Claimed task"));
        await svc.ClaimAsync(t1.Id, "agent-x");
        await svc.CreateAsync(MakeCreateRequest("Unclaimed task"));

        var result = await svc.GetAllAsync(null, null, null, "agent-x");

        Assert.Single(result);
        Assert.Equal("agent-x", result[0].ClaimedBy);
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ReturnsTodo_WhenFound()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeCreateRequest("Find Me"));

        var result = await svc.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal("Find Me", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var svc = BuildService();
        var result = await svc.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_UpdatesAllFields()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeCreateRequest("Original"));

        var updated = await svc.UpdateAsync(created.Id, new UpdateTodoRequest
        {
            Title = "Updated",
            Description = "New desc",
            Status = TodoStatus.InProgress,
            Priority = TodoPriority.High,
            AssignedTo = "agent-1"
        });

        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Title);
        Assert.Equal("New desc", updated.Description);
        Assert.Equal(TodoStatus.InProgress, updated.Status);
        Assert.Equal(TodoPriority.High, updated.Priority);
        Assert.Equal("agent-1", updated.AssignedTo);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_ForMissingId()
    {
        var svc = BuildService();
        var result = await svc.UpdateAsync(Guid.NewGuid(), new UpdateTodoRequest { Title = "X" });
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // PatchAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PatchAsync_UpdatesOnlyStatus_WhenOnlyStatusProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeCreateRequest(priority: TodoPriority.High));

        var patched = await svc.PatchAsync(created.Id, new PatchTodoRequest { Status = TodoStatus.Done });

        Assert.NotNull(patched);
        Assert.Equal(TodoStatus.Done, patched.Status);
        Assert.Equal(TodoPriority.High, patched.Priority); // unchanged
    }

    [Fact]
    public async Task PatchAsync_UpdatesOnlyPriority_WhenOnlyPriorityProvided()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeCreateRequest());

        var patched = await svc.PatchAsync(created.Id, new PatchTodoRequest { Priority = TodoPriority.Critical });

        Assert.NotNull(patched);
        Assert.Equal(TodoPriority.Critical, patched.Priority);
        Assert.Equal(TodoStatus.Pending, patched.Status); // unchanged
    }

    [Fact]
    public async Task PatchAsync_ReturnsNull_ForMissingId()
    {
        var svc = BuildService();
        var result = await svc.PatchAsync(Guid.NewGuid(), new PatchTodoRequest { Status = TodoStatus.Done });
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesTodo_ReturnsTrue()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeCreateRequest());

        var deleted = await svc.DeleteAsync(created.Id);

        Assert.True(deleted);
        Assert.Null(await svc.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_ForMissingId()
    {
        var svc = BuildService();
        var result = await svc.DeleteAsync(Guid.NewGuid());
        Assert.False(result);
    }

    // -------------------------------------------------------------------------
    // ClaimAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClaimAsync_SetsClaimedByAndClaimedAt()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeCreateRequest());
        var before = DateTime.UtcNow.AddSeconds(-1);

        var (todo, conflict, conflictAgent) = await svc.ClaimAsync(created.Id, "agent-a");

        Assert.NotNull(todo);
        Assert.False(conflict);
        Assert.Null(conflictAgent);
        Assert.Equal("agent-a", todo.ClaimedBy);
        Assert.NotNull(todo.ClaimedAt);
        Assert.True(todo.ClaimedAt >= before);
    }

    [Fact]
    public async Task ClaimAsync_IsIdempotent_WhenSameAgentReclaims()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeCreateRequest());
        await svc.ClaimAsync(created.Id, "agent-a");

        var (todo, conflict, conflictAgent) = await svc.ClaimAsync(created.Id, "agent-a");

        Assert.NotNull(todo);
        Assert.False(conflict);
        Assert.Null(conflictAgent);
        Assert.Equal("agent-a", todo.ClaimedBy);
    }

    [Fact]
    public async Task ClaimAsync_ReturnsConflict_WhenClaimedByAnotherAgent()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeCreateRequest());
        await svc.ClaimAsync(created.Id, "agent-a");

        var (todo, conflict, conflictAgent) = await svc.ClaimAsync(created.Id, "agent-b");

        Assert.NotNull(todo);
        Assert.True(conflict);
        Assert.Equal("agent-a", conflictAgent);
    }

    [Fact]
    public async Task ClaimAsync_ReturnsNullTodo_ForMissingId()
    {
        var svc = BuildService();
        var (todo, conflict, _) = await svc.ClaimAsync(Guid.NewGuid(), "agent-a");
        Assert.Null(todo);
        Assert.False(conflict);
    }

    // -------------------------------------------------------------------------
    // ReleaseClaimAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReleaseClaimAsync_ClearsClaimedByAndClaimedAt()
    {
        var svc = BuildService();
        var created = await svc.CreateAsync(MakeCreateRequest());
        await svc.ClaimAsync(created.Id, "agent-a");

        var released = await svc.ReleaseClaimAsync(created.Id);

        Assert.NotNull(released);
        Assert.Null(released.ClaimedBy);
        Assert.Null(released.ClaimedAt);
    }

    [Fact]
    public async Task ReleaseClaimAsync_ReturnsNull_ForMissingId()
    {
        var svc = BuildService();
        var result = await svc.ReleaseClaimAsync(Guid.NewGuid());
        Assert.Null(result);
    }
}
