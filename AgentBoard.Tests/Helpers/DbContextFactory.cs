using AgentBoard.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Tests.Helpers;

public static class TestDbFactory
{
    public static IDbContextFactory<ApplicationDbContext> Create(string dbName = "")
    {
        if (string.IsNullOrEmpty(dbName)) dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TestDbContextFactory(options);
    }
}

internal class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
    : IDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext() => new(options);

    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken ct = default)
        => Task.FromResult(new ApplicationDbContext(options));
}
