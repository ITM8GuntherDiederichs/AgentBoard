using System.Net;
using AgentBoard.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentBoard.Tests.Helpers;

/// <summary>
/// Web factory variant that replaces the EF SQL Server provider with InMemory
/// and injects a controllable <see cref="MockHttpMessageHandler"/> so that
/// integration-token validation calls can be made to return success or failure
/// without hitting real external APIs.
/// </summary>
public sealed class IntegrationWebFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Controls how the mock HTTP handler behaves during a test.
    /// Set <c>ReturnSuccessForToken</c> to the token string that should be treated as valid.
    /// Any other token returns 401 Unauthorized.
    /// Defaults to returning 200 for every token (no filtering).
    /// </summary>
    public MockHttpMessageHandler MockHandler { get; } = new MockHttpMessageHandler();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // ── Replace EF Core with InMemory ────────────────────────────────
            var descriptors = services
                .Where(d => d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true
                         || d.ServiceType == typeof(IDbContextFactory<ApplicationDbContext>)
                         || d.ServiceType == typeof(ApplicationDbContext))
                .ToList();
            foreach (var d in descriptors) services.Remove(d);

            services.AddDbContextFactory<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationAuth-" + Guid.NewGuid()));

            // ── Wire mock handler to the named "IntegrationValidator" client ──
            services.AddHttpClient("IntegrationValidator")
                .ConfigurePrimaryHttpMessageHandler(() => MockHandler);
        });
    }
}

/// <summary>
/// Configurable HTTP message handler for integration-token validation tests.
/// Returns 200 OK when the Authorization header contains <see cref="ValidTokenValue"/>;
/// returns 401 Unauthorized for every other token value.
/// When <see cref="ValidTokenValue"/> is <c>null</c> (default) every request succeeds.
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    /// <summary>
    /// The token value (without scheme prefix) that will receive a 200 response.
    /// All other values receive 401. If <c>null</c>, all requests succeed.
    /// </summary>
    public string? ValidTokenValue { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpStatusCode statusCode;

        if (ValidTokenValue is null)
        {
            statusCode = HttpStatusCode.OK;
        }
        else
        {
            // Check Authorization header; strip scheme prefix (token / Bearer / Basic …)
            var authHeader = request.Headers.Authorization?.ToString() ?? string.Empty;
            statusCode = authHeader.Contains(ValidTokenValue)
                ? HttpStatusCode.OK
                : HttpStatusCode.Unauthorized;
        }

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("{}")
        };

        return Task.FromResult(response);
    }
}
