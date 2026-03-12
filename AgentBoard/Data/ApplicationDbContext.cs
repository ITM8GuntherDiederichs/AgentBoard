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
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<ProjectAgent> ProjectAgents => Set<ProjectAgent>();
    public DbSet<ProjectTeam> ProjectTeams => Set<ProjectTeam>();

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

        modelBuilder.Entity<Team>(e =>
        {
            e.Property(t => t.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<TeamMember>(e =>
        {
            e.HasKey(tm => new { tm.TeamId, tm.AgentId });
            e.HasIndex(tm => tm.AgentId);
            // No FK constraint to Agents table - AgentId is a plain Guid reference
            e.HasOne(tm => tm.Team)
                .WithMany(t => t.Members)
                .HasForeignKey(tm => tm.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectAgent>(e =>
        {
            e.HasKey(pa => new { pa.ProjectId, pa.AgentId });
            e.HasIndex(pa => pa.AgentId);
            // No FK constraint to Agents table - loose coupling pattern
        });

        modelBuilder.Entity<ProjectTeam>(e =>
        {
            e.HasKey(pt => new { pt.ProjectId, pt.TeamId });
            e.HasIndex(pt => pt.TeamId);
            // No FK constraint to Teams table - loose coupling pattern
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Todo>().Where(e => e.State == EntityState.Modified))
            entry.Entity.UpdatedAt = DateTime.UtcNow;

        return await base.SaveChangesAsync(cancellationToken);
    }
}
