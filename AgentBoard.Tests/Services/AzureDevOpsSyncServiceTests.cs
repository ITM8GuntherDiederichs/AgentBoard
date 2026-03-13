using System.Net;
using AgentBoard.Data.Models;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace AgentBoard.Tests.Services;

public class AzureDevOpsSyncServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private const string FakeToken = "fake-ado-pat";
    private const string FakeAdoUrl = "https://dev.azure.com/myorg/myproject";

    private static AzureDevOpsSyncService BuildService(
        out FakeHttpMessageHandler usedHandler,
        string? dbName = null,
        FakeHttpMessageHandler? handler = null)
    {
        handler ??= new FakeHttpMessageHandler();
        usedHandler = handler;
        var factory = TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString());
        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        return new AzureDevOpsSyncService(factory, httpFactory, config);
    }

    private static async Task<(AzureDevOpsSyncService svc, Guid projectId)> SetupProjectAsync(
        string dbName,
        FakeHttpMessageHandler handler,
        int todoCount = 1,
        int featureCount = 0,
        bool todosHaveExternal = false,
        bool featuresHaveExternal = false)
    {
        var factory = TestDbFactory.Create(dbName);
        using var db = await factory.CreateDbContextAsync();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "ADO Test Project",
            IntegrationToken = FakeToken,
            IntegrationRepoUrl = FakeAdoUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.Add(project);

        for (var i = 0; i < todoCount; i++)
        {
            db.Todos.Add(new Todo
            {
                Id = Guid.NewGuid(),
                Title = $"Todo {i + 1}",
                Description = $"Description {i + 1}",
                ProjectId = project.Id,
                Status = TodoStatus.Pending,
                Priority = TodoPriority.Medium,
                ExternalIssueNumber = todosHaveExternal ? 100 + i : null,
                ExternalSystem = todosHaveExternal ? "azuredevops" : null
            });
        }

        for (var i = 0; i < featureCount; i++)
        {
            db.FeatureRequests.Add(new FeatureRequest
            {
                Id = Guid.NewGuid(),
                Title = $"Feature {i + 1}",
                Description = $"Feature desc {i + 1}",
                ProjectId = project.Id,
                Status = FeatureRequestStatus.Proposed,
                Priority = TodoPriority.Medium,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExternalIssueNumber = featuresHaveExternal ? 200 + i : null,
                ExternalSystem = featuresHaveExternal ? "azuredevops" : null
            });
        }

        await db.SaveChangesAsync();

        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        var svc = new AzureDevOpsSyncService(factory, httpFactory, config);
        return (svc, project.Id);
    }

    // -------------------------------------------------------------------------
    // ParseOrgProject tests
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("https://dev.azure.com/myorg/myproject", "myorg", "myproject")]
    [InlineData("https://dev.azure.com/myorg/myproject/", "myorg", "myproject")]
    [InlineData("myorg/myproject", "myorg", "myproject")]
    public void ParseOrgProject_ReturnsCorrectValues(string url, string expectedOrg, string expectedProject)
    {
        var (org, project) = AzureDevOpsSyncService.ParseOrgProject(url);
        Assert.Equal(expectedOrg, org);
        Assert.Equal(expectedProject, project);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("just-a-string")]
    public void ParseOrgProject_ReturnsNulls_ForInvalidInput(string url)
    {
        var (org, project) = AzureDevOpsSyncService.ParseOrgProject(url);
        Assert.Null(org);
        Assert.Null(project);
    }

    // -------------------------------------------------------------------------
    // SyncToAzureDevOpsAsync — creates work items for items without ExternalIssueNumber
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToADO_CreatesWorkItems_ForTodosWithoutExternalIssueNumber()
    {
        var dbName = Guid.NewGuid().ToString();
        var handler = new FakeHttpMessageHandler();
        // ADO create returns {"id": 42, ...}
        handler.EnqueueJson("""{"id": 42, "fields": {"System.Title": "Todo 1"}}""");

        var (svc, projectId) = await SetupProjectAsync(dbName, handler, todoCount: 1);

        var result = await svc.SyncToAzureDevOpsAsync(projectId);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Failed);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("_apis/wit/workitems", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("$Task", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task SyncToADO_CreatesWorkItems_ForFeatureRequestsWithoutExternalIssueNumber()
    {
        var dbName = Guid.NewGuid().ToString();
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson("""{"id": 77, "fields": {"System.Title": "Feature 1"}}""");

        var (svc, projectId) = await SetupProjectAsync(dbName, handler, todoCount: 0, featureCount: 1);

        var result = await svc.SyncToAzureDevOpsAsync(projectId);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Failed);
        Assert.Single(handler.Requests);
        Assert.Contains("$Feature", handler.Requests[0].RequestUri!.ToString());
    }

    // -------------------------------------------------------------------------
    // SyncToAzureDevOpsAsync — updates work items for items with ExternalIssueNumber
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToADO_UpdatesWorkItems_ForTodosWithExistingExternalIssueNumber()
    {
        var dbName = Guid.NewGuid().ToString();
        var handler = new FakeHttpMessageHandler();
        // PATCH response for update
        handler.EnqueueJson("""{"id": 100, "fields": {"System.Title": "Todo 1"}}""");

        var (svc, projectId) = await SetupProjectAsync(dbName, handler, todoCount: 1, todosHaveExternal: true);

        var result = await svc.SyncToAzureDevOpsAsync(projectId);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Failed);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
        Assert.Contains("/100", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task SyncToADO_UpdatesWorkItems_ForFeatureRequestsWithExistingExternalIssueNumber()
    {
        var dbName = Guid.NewGuid().ToString();
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson("""{"id": 200, "fields": {"System.Title": "Feature 1"}}""");

        var (svc, projectId) = await SetupProjectAsync(dbName, handler, todoCount: 0, featureCount: 1, featuresHaveExternal: true);

        var result = await svc.SyncToAzureDevOpsAsync(projectId);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Failed);
        Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
    }

    // -------------------------------------------------------------------------
    // SyncToAzureDevOpsAsync — stores returned work item IDs + sets ExternalSystem
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToADO_StoresReturnedWorkItemId_AndSetsExternalSystem_OnTodo()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        using (var db = await factory.CreateDbContextAsync())
        {
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "P",
                IntegrationToken = FakeToken,
                IntegrationRepoUrl = FakeAdoUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Projects.Add(project);

            db.Todos.Add(new Todo
            {
                Id = Guid.NewGuid(),
                Title = "Store Me",
                ProjectId = project.Id,
                Status = TodoStatus.Pending,
                Priority = TodoPriority.Medium
            });
            await db.SaveChangesAsync();

            var handler = new FakeHttpMessageHandler();
            handler.EnqueueJson("""{"id": 55, "fields": {"System.Title": "Store Me"}}""");

            var httpFactory = new FakeHttpClientFactory(handler);
            var config = Substitute.For<IConfiguration>();
            var svc = new AzureDevOpsSyncService(factory, httpFactory, config);

            await svc.SyncToAzureDevOpsAsync(project.Id);

            // Re-read from DB to verify persistence
            using var verifyDb = await factory.CreateDbContextAsync();
            var todo = await verifyDb.Todos.FirstAsync(t => t.Title == "Store Me");
            Assert.Equal(55, todo.ExternalIssueNumber);
            Assert.Equal("azuredevops", todo.ExternalSystem);
        }
    }

    [Fact]
    public async Task SyncToADO_StoresReturnedWorkItemId_AndSetsExternalSystem_OnFeatureRequest()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        Guid projectId;
        using (var db = await factory.CreateDbContextAsync())
        {
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "P",
                IntegrationToken = FakeToken,
                IntegrationRepoUrl = FakeAdoUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Projects.Add(project);
            projectId = project.Id;

            db.FeatureRequests.Add(new FeatureRequest
            {
                Id = Guid.NewGuid(),
                Title = "Feature Store Me",
                ProjectId = projectId,
                Status = FeatureRequestStatus.Proposed,
                Priority = TodoPriority.Low,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson("""{"id": 99, "fields": {"System.Title": "Feature Store Me"}}""");

        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        var svc = new AzureDevOpsSyncService(factory, httpFactory, config);

        await svc.SyncToAzureDevOpsAsync(projectId);

        using var verifyDb = await factory.CreateDbContextAsync();
        var fr = await verifyDb.FeatureRequests.FirstAsync(f => f.Title == "Feature Store Me");
        Assert.Equal(99, fr.ExternalIssueNumber);
        Assert.Equal("azuredevops", fr.ExternalSystem);
    }

    // -------------------------------------------------------------------------
    // SyncToAzureDevOpsAsync — returns failure when project not found
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToADO_ReturnsError_WhenProjectNotFound()
    {
        var svc = BuildService(out _);
        var result = await svc.SyncToAzureDevOpsAsync(Guid.NewGuid());

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Failed);
        Assert.Contains("Project not found", result.Errors[0]);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync — "Done" state → todo status = done
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleWebhook_DoneState_SetsTodoStatusToDone()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        Guid todoId;
        Guid projectId;

        using (var db = await factory.CreateDbContextAsync())
        {
            var project = new Project { Id = Guid.NewGuid(), Name = "P", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Projects.Add(project);
            projectId = project.Id;

            var todo = new Todo
            {
                Id = Guid.NewGuid(),
                Title = "Close Me",
                ProjectId = projectId,
                ExternalIssueNumber = 10,
                ExternalSystem = "azuredevops",
                Status = TodoStatus.Pending,
                Priority = TodoPriority.Medium
            };
            db.Todos.Add(todo);
            todoId = todo.Id;
            await db.SaveChangesAsync();
        }

        var handler = new FakeHttpMessageHandler();
        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        var svc = new AzureDevOpsSyncService(factory, httpFactory, config);

        var payload = new AzureDevOpsWebhookPayload
        {
            EventType = "workitem.updated",
            Resource = new AzureDevOpsWorkItemResource
            {
                Id = 10,
                Fields = new AzureDevOpsWorkItemFields { State = "Done" }
            }
        };

        var handled = await svc.HandleWebhookAsync(projectId, payload);

        Assert.True(handled);

        using var verify = await factory.CreateDbContextAsync();
        var updatedTodo = await verify.Todos.FindAsync(todoId);
        Assert.Equal(TodoStatus.Done, updatedTodo!.Status);
    }

    [Fact]
    public async Task HandleWebhook_ClosedState_SetsTodoStatusToDone()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        Guid todoId;
        Guid projectId;

        using (var db = await factory.CreateDbContextAsync())
        {
            var project = new Project { Id = Guid.NewGuid(), Name = "P", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Projects.Add(project);
            projectId = project.Id;

            var todo = new Todo
            {
                Id = Guid.NewGuid(),
                Title = "Close Me",
                ProjectId = projectId,
                ExternalIssueNumber = 11,
                ExternalSystem = "azuredevops",
                Status = TodoStatus.InProgress,
                Priority = TodoPriority.Medium
            };
            db.Todos.Add(todo);
            todoId = todo.Id;
            await db.SaveChangesAsync();
        }

        var handler = new FakeHttpMessageHandler();
        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        var svc = new AzureDevOpsSyncService(factory, httpFactory, config);

        var payload = new AzureDevOpsWebhookPayload
        {
            EventType = "workitem.updated",
            Resource = new AzureDevOpsWorkItemResource
            {
                Id = 11,
                Fields = new AzureDevOpsWorkItemFields { State = "Closed" }
            }
        };

        var handled = await svc.HandleWebhookAsync(projectId, payload);

        Assert.True(handled);

        using var verify = await factory.CreateDbContextAsync();
        var updatedTodo = await verify.Todos.FindAsync(todoId);
        Assert.Equal(TodoStatus.Done, updatedTodo!.Status);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync — "Active" / "To Do" state → status restored
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleWebhook_ActiveState_SetsTodoStatusToPending()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        Guid todoId;
        Guid projectId;

        using (var db = await factory.CreateDbContextAsync())
        {
            var project = new Project { Id = Guid.NewGuid(), Name = "P", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Projects.Add(project);
            projectId = project.Id;

            var todo = new Todo
            {
                Id = Guid.NewGuid(),
                Title = "Reactivate Me",
                ProjectId = projectId,
                ExternalIssueNumber = 20,
                ExternalSystem = "azuredevops",
                Status = TodoStatus.Done,
                Priority = TodoPriority.Medium
            };
            db.Todos.Add(todo);
            todoId = todo.Id;
            await db.SaveChangesAsync();
        }

        var handler = new FakeHttpMessageHandler();
        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        var svc = new AzureDevOpsSyncService(factory, httpFactory, config);

        var payload = new AzureDevOpsWebhookPayload
        {
            EventType = "workitem.updated",
            Resource = new AzureDevOpsWorkItemResource
            {
                Id = 20,
                Fields = new AzureDevOpsWorkItemFields { State = "Active" }
            }
        };

        var handled = await svc.HandleWebhookAsync(projectId, payload);

        Assert.True(handled);

        using var verify = await factory.CreateDbContextAsync();
        var updatedTodo = await verify.Todos.FindAsync(todoId);
        Assert.Equal(TodoStatus.Pending, updatedTodo!.Status);
    }

    [Fact]
    public async Task HandleWebhook_ToDoState_SetsTodoStatusToPending()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        Guid todoId;
        Guid projectId;

        using (var db = await factory.CreateDbContextAsync())
        {
            var project = new Project { Id = Guid.NewGuid(), Name = "P", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Projects.Add(project);
            projectId = project.Id;

            var todo = new Todo
            {
                Id = Guid.NewGuid(),
                Title = "Reset Me",
                ProjectId = projectId,
                ExternalIssueNumber = 21,
                ExternalSystem = "azuredevops",
                Status = TodoStatus.Done,
                Priority = TodoPriority.Low
            };
            db.Todos.Add(todo);
            todoId = todo.Id;
            await db.SaveChangesAsync();
        }

        var handler = new FakeHttpMessageHandler();
        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        var svc = new AzureDevOpsSyncService(factory, httpFactory, config);

        var payload = new AzureDevOpsWebhookPayload
        {
            EventType = "workitem.updated",
            Resource = new AzureDevOpsWorkItemResource
            {
                Id = 21,
                Fields = new AzureDevOpsWorkItemFields { State = "To Do" }
            }
        };

        var handled = await svc.HandleWebhookAsync(projectId, payload);

        Assert.True(handled);

        using var verify = await factory.CreateDbContextAsync();
        var updatedTodo = await verify.Todos.FindAsync(todoId);
        Assert.Equal(TodoStatus.Pending, updatedTodo!.Status);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync — non-workitem.updated event → returns false
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleWebhook_NonUpdateEvent_ReturnsFalse()
    {
        var svc = BuildService(out _);
        var payload = new AzureDevOpsWebhookPayload
        {
            EventType = "workitem.created",
            Resource = new AzureDevOpsWorkItemResource
            {
                Id = 1,
                Fields = new AzureDevOpsWorkItemFields { State = "Active" }
            }
        };

        var handled = await svc.HandleWebhookAsync(Guid.NewGuid(), payload);
        Assert.False(handled);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync — unmatched work item ID → returns false
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleWebhook_UnmatchedWorkItemId_ReturnsFalse()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        using var db = await factory.CreateDbContextAsync();
        var project = new Project { Id = Guid.NewGuid(), Name = "P", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var handler = new FakeHttpMessageHandler();
        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        var svc = new AzureDevOpsSyncService(factory, httpFactory, config);

        var payload = new AzureDevOpsWebhookPayload
        {
            EventType = "workitem.updated",
            Resource = new AzureDevOpsWorkItemResource
            {
                Id = 9999,
                Fields = new AzureDevOpsWorkItemFields { State = "Done" }
            }
        };

        var handled = await svc.HandleWebhookAsync(project.Id, payload);
        Assert.False(handled);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync — feature request "Done" → status = done
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleWebhook_DoneState_SetsFeatureRequestStatusToDone()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        Guid frId;
        Guid projectId;

        using (var db = await factory.CreateDbContextAsync())
        {
            var project = new Project { Id = Guid.NewGuid(), Name = "P", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Projects.Add(project);
            projectId = project.Id;

            var fr = new FeatureRequest
            {
                Id = Guid.NewGuid(),
                Title = "Feature Done",
                ProjectId = projectId,
                ExternalIssueNumber = 50,
                ExternalSystem = "azuredevops",
                Status = FeatureRequestStatus.InProgress,
                Priority = TodoPriority.Low,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.FeatureRequests.Add(fr);
            frId = fr.Id;
            await db.SaveChangesAsync();
        }

        var handler = new FakeHttpMessageHandler();
        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        var svc = new AzureDevOpsSyncService(factory, httpFactory, config);

        var payload = new AzureDevOpsWebhookPayload
        {
            EventType = "workitem.updated",
            Resource = new AzureDevOpsWorkItemResource
            {
                Id = 50,
                Fields = new AzureDevOpsWorkItemFields { State = "Done" }
            }
        };

        var handled = await svc.HandleWebhookAsync(projectId, payload);

        Assert.True(handled);

        using var verify = await factory.CreateDbContextAsync();
        var updated = await verify.FeatureRequests.FindAsync(frId);
        Assert.Equal(FeatureRequestStatus.Done, updated!.Status);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync — feature request "Active" → status = Proposed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleWebhook_ActiveState_SetsFeatureRequestStatusToProposed()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        Guid frId;
        Guid projectId;

        using (var db = await factory.CreateDbContextAsync())
        {
            var project = new Project { Id = Guid.NewGuid(), Name = "P", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Projects.Add(project);
            projectId = project.Id;

            var fr = new FeatureRequest
            {
                Id = Guid.NewGuid(),
                Title = "Feature Restore",
                ProjectId = projectId,
                ExternalIssueNumber = 60,
                ExternalSystem = "azuredevops",
                Status = FeatureRequestStatus.Done,
                Priority = TodoPriority.High,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.FeatureRequests.Add(fr);
            frId = fr.Id;
            await db.SaveChangesAsync();
        }

        var handler = new FakeHttpMessageHandler();
        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        var svc = new AzureDevOpsSyncService(factory, httpFactory, config);

        var payload = new AzureDevOpsWebhookPayload
        {
            EventType = "workitem.updated",
            Resource = new AzureDevOpsWorkItemResource
            {
                Id = 60,
                Fields = new AzureDevOpsWorkItemFields { State = "Active" }
            }
        };

        var handled = await svc.HandleWebhookAsync(projectId, payload);

        Assert.True(handled);

        using var verify = await factory.CreateDbContextAsync();
        var updated = await verify.FeatureRequests.FindAsync(frId);
        Assert.Equal(FeatureRequestStatus.Proposed, updated!.Status);
    }
}
