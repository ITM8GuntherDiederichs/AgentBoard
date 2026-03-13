using AgentBoard.Data;
using AgentBoard.Data.Models;
using AgentBoard.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>
/// Manages live project activity events and broadcasts them over SignalR.
/// </summary>
public class ProjectEventService(
    IDbContextFactory<ApplicationDbContext> factory,
    IHubContext<AgentBoardHub> hub)
{
    /// <summary>
    /// Persists a new event and broadcasts it to all SignalR clients subscribed to the project group.
    /// </summary>
    /// <param name="projectId">The project the event belongs to.</param>
    /// <param name="agentName">Optional name of the agent raising the event.</param>
    /// <param name="eventType">Classification of the event.</param>
    /// <param name="message">Human-readable message.</param>
    /// <param name="metadata">Optional JSON metadata blob.</param>
    /// <returns>The persisted <see cref="ProjectEvent"/>.</returns>
    public async Task<ProjectEvent> PostEventAsync(
        Guid projectId,
        string? agentName,
        ProjectEventType eventType,
        string message,
        string? metadata = null)
    {
        using var db = await factory.CreateDbContextAsync();

        var ev = new ProjectEvent
        {
            ProjectId = projectId,
            AgentName = agentName,
            EventType = eventType,
            Message = message,
            Metadata = metadata
        };

        db.ProjectEvents.Add(ev);
        await db.SaveChangesAsync();

        // Broadcast to SignalR clients in the project group
        await hub.Clients.Group(projectId.ToString()).SendAsync("ProjectEventReceived", ev);

        return ev;
    }

    /// <summary>
    /// Returns the most recent events for a project, newest first.
    /// </summary>
    /// <param name="projectId">The project to query.</param>
    /// <param name="limit">Maximum number of events to return (capped by caller).</param>
    public async Task<List<ProjectEvent>> GetEventsAsync(Guid projectId, int limit = 100)
    {
        using var db = await factory.CreateDbContextAsync();

        return await db.ProjectEvents
            .Where(e => e.ProjectId == projectId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }
}
