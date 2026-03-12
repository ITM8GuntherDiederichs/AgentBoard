namespace AgentBoard.Data.Models;

/// <summary>Identifies the external integration type for a team or project.</summary>
public enum IntegrationType
{
    /// <summary>No integration configured.</summary>
    None,

    /// <summary>GitHub integration.</summary>
    GitHub,

    /// <summary>Azure DevOps integration.</summary>
    AzureDevOps
}
