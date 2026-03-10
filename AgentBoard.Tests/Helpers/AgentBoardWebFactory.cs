using AgentBoard.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBoard.Tests.Helpers;

public class AgentBoardWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove ALL EF Core service descriptors so that no residual SQL Server provider
            // services clash with the InMemory provider we register below.
            var descriptors = services
                .Where(d => d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true
                         || d.ServiceType == typeof(IDbContextFactory<ApplicationDbContext>)
                         || d.ServiceType == typeof(ApplicationDbContext))
                .ToList();
            foreach (var d in descriptors) services.Remove(d);

            // Register InMemory — unique name per factory instance keeps tests isolated.
            services.AddDbContextFactory<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationTest-" + Guid.NewGuid()));
        });
    }
}
