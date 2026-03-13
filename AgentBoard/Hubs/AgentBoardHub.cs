using Microsoft.AspNetCore.SignalR;

namespace AgentBoard.Hubs;

/// <summary>
/// SignalR hub for real-time AgentBoard push notifications.
/// Clients connect here to receive <c>TodoUpdated</c> and <c>ProjectEventReceived</c> events pushed by the server.
/// </summary>
public class AgentBoardHub : Hub
{
    // Clients connect to receive push notifications.
    // Server pushes via IHubContext<AgentBoardHub>.

    /// <summary>
    /// Adds the caller's connection to the named project group so they receive
    /// <c>ProjectEventReceived</c> broadcasts for that project.
    /// </summary>
    /// <param name="projectId">The project ID (string GUID).</param>
    public async Task JoinProject(string projectId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, projectId);

    /// <summary>
    /// Removes the caller's connection from the named project group.
    /// </summary>
    /// <param name="projectId">The project ID (string GUID).</param>
    public async Task LeaveProject(string projectId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, projectId);
}
