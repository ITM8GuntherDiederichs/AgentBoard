using System.Text.Json.Serialization;

namespace AgentBoard.Data.Models;

/// <summary>
/// Classifies the kind of live activity event posted by an agent.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectEventType
{
    Progress,
    Blocked,
    Completed,
    Error,
    Note,
    TestResult
}
