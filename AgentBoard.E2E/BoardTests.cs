using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace AgentBoard.E2E;

/// <summary>
/// Playwright E2E tests for the AgentBoard Blazor dashboard.
/// Tests run against the live app at http://localhost:5227.
/// Each test uses a unique title (Guid suffix) to avoid cross-test pollution.
/// </summary>
[Collection("Board E2E")]
public class BoardTests : PageTest
{
    private const string AppUrl = "http://localhost:5227";

    // Single HttpClient shared for API seeding — thread-safe for concurrent GET/POST
    private static readonly HttpClient Http = new();

    public BoardTests(PlaywrightFixture fixture) : base(fixture) { }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: Board page loads
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the board page renders its heading and the New Todo action button.
    /// </summary>
    [Fact]
    public async Task BoardPage_Loads_ShowsHeadingAndNewTodoButton()
    {
        await GotoPageAsync(AppUrl);

        // Title contains "AgentBoard"
        await Expect(Page).ToHaveTitleAsync(new Regex("AgentBoard", RegexOptions.IgnoreCase));

        // Board heading and primary action visible
        await Expect(Page.Locator("h4").Filter(new() { HasText = "Board" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "New Todo" })).ToBeVisibleAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: Create a todo via the UI
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the create dialog, fills in the title, submits, and asserts the new
    /// todo row appears in the MudDataGrid.
    /// </summary>
    [Fact]
    public async Task CreateTodo_ViaUI_AppearsInBoardGrid()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var uniqueTitle = $"E2E Create {suffix}";

        await GotoPageAsync(AppUrl);

        // Open "New Todo" dialog — button is wired up after Blazor connects
        await Page.GetByRole(AriaRole.Button, new() { Name = "New Todo" }).ClickAsync();

        // Wait for the MudDialog portal to appear
        var dialog = Page.Locator(".mud-dialog-container");
        await dialog.WaitForAsync(new() { Timeout = 10_000 });

        // Fill in the Title field — first text input inside the dialog
        await dialog.Locator("input[type='text']").First.FillAsync(uniqueTitle);

        // Submit
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        // Wait for dialog to close and Blazor to re-render the grid
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The new todo row must be visible in the data grid
        await Expect(
            Page.Locator("tr.mud-table-row").Filter(new() { HasText = uniqueTitle })
        ).ToBeVisibleAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3: Default status and priority badges
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// After creating a todo (without selecting status/priority), verifies the
    /// row shows "Pending" in the StatusBadge and "Medium" in the PriorityBadge.
    /// </summary>
    [Fact]
    public async Task CreateTodo_ViaUI_ShowsDefaultPendingStatusAndMediumPriority()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var uniqueTitle = $"E2E Badges {suffix}";

        await GotoPageAsync(AppUrl);

        // Create via the UI (same flow as Test 2)
        await Page.GetByRole(AriaRole.Button, new() { Name = "New Todo" }).ClickAsync();
        var dialog = Page.Locator(".mud-dialog-container");
        await dialog.WaitForAsync(new() { Timeout = 10_000 });
        await dialog.Locator("input[type='text']").First.FillAsync(uniqueTitle);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Find the row
        var row = Page.Locator("tr.mud-table-row").Filter(new() { HasText = uniqueTitle });
        await row.WaitForAsync(new() { Timeout = 10_000 });

        // Status chip must show "Pending"
        await Expect(row.GetByText("Pending")).ToBeVisibleAsync();

        // Priority chip must show "Medium"
        await Expect(row.GetByText("Medium")).ToBeVisibleAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: Navigate to the todo detail page and back
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clicks the pencil (edit) icon on a todo row, verifies the URL changes to
    /// /todo/{guid}, confirms the Title field is populated, then navigates back.
    /// </summary>
    [Fact]
    public async Task ClickEditIcon_NavigatesToDetailPage_AndBackReturnsToBoard()
    {
        // Seed a known todo via API so the test is independent of other tests
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var uniqueTitle = $"E2E Detail {suffix}";
        await CreateTodoViaApiAsync(uniqueTitle);

        await GotoPageAsync(AppUrl);

        // Find the row and click the edit anchor (<a href="/todo/{id}">)
        var row = Page.Locator("tr.mud-table-row").Filter(new() { HasText = uniqueTitle });
        await row.WaitForAsync(new() { Timeout = 10_000 });
        await row.Locator("a[href*='/todo/']").ClickAsync();

        // Wait for the detail page to finish loading (Blazor SPA nav → same circuit)
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // URL must contain /todo/{guid}
        Assert.Matches(@"/todo/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", Page.Url);

        // Title field must be visible and populated with the correct value
        // MudTextField "Title" — wait for the detail form to appear
        var titleField = Page.Locator("input[type='text']").First;
        await Expect(titleField).ToBeVisibleAsync();
        var titleValue = await titleField.InputValueAsync();
        Assert.Equal(uniqueTitle, titleValue);

        // Navigate back via the Cancel button (calls Blazor NavigateTo("/"))
        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();

        // Wait for Blazor to complete the SPA navigation back to the board
        await Expect(Page.Locator("h4").Filter(new() { HasText = "Board" }))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5: Delete a todo via the UI
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a todo via the REST API, finds it on the board, clicks the delete
    /// icon, confirms in the dialog, and verifies it disappears from the grid.
    /// </summary>
    [Fact]
    public async Task DeleteTodo_ViaUI_RemovesRowFromGrid()
    {
        // Seed a known todo via the REST API
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var uniqueTitle = $"E2E Delete {suffix}";
        await CreateTodoViaApiAsync(uniqueTitle);

        await GotoPageAsync(AppUrl);

        // Find the row
        var row = Page.Locator("tr.mud-table-row").Filter(new() { HasText = uniqueTitle });
        await row.WaitForAsync(new() { Timeout = 10_000 });

        // Click the delete button — the only <button> in the row for an unclaimed todo
        // (the edit icon renders as <a href="...">, the delete icon renders as <button>)
        await row.Locator("button").ClickAsync();

        // Wait for the MudDialog confirmation portal
        var confirmDialog = Page.Locator(".mud-dialog-container");
        await confirmDialog.WaitForAsync(new() { Timeout = 10_000 });

        // Click the error-styled "Delete" confirmation button
        await confirmDialog.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();

        // Wait for Blazor to refresh the grid
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The row must no longer be present
        await Expect(
            Page.Locator("tr.mud-table-row").Filter(new() { HasText = uniqueTitle })
        ).ToHaveCountAsync(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a todo directly through the REST API — faster and more reliable
    /// than going through the UI for tests that just need pre-existing data.
    /// Priority 1 = Medium (the default; matches what the UI creates by default).
    /// </summary>
    private static async Task CreateTodoViaApiAsync(string title)
    {
        var response = await Http.PostAsJsonAsync(
            $"{AppUrl}/api/todos",
            new { title, priority = 1 });
        response.EnsureSuccessStatusCode();
    }
}
