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
    public async Task GetAll_Returns200_WithJsonArray()
    {
        // Uses the shared factory DB — may already contain todos created by other tests.
        // Verifies the endpoint responds 200 and returns a valid JSON array.
        var response = await _client.GetAsync("/api/todos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todos = await response.Content.ReadFromJsonAsync<List<TodoDto>>();
        Assert.NotNull(todos);
    }

    [Fact]
    public async Task GetAll_Returns200_FiltersByTitle_ViaAssignedTo()
    {
        // Create a todo assigned to a unique agent to isolate this test from shared DB state.
        var uniqueAgent = "filter-agent-" + Guid.NewGuid().ToString("N");
        await CreateTodoAsync("Filterable Todo", assignedTo: uniqueAgent);

        var response = await _client.GetAsync($"/api/todos?assignedTo={uniqueAgent}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var todos = await response.Content.ReadFromJsonAsync<List<TodoDto>>();
        Assert.NotNull(todos);
        Assert.Single(todos);
        Assert.Equal(uniqueAgent, todos[0].AssignedTo);
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
}
