using System.Net;
using System.Net.Http.Json;
using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Tests.Helpers;

namespace AgentBoard.Tests.Api;

/// <summary>
/// Integration tests for all 8 TodoEndpoints routes using WebApplicationFactory.
/// Each test class instance gets its own factory and isolated InMemory database.
///
/// NOTE: The [Timestamp]/RowVersion concurrency token on Todo is not enforced by EF Core InMemory.
/// Optimistic concurrency behaviour must be verified against a relational database.
/// </summary>
public class TodoEndpointsTests : IClassFixture<AgentBoardWebFactory>
{
    private readonly HttpClient _client;

    public TodoEndpointsTests(AgentBoardWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // GET /api/todos
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAll_Returns200_WithPagedResult()
    {
        // Uses the shared factory DB — may already contain todos created by other tests.
        // Verifies the endpoint responds 200 and returns a valid PagedResult.
        var response = await _client.GetAsync("/api/todos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<TodoDto>>();
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.True(result.Page >= 1);
        Assert.True(result.PageSize >= 1);
        Assert.True(result.TotalCount >= 0);
        Assert.True(result.TotalPages >= 0);
    }

    [Fact]
    public async Task GetAll_Returns200_FiltersByTitle_ViaAssignedTo()
    {
        // Create a todo assigned to a unique agent to isolate this test from shared DB state.
        var uniqueAgent = "filter-agent-" + Guid.NewGuid().ToString("N");
        await CreateTodoAsync("Filterable Todo", assignedTo: uniqueAgent);

        var response = await _client.GetAsync($"/api/todos?assignedTo={uniqueAgent}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<TodoDto>>();
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(uniqueAgent, result.Items[0].AssignedTo);
    }

    [Fact]
    public async Task GetAll_Returns_DefaultPage1_PageSize25()
    {
        var response = await _client.GetAsync("/api/todos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<TodoDto>>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
    }

    [Fact]
    public async Task GetAll_RespectsPageAndPageSizeParams()
    {
        var response = await _client.GetAsync("/api/todos?page=2&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<TodoDto>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task GetAll_ClampsPageSizeTo100()
    {
        var response = await _client.GetAsync("/api/todos?pageSize=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<TodoDto>>();
        Assert.NotNull(result);
        Assert.Equal(100, result.PageSize);
    }

    // -------------------------------------------------------------------------
    // POST /api/todos
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Post_Returns201_WithLocationHeader_AndCorrectBody()
    {
        var request = new CreateTodoRequest { Title = "Integration Todo", Priority = TodoPriority.High };

        var response = await _client.PostAsJsonAsync("/api/todos", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/todos/", response.Headers.Location!.ToString());

        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.NotNull(todo);
        Assert.Equal("Integration Todo", todo.Title);
        Assert.Equal(TodoPriority.High, todo.Priority);
        Assert.Equal(TodoStatus.Pending, todo.Status);
        Assert.NotEqual(Guid.Empty, todo.Id);
    }

    // -------------------------------------------------------------------------
    // GET /api/todos/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetById_Returns200_ForExistingTodo()
    {
        var created = await CreateTodoAsync("GetById Todo");

        var response = await _client.GetAsync($"/api/todos/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.NotNull(todo);
        Assert.Equal("GetById Todo", todo.Title);
    }

    [Fact]
    public async Task GetById_Returns404_ForMissingId()
    {
        var response = await _client.GetAsync($"/api/todos/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PUT /api/todos/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Put_Returns200_WithUpdatedTodo()
    {
        var created = await CreateTodoAsync("Original Title");
        var updateRequest = new UpdateTodoRequest
        {
            Title = "Updated Title",
            Description = "Updated desc",
            Status = TodoStatus.InProgress,
            Priority = TodoPriority.Critical,
            AssignedTo = "backend-agent"
        };

        var response = await _client.PutAsJsonAsync($"/api/todos/{created.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.NotNull(todo);
        Assert.Equal("Updated Title", todo.Title);
        Assert.Equal(TodoStatus.InProgress, todo.Status);
        Assert.Equal(TodoPriority.Critical, todo.Priority);
        Assert.Equal("backend-agent", todo.AssignedTo);
    }

    [Fact]
    public async Task Put_Returns404_ForMissingId()
    {
        var request = new UpdateTodoRequest { Title = "Ghost" };
        var response = await _client.PutAsJsonAsync($"/api/todos/{Guid.NewGuid()}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PATCH /api/todos/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Patch_Returns200_WithUpdatedStatus()
    {
        var created = await CreateTodoAsync("Patch Me");
        var patchRequest = new PatchTodoRequest { Status = TodoStatus.Done };

        var response = await _client.PatchAsJsonAsync($"/api/todos/{created.Id}", patchRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.NotNull(todo);
        Assert.Equal(TodoStatus.Done, todo.Status);
    }

    [Fact]
    public async Task Patch_Returns404_ForMissingId()
    {
        var request = new PatchTodoRequest { Status = TodoStatus.Done };
        var response = await _client.PatchAsJsonAsync($"/api/todos/{Guid.NewGuid()}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // DELETE /api/todos/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Returns204_ForExistingTodo()
    {
        var created = await CreateTodoAsync("Delete Me");

        var response = await _client.DeleteAsync($"/api/todos/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify it is gone
        var getResponse = await _client.GetAsync($"/api/todos/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_ForMissingId()
    {
        var response = await _client.DeleteAsync($"/api/todos/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // POST /api/todos/{id}/claim
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Claim_Returns200_AndSetsClaimedBy()
    {
        var created = await CreateTodoAsync("Claim Me");
        var claimRequest = new ClaimRequest { AgentId = "qa-agent" };

        var response = await _client.PostAsJsonAsync($"/api/todos/{created.Id}/claim", claimRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.NotNull(todo);
        Assert.Equal("qa-agent", todo.ClaimedBy);
        Assert.NotNull(todo.ClaimedAt);
    }

    [Fact]
    public async Task Claim_Returns409_WhenAlreadyClaimedByAnotherAgent()
    {
        var created = await CreateTodoAsync("Contested Todo");

        // First agent claims
        await _client.PostAsJsonAsync($"/api/todos/{created.Id}/claim", new ClaimRequest { AgentId = "agent-1" });

        // Second agent tries to claim
        var response = await _client.PostAsJsonAsync($"/api/todos/{created.Id}/claim", new ClaimRequest { AgentId = "agent-2" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Claim_Returns404_ForMissingId()
    {
        var response = await _client.PostAsJsonAsync($"/api/todos/{Guid.NewGuid()}/claim",
            new ClaimRequest { AgentId = "agent-x" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // DELETE /api/todos/{id}/claim
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReleaseClaim_Returns200_AndClearsClaim()
    {
        var created = await CreateTodoAsync("Release Me");
        await _client.PostAsJsonAsync($"/api/todos/{created.Id}/claim", new ClaimRequest { AgentId = "qa-agent" });

        var response = await _client.DeleteAsync($"/api/todos/{created.Id}/claim");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.NotNull(todo);
        Assert.Null(todo.ClaimedBy);
        Assert.Null(todo.ClaimedAt);
    }

    [Fact]
    public async Task ReleaseClaim_Returns404_ForMissingId()
    {
        var response = await _client.DeleteAsync($"/api/todos/{Guid.NewGuid()}/claim");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // GET /api/todos/{id}/events
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetEvents_Returns200_WithEmptyList_ForNewTodo()
    {
        // A freshly created todo should have at least the "Created" audit event.
        var created = await CreateTodoAsync("Events Todo");

        var response = await _client.GetAsync($"/api/todos/{created.Id}/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<TodoEventDto>>();
        Assert.NotNull(events);
        // CreateAsync records a "Created" event
        Assert.Contains(events, e => e.EventType == "Created");
    }

    [Fact]
    public async Task GetEvents_RecordsCreatedEvent_OnCreate()
    {
        var created = await CreateTodoAsync("Audit Create");

        var events = await GetEventsAsync(created.Id);

        Assert.Single(events, e => e.EventType == "Created");
        Assert.All(events.Where(e => e.EventType == "Created"), e =>
            Assert.Equal(created.Id, e.TodoId));
    }

    [Fact]
    public async Task GetEvents_RecordsUpdatedEvent_OnPut()
    {
        var created = await CreateTodoAsync("Audit Update");
        var updateRequest = new UpdateTodoRequest
        {
            Title = "Updated Title",
            Status = TodoStatus.InProgress,
            Priority = TodoPriority.High
        };

        await _client.PutAsJsonAsync($"/api/todos/{created.Id}", updateRequest);

        var events = await GetEventsAsync(created.Id);
        Assert.Contains(events, e => e.EventType == "Updated");
    }

    [Fact]
    public async Task GetEvents_RecordsClaimedEvent_OnClaim()
    {
        var created = await CreateTodoAsync("Audit Claim");

        await _client.PostAsJsonAsync($"/api/todos/{created.Id}/claim",
            new ClaimRequest { AgentId = "audit-agent" });

        var events = await GetEventsAsync(created.Id);
        var claimedEvent = Assert.Single(events, e => e.EventType == "Claimed");
        Assert.Equal("audit-agent", claimedEvent.Actor);
    }

    [Fact]
    public async Task GetEvents_RecordsReleasedEvent_OnReleaseClaim()
    {
        var created = await CreateTodoAsync("Audit Release");
        await _client.PostAsJsonAsync($"/api/todos/{created.Id}/claim",
            new ClaimRequest { AgentId = "audit-agent" });

        await _client.DeleteAsync($"/api/todos/{created.Id}/claim");

        var events = await GetEventsAsync(created.Id);
        Assert.Contains(events, e => e.EventType == "Released");
    }

    [Fact]
    public async Task GetEvents_Returns200_ForDeletedTodo_EventsSurvive()
    {
        // Events must survive todo deletion (no FK constraint).
        var created = await CreateTodoAsync("Audit Delete");
        var todoId = created.Id;

        await _client.DeleteAsync($"/api/todos/{todoId}");

        // The todo is gone but events endpoint still returns events
        var response = await _client.GetAsync($"/api/todos/{todoId}/events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<TodoEventDto>>();
        Assert.NotNull(events);
        Assert.Contains(events, e => e.EventType == "Deleted");
    }

    [Fact]
    public async Task GetEvents_ReturnsEventsOrderedByOccurredAtDescending()
    {
        var created = await CreateTodoAsync("Audit Order");

        // Create two more events by patching
        await _client.PatchAsJsonAsync($"/api/todos/{created.Id}",
            new PatchTodoRequest { Status = TodoStatus.InProgress });
        await _client.PatchAsJsonAsync($"/api/todos/{created.Id}",
            new PatchTodoRequest { Status = TodoStatus.Done });

        var events = await GetEventsAsync(created.Id);
        Assert.True(events.Count >= 2);
        for (var i = 0; i < events.Count - 1; i++)
            Assert.True(events[i].OccurredAt >= events[i + 1].OccurredAt);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<TodoDto> CreateTodoAsync(string title, TodoPriority priority = TodoPriority.Medium, string? assignedTo = null)
    {
        var response = await _client.PostAsJsonAsync("/api/todos", new CreateTodoRequest
        {
            Title = title,
            Priority = priority,
            AssignedTo = assignedTo
        });
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        return todo!;
    }

    private async Task<List<TodoEventDto>> GetEventsAsync(Guid todoId)
    {
        var response = await _client.GetAsync($"/api/todos/{todoId}/events");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<TodoEventDto>>())!;
    }

    /// <summary>
    /// Local DTO matching the PagedResult shape returned by GET /api/todos.
    /// </summary>
    private sealed record PagedResultDto<T>(
        List<T> Items,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages);

    /// <summary>
    /// Local DTO matching the shape returned by the API (the Todo entity is serialised directly).
    /// </summary>
    private sealed record TodoDto(
        Guid Id,
        string Title,
        string? Description,
        TodoStatus Status,
        TodoPriority Priority,
        string? AssignedTo,
        string? ClaimedBy,
        DateTime? ClaimedAt,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    /// <summary>
    /// Local DTO matching the TodoEvent shape returned by GET /api/todos/{id}/events.
    /// </summary>
    private sealed record TodoEventDto(
        Guid Id,
        Guid TodoId,
        string TodoTitle,
        string EventType,
        string? Actor,
        string? Details,
        DateTime OccurredAt);
}
