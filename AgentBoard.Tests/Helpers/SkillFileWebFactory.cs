using AgentBoard.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBoard.Tests.Helpers;

/// <summary>
/// Variant of <see cref="AgentBoardWebFactory"/> that additionally configures a writable
/// temp directory as the web root, enabling skill-file upload/download integration tests.
/// </summary>
public sealed class SkillFileWebFactory : WebApplicationFactory<Program>, IDisposable
{
    /// <summary>Unique temp directory used as the web root for this test run.</summary>
    public string TempWebRoot { get; } = Path.Combine(
        Path.GetTempPath(), "AgentBoardTests_WebRoot_" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Replace EF Core with InMemory (same as AgentBoardWebFactory).
        builder.ConfigureServices(services =>
        {
            var descriptors = services
                .Where(d => d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true
                         || d.ServiceType == typeof(IDbContextFactory<ApplicationDbContext>)
                         || d.ServiceType == typeof(ApplicationDbContext))
                .ToList();
            foreach (var d in descriptors) services.Remove(d);

            services.AddDbContextFactory<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("SkillFileIntegration-" + Guid.NewGuid()));
        });

        // Point the web root at our temp directory so uploaded files land somewhere writable.
        Directory.CreateDirectory(TempWebRoot);
        builder.UseWebRoot(TempWebRoot);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(TempWebRoot))
        {
            try { Directory.Delete(TempWebRoot, recursive: true); } catch { /* best-effort */ }
        }
    }
}
