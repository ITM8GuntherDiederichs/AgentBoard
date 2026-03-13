using AgentBoard.Data.Models;
using AgentBoard.Services;

namespace AgentBoard.Api;

/// <summary>
/// Minimal API endpoints for agent live-event publishing and retrieval.
/// </summary>
public static class ProjectEventEndpoints
{
    /// <summary>
    /// Registers <c>/api/projects/{id:guid}/events</c> routes on the application.
    /// </summary>
    public static void MapProjectEventEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/projects/{id:guid}/events").WithTags("events");

        // POST — agent pushes a live event
        group.MapPost("/", async (Guid id, PostEventRequest req, ProjectEventService svc) =>
        {
            var ev = await svc.PostEventAsync(id, req.AgentName, req.EventType, req.Message, req.Metadata);
            return Results.Created($"/api/projects/{id}/events/{ev.Id}", ev);
        });

        // GET — fetch recent events for a project
        group.MapGet("/", async (Guid id, ProjectEventService svc, int limit = 100) =>
        {
            var events = await svc.GetEventsAsync(id, Math.Min(limit, 500));
            return Results.Ok(events);
        });
    }
}

/// <summary>Request body for posting a new project event.</summary>
public record PostEventRequest(
    string? AgentName,
    ProjectEventType EventType,
    string Message,
    string? Metadata = null);
