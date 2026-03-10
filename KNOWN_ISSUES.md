# Known Issues & Fixes

Quick reference for recurring issues. Check here first before debugging from scratch.

---

## 🔴 MudBlazor icons render super large / CSS not loading

**Symptoms**
- MudBlazor icons (Edit, Delete, Add etc.) appear as huge ~300px SVGs
- Page layout broken — content hidden behind AppBar
- MudBlazor colours/theme not applied (everything looks unstyled)

**Root Causes**

### 1. `_content/MudBlazor/` files returning 500
In .NET 10 with `MapStaticAssets()`, Razor Class Library `_content/` paths are **not** served by the static assets middleware. Without `UseStaticFiles()` running first, the Blazor router intercepts the request, can't match a page, and the 500 middleware returns an error page instead of the CSS/font files. MudBlazor's icon SVGs then have no size constraints and render at their natural ~300px default.

**Fix — `Program.cs`:** Add `UseStaticFiles()` **before** `MapStaticAssets()`:
```csharp
app.UseStaticFiles();       // ← must come first
app.MapStaticAssets();
```

### 2. Wrong CSS load order in `App.razor`
If `app.css` loads before `MudBlazor.min.css`, any custom rules that conflict with MudBlazor lose the specificity battle and need `!important` everywhere. More importantly, wrong ordering can cause theme variables to be missing on first paint.

**Correct order in `App.razor`:**
```html
<link rel="stylesheet" href="@Assets["lib/bootstrap/dist/css/bootstrap.min.css"]" />
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<link rel="stylesheet" href="@Assets["AgentBoard.styles.css"]" />
<link rel="stylesheet" href="@Assets["app.css"]" />   <!-- custom overrides last -->
```

### 3. Adding a separate Material Icons Google Font link
MudBlazor **bundles its own icon font** inside `MudBlazor.min.css`. Adding an extra Google Fonts `Material+Icons` link (especially via the wrong `css2?family=` endpoint) conflicts with the bundled font and breaks icon rendering.

**Do not add this:**
```html
<!-- ❌ WRONG — do not add -->
<link href="https://fonts.googleapis.com/css2?family=Material+Icons" rel="stylesheet" />
```

### 4. `body::before` scanline overlay with high z-index
A `position: fixed; z-index: 9999` overlay covers MudBlazor popovers, dropdowns and dialogs, making them appear broken or unclickable.

**Fix — `app.css`:** Keep scanline z-index low:
```css
body::before {
    ...
    pointer-events: none;
    z-index: 1;   /* ← not 9999 */
}
```

### 5. `pa-4` on `MudMainContent` hides content behind AppBar
`Class="pa-4"` sets `padding: 16px` on all sides, overriding MudBlazor's built-in `padding-top: var(--mud-appbar-height)` (64px). Content renders behind the AppBar.

**Fix — `MainLayout.razor`:** Put padding on an inner wrapper, not on `MudMainContent`:
```html
<!-- ❌ Wrong -->
<MudMainContent Class="pa-4">@Body</MudMainContent>

<!-- ✅ Correct -->
<MudMainContent>
    <div class="pa-4">@Body</div>
</MudMainContent>
```

---

## 🟡 E2E / `AgentBoard.E2E` project not committed

**Symptoms**
- CI fails with "project not found" on `AgentBoard.E2E`
- `.gitignore` line 130 (`*.e2e`) accidentally matches the `AgentBoard.E2E/` directory

**Fix:**
```powershell
git add -f AgentBoard.E2E/AgentBoard.E2E.csproj AgentBoard.E2E/BoardTests.cs AgentBoard.E2E/PlaywrightFixture.cs
```
The negation lines in `.gitignore` (`!AgentBoard.E2E/`) do not fully work when a parent directory is ignored — force-add individual files.

---

## 🟡 Unit tests fail after adding `IHubContext` to `TodoService`

**Symptoms**
- CI build error: `CS7036 — no argument for required parameter 'hub' of TodoService`
- Happens when SignalR is added to `TodoService` constructor

**Fix — `TodoServiceTests.cs`:** Mock the hub with NSubstitute:
```csharp
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

private static TodoService BuildService(string? dbName = null)
    => new(TestDbFactory.Create(dbName ?? Guid.NewGuid().ToString()),
           Substitute.For<IHubContext<AgentBoardHub>>());
```

---

## 🟡 `UseStatusCodePagesWithReExecute` intercepts API 404s

**Symptoms**
- `GET /api/todos/{id}` for a missing todo returns HTTP 200 with an HTML page instead of 404 JSON

**Fix — `Program.cs`:** Scope the middleware to non-API routes only:
```csharp
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found")
);
```

---

## 🟡 Windows "Access is denied" running `AgentBoard.exe`

**Symptoms**
- `run.cmd` or direct `.exe` execution fails with `Access is denied`

**Fix — `run.cmd`:** Use `dotnet exec` instead of running the `.exe` directly:
```cmd
dotnet exec bin\Debug\net10.0\AgentBoard.dll --urls http://localhost:5227
```

---

## 🟡 Azure deploy workflow fails on every push

**Symptoms**
- `deploy.yml` errors with `No credentials found` on every push to `main`

**Fix:** Change trigger to `workflow_dispatch` only until Azure secrets are configured:
```yaml
on:
  workflow_dispatch: # manual only — re-enable push trigger after adding AZURE_WEBAPP_NAME and AZURE_WEBAPP_PUBLISH_PROFILE secrets
```
