using System.ComponentModel.DataAnnotations;

namespace AgentBoard.Contracts;

public class ClaimRequest
{
    [Required, MaxLength(100)]
    public string AgentId { get; set; } = string.Empty;
}
