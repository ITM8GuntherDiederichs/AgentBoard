namespace AgentBoard.Services;

/// <summary>Result of a GitHub sync operation.</summary>
/// <param name="Created">Number of issues created on GitHub.</param>
/// <param name="Updated">Number of issues updated on GitHub.</param>
/// <param name="Failed">Number of items that failed to sync.</param>
/// <param name="Errors">Error messages for failed items.</param>
public record SyncResult(int Created, int Updated, int Failed, string[] Errors);
