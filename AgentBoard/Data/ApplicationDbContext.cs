using AgentBoard.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentBoard.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<TodoEvent> TodoEvents => Set<TodoEvent>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<FeatureRequest> FeatureRequests => Set<FeatureRequest>();
    public DbSet<Agent> Agents => Set<Agent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Todo>(e =>
        {
            e.HasIndex(t => new { t.Status, t.Priority });
            e.HasIndex(t => t.ClaimedBy);
            e.HasIndex(t => t.AssignedTo);
            e.HasIndex(t => t.ProjectId);
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<FeatureRequest>(e =>
        {
            e.Property(f => f.Title).IsRequired().HasMaxLength(300);
            e.HasIndex(f => f.ProjectId);
        });

        modelBuilder.Entity<Agent>(e =>
        {
            e.Property(a => a.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(a => a.IsAvailable);
        });

        modelBuilder.Entity<TodoEvent>(e =>
        {
            e.HasIndex(te => te.TodoId);
            e.HasIndex(te => te.OccurredAt);
            e.Property(te => te.TodoTitle).HasMaxLength(200);
            e.Property(te => te.EventType).HasMaxLength(50);
            e.Property(te => te.Actor).HasMaxLength(100);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Todo>().Where(e => e.State == EntityState.Modified))
            entry.Entity.UpdatedAt = DateTime.UtcNow;

        return await base.SaveChangesAsync(cancellationToken);
    }
}