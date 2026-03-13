using AgentBoard.Data.Models;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace AgentBoard.Tests.Services;

public class GitHubSyncServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private const string FakeToken = "ghp_testtoken123";
    private const string FakeRepoUrl = "https://github.com/test-owner/test-repo";

    private static GitHubSyncService BuildService(
        out FakeHttpMessageHandler usedHandler,
        string? dbName = null,
        FakeHttpMessageHandler? handler = null)
    {
        handler ??= new FakeHttpMessageHandler();
        usedHandler = handler;
        var factory = TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString());
        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        return new GitHubSyncService(factory, httpFactory, config);
    }

    private static async Task<(GitHubSyncService svc, Guid projectId)> SetupProjectWithTodosAsync(
        string dbName,
        FakeHttpMessageHandler handler,
        int todoCount = 1,
        int featureCount = 0)
    {
        var factory = TestDbFactory.Create(dbName);
        using var db = await factory.CreateDbContextAsync();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            IntegrationToken = FakeToken,
            IntegrationRepoUrl = FakeRepoUrl,
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
                Priority = TodoPriority.Medium
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
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();

        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        var svc = new GitHubSyncService(factory, httpFactory, config);
        return (svc, project.Id);
    }

    // -------------------------------------------------------------------------
    // ParseOwnerRepo tests (internal, called via reflection pattern — test via SyncToGitHub)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("https://github.com/owner/repo", "owner", "repo")]
    [InlineData("https://github.com/owner/repo.git", "owner", "repo")]
    [InlineData("owner/repo", "owner", "repo")]
    [InlineData("http://github.com/owner/repo/", "owner", "repo")]
    public void ParseOwnerRepo_ReturnsCorrectValues(string url, string expectedOwner, string expectedRepo)
    {
        var (owner, repo) = GitHubSyncService.ParseOwnerRepo(url);
        Assert.Equal(expectedOwner, owner);
        Assert.Equal(expectedRepo, repo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("just-a-string")]
    public void ParseOwnerRepo_ReturnsNulls_ForInvalidInput(string url)
    {
        var (owner, repo) = GitHubSyncService.ParseOwnerRepo(url);
        Assert.Null(owner);
        Assert.Null(repo);
    }

    // -------------------------------------------------------------------------
    // SyncToGitHubAsync — creates issues for items without ExternalIssueNumber
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToGitHub_CreatesIssues_ForTodosWithoutExternalIssueNumber()
    {
        var dbName = Guid.NewGuid().ToString();
        var handler = new FakeHttpMessageHandler();
        // Enqueue a response for 1 todo
        handler.EnqueueJson("""{"number": 42, "title": "Todo 1", "state": "open"}""");

        var (svc, projectId) = await SetupProjectWithTodosAsync(dbName, handler, todoCount: 1);

        var result = await svc.SyncToGitHubAsync(projectId);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Failed);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("/issues", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task SyncToGitHub_CreatesIssues_ForFeatureRequestsWithoutExternalIssueNumber()
    {
        var dbName = Guid.NewGuid().ToString();
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson("""{"number": 7, "title": "Feature 1", "state": "open"}""");

        var (svc, projectId) = await SetupProjectWithTodosAsync(dbName, handler, todoCount: 0, featureCount: 1);

        var result = await svc.SyncToGitHubAsync(projectId);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Failed);
    }

    // -------------------------------------------------------------------------
    // SyncToGitHubAsync — updates issues for items with ExternalIssueNumber
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToGitHub_UpdatesIssues_ForTodosWithExistingExternalIssueNumber()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.Create(dbName);
        using var db = await factory.CreateDbContextAsync();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            IntegrationToken = FakeToken,
            IntegrationRepoUrl = FakeRepoUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Projects.Add(project);

        db.Todos.Add(new Todo
        {
            Id = Guid.NewGuid(),
            Title = "Existing Todo",
            ProjectId = project.Id,
            ExternalIssueNumber = 99,
            ExternalSystem = "github",
            Status = TodoStatus.InProgress,
            Priority = TodoPriority.High
        });
        await db.SaveChangesAsync();

        var handler = new FakeHttpMessageHandler();
        // PATCH response for update
        handler.EnqueueJson("""{"number": 99, "title": "Existing Todo", "state": "open"}""");

        var httpFactory = new FakeHttpClientFactory(handler);
        var config = Substitute.For<IConfiguration>();
        var svc = new GitHubSyncService(factory, httpFactory, config);

        var result = await svc.SyncToGitHubAsync(project.Id);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Failed);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
    }

    // -------------------------------------------------------------------------
    // SyncToGitHubAsync — stores returned issue numbers
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToGitHub_StoresReturnedIssueNumber_OnTodo()
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
                IntegrationRepoUrl = FakeRepoUrl,
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
            handler.EnqueueJson("""{"number": 55, "title": "Store Me", "state": "open"}""");

            var httpFactory = new FakeHttpClientFactory(handler);
            var config = Substitute.For<IConfiguration>();
            var svc = new GitHubSyncService(factory, httpFactory, config);

            await svc.SyncToGitHubAsync(project.Id);

            // Re-read from DB to verify persistence
            using var verifyDb = await factory.CreateDbContextAsync();
            var todo = await verifyDb.Todos.FirstAsync(t => t.Title == "Store Me");
            Assert.Equal(55, todo.ExternalIssueNumber);
            Assert.Equal("github", todo.ExternalSystem);
        }
    }

    // -------------------------------------------------------------------------
    // SyncToGitHubAsync — returns failure when project not found
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToGitHub_ReturnsError_WhenProjectNotFound()
    {
        var svc = BuildService(out _);
        var result = await svc.SyncToGitHubAsync(Guid.NewGuid());

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Failed);
        Assert.Contains("Project not found", result.Errors[0]);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync — closed → status = done
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleWebhook_Closed_SetsTodoStatusToDone()
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
                ExternalSystem = "github",
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
        var svc = new GitHubSyncService(factory, httpFactory, config);

        var payload = new GitHubWebhookPayload
        {
            Action = "closed",
            Issue = new GitHubIssue { Number = 10, Title = "Close Me", State = "closed" }
        };

        var handled = await svc.HandleWebhookAsync(projectId, payload);

        Assert.True(handled);

        using var verify = await factory.CreateDbContextAsync();
        var updatedTodo = await verify.Todos.FindAsync(todoId);
        Assert.Equal(TodoStatus.Done, updatedTodo!.Status);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync — reopened → status restored
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleWebhook_Reopened_SetsTodoStatusToPending()
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
                Title = "Reopen Me",
                ProjectId = projectId,
                ExternalIssueNumber = 20,
                ExternalSystem = "github",
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
        var svc = new GitHubSyncService(factory, httpFactory, config);

        var payload = new GitHubWebhookPayload
        {
            Action = "reopened",
            Issue = new GitHubIssue { Number = 20, Title = "Reopen Me", State = "open" }
        };

        var handled = await svc.HandleWebhookAsync(projectId, payload);

        Assert.True(handled);

        using var verify = await factory.CreateDbContextAsync();
        var updatedTodo = await verify.Todos.FindAsync(todoId);
        Assert.Equal(TodoStatus.Pending, updatedTodo!.Status);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync — edited → title/description updated
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleWebhook_Edited_UpdatesTodoTitleAndDescription()
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
                Title = "Old Title",
                Description = "Old description",
                ProjectId = projectId,
                ExternalIssueNumber = 30,
                ExternalSystem = "github",
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
        var svc = new GitHubSyncService(factory, httpFactory, config);

        var payload = new GitHubWebhookPayload
        {
            Action = "edited",
            Issue = new GitHubIssue { Number = 30, Title = "New Title", Body = "New description", State = "open" }
        };

        var handled = await svc.HandleWebhookAsync(projectId, payload);

        Assert.True(handled);

        using var verify = await factory.CreateDbContextAsync();
        var updatedTodo = await verify.Todos.FindAsync(todoId);
        Assert.Equal("New Title", updatedTodo!.Title);
        Assert.Equal("New description", updatedTodo.Description);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync — unmatched issue number → returns false
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleWebhook_UnmatchedIssueNumber_ReturnsFalse()
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
        var svc = new GitHubSyncService(factory, httpFactory, config);

        var payload = new GitHubWebhookPayload
        {
            Action = "closed",
            Issue = new GitHubIssue { Number = 9999, Title = "No match", State = "closed" }
        };

        var handled = await svc.HandleWebhookAsync(project.Id, payload);
        Assert.False(handled);
    }

    // -------------------------------------------------------------------------
    // HandleWebhookAsync — feature request closed → status = done
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleWebhook_Closed_SetsFeatureRequestStatusToDone()
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
                Title = "Feature Close",
                ProjectId = projectId,
                ExternalIssueNumber = 50,
                ExternalSystem = "github",
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
        var svc = new GitHubSyncService(factory, httpFactory, config);

        var payload = new GitHubWebhookPayload
        {
            Action = "closed",
            Issue = new GitHubIssue { Number = 50, Title = "Feature Close", State = "closed" }
        };

        var handled = await svc.HandleWebhookAsync(projectId, payload);

        Assert.True(handled);

        using var verify = await factory.CreateDbContextAsync();
        var updated = await verify.FeatureRequests.FindAsync(frId);
        Assert.Equal(FeatureRequestStatus.Done, updated!.Status);
    }
}

