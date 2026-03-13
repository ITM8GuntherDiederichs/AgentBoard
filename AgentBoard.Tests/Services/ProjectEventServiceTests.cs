using AgentBoard.Data.Models;
using AgentBoard.Hubs;
using AgentBoard.Services;
using AgentBoard.Tests.Helpers;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace AgentBoard.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ProjectEventService"/> using an in-memory database
/// and a substituted <see cref="IHubContext{AgentBoardHub}"/>.
/// </summary>
public class ProjectEventServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (ProjectEventService svc, IClientProxy clientProxy)
        BuildService(string? dbName = null)
    {
        var factory = TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString());

        var clientProxy = Substitute.For<IClientProxy>();
        var clients = Substitute.For<IHubClients>();
        clients.Group(Arg.Any<string>()).Returns(clientProxy);

        var hub = Substitute.For<IHubContext<AgentBoardHub>>();
        hub.Clients.Returns(clients);

        return (new ProjectEventService(factory, hub), clientProxy);
    }

    // -------------------------------------------------------------------------
    // PostEventAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PostEventAsync_ReturnsEvent_WithNewId()
    {
        var (svc, _) = BuildService();

        var ev = await svc.PostEventAsync(Guid.NewGuid(), "agent-1", ProjectEventType.Progress, "Started");

        Assert.NotEqual(Guid.Empty, ev.Id);
    }

    [Fact]
    public async Task PostEventAsync_PersistsEvent_InDatabase()
    {
        var dbName = Guid.NewGuid().ToString();
        var (svc, _) = BuildService(dbName);
        var projectId = Guid.NewGuid();

        await svc.PostEventAsync(projectId, "agent-1", ProjectEventType.Progress, "Hello");

        var events = await svc.GetEventsAsync(projectId);
        Assert.Single(events);
        Assert.Equal("Hello", events[0].Message);
    }

    [Fact]
    public async Task PostEventAsync_SetsProjectId()
    {
        var (svc, _) = BuildService();
        var projectId = Guid.NewGuid();

        var ev = await svc.PostEventAsync(projectId, null, ProjectEventType.Note, "Note");

        Assert.Equal(projectId, ev.ProjectId);
    }

    [Fact]
    public async Task PostEventAsync_SetsAgentName()
    {
        var (svc, _) = BuildService();

        var ev = await svc.PostEventAsync(Guid.NewGuid(), "my-agent", ProjectEventType.Progress, "msg");

        Assert.Equal("my-agent", ev.AgentName);
    }

    [Fact]
    public async Task PostEventAsync_SetsEventType()
    {
        var (svc, _) = BuildService();

        var ev = await svc.PostEventAsync(Guid.NewGuid(), null, ProjectEventType.Error, "Boom");

        Assert.Equal(ProjectEventType.Error, ev.EventType);
    }

    [Fact]
    public async Task PostEventAsync_SetsCreatedAt_ToApproximatelyNow()
    {
        var (svc, _) = BuildService();
        var before = DateTime.UtcNow.AddSeconds(-1);

        var ev = await svc.PostEventAsync(Guid.NewGuid(), null, ProjectEventType.Progress, "msg");

        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.InRange(ev.CreatedAt, before, after);
    }

    [Fact]
    public async Task PostEventAsync_BroadcastsProjectEventReceived_ToProjectGroup()
    {
        var (svc, clientProxy) = BuildService();
        var projectId = Guid.NewGuid();

        await svc.PostEventAsync(projectId, "a", ProjectEventType.Completed, "Done");

        await clientProxy.Received(1).SendCoreAsync(
            "ProjectEventReceived",
            Arg.Any<object[]>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // GetEventsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetEventsAsync_ReturnsEventsOrderedByDescendingCreatedAt()
    {
        var dbName = Guid.NewGuid().ToString();
        var (svc, _) = BuildService(dbName);
        var projectId = Guid.NewGuid();

        await svc.PostEventAsync(projectId, null, ProjectEventType.Progress, "first");
        await Task.Delay(10);
        await svc.PostEventAsync(projectId, null, ProjectEventType.Progress, "second");
        await Task.Delay(10);
        await svc.PostEventAsync(projectId, null, ProjectEventType.Progress, "third");

        var events = await svc.GetEventsAsync(projectId);

        Assert.Equal(3, events.Count);
        Assert.Equal("third", events[0].Message);
        Assert.Equal("second", events[1].Message);
        Assert.Equal("first", events[2].Message);
    }

    [Fact]
    public async Task GetEventsAsync_RespectsLimit()
    {
        var dbName = Guid.NewGuid().ToString();
        var (svc, _) = BuildService(dbName);
        var projectId = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
            await svc.PostEventAsync(projectId, null, ProjectEventType.Progress, $"event-{i}");

        var events = await svc.GetEventsAsync(projectId, limit: 3);

        Assert.Equal(3, events.Count);
    }

    [Fact]
    public async Task GetEventsAsync_ReturnsEmptyList_WhenNoEvents()
    {
        var (svc, _) = BuildService();

        var events = await svc.GetEventsAsync(Guid.NewGuid());

        Assert.Empty(events);
    }

    [Fact]
    public async Task GetEventsAsync_ReturnsOnlyEventsForSpecifiedProject()
    {
        var dbName = Guid.NewGuid().ToString();
        var (svc, _) = BuildService(dbName);
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();

        await svc.PostEventAsync(projectId, null, ProjectEventType.Progress, "mine");
        await svc.PostEventAsync(otherProjectId, null, ProjectEventType.Progress, "not mine");

        var events = await svc.GetEventsAsync(projectId);

        Assert.Single(events);
        Assert.Equal("mine", events[0].Message);
    }
}
