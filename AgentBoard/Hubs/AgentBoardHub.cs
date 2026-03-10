using Microsoft.AspNetCore.SignalR;

namespace AgentBoard.Hubs;

/// <summary>
/// SignalR hub for real-time AgentBoard push notifications.
/// Clients connect here to receive <c>TodoUpdated</c> events pushed by the server.
/// </summary>
public class AgentBoardHub : Hub
{
    // Clients connect to receive push notifications.
    // Server pushes via IHubContext<AgentBoardHub>.
}
