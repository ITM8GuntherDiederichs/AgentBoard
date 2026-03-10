using AgentBoard.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Services;

/// <summary>
/// Background service that automatically releases expired todo claims every minute.
/// </summary>
public class ClaimExpiryService(IDbContextFactory<ApplicationDbContext> factory, ILogger<ClaimExpiryService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            await ReleaseExpiredClaimsAsync();
        }
    }

    private async Task ReleaseExpiredClaimsAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        var expired = await db.Todos
            .Where(t => t.ClaimExpiresAt.HasValue && t.ClaimExpiresAt.Value < DateTime.UtcNow)
            .ToListAsync();

        foreach (var todo in expired)
        {
            logger.LogInformation("Auto-releasing expired claim on todo {Id} held by {Agent}", todo.Id, todo.ClaimedBy);
            todo.ClaimedBy = null;
            todo.ClaimedAt = null;
            todo.ClaimExpiresAt = null;
        }

        if (expired.Count > 0)
            await db.SaveChangesAsync();
    }
}
