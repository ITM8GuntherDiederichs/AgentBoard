using Microsoft.Playwright;
using Xunit;

namespace AgentBoard.E2E;

/// <summary>
/// Shared Playwright browser instance for all board tests.
/// xUnit calls InitializeAsync/DisposeAsync around the full test class.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
    }
}

/// <summary>
/// Base class for all Playwright page tests.
/// Each test gets a fresh browser context and page to ensure isolation.
/// </summary>
public abstract class PageTest : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    private readonly PlaywrightFixture _fixture;

    protected PageTest(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        Context = await _fixture.Browser.NewContextAsync();
        Page = await Context.NewPageAsync();

        // Inject a flag that is set when Blazor Server's SignalR circuit connects.
        // This script runs before any page script on every full navigation, so we
        // reliably catch 'blazor:connected' even when Blazor boots quickly.
        await Page.AddInitScriptAsync(@"
            window.__blazorReady = false;
            window.addEventListener('blazor:connected', function() {
                window.__blazorReady = true;
            });
        ");
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
    }

    /// <summary>
    /// Navigates to a URL and waits for Blazor's interactive SignalR circuit to be
    /// established before returning. Use this instead of a bare GotoAsync so that
    /// button clicks and event callbacks are wired up before any test interaction.
    /// </summary>
    protected async Task GotoPageAsync(string url)
    {
        await Page.GotoAsync(url);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        try
        {
            // Wait for the 'blazor:connected' event that fires once the SignalR hub
            // handshake completes and the component tree is fully interactive.
            await Page.WaitForFunctionAsync(
                "() => window.__blazorReady === true",
                null,
                new PageWaitForFunctionOptions { Timeout = 15_000 });
        }
        catch
        {
            // Fallback: 'blazor:connected' may not be available in all build configs.
            // A short fixed delay is enough on localhost.
            await Task.Delay(2_000);
        }
    }
}
