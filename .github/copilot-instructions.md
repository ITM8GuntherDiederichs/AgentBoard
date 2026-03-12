# AgentBoard — Copilot Agent Instructions

## Build & Test (agents MUST follow this)

```powershell
cd C:\data\itm8\Copilot\AgentBoard

# Build
dotnet build AgentBoard.slnx --configuration Release

# Test
dotnet test AgentBoard.Tests/AgentBoard.Tests.csproj --configuration Release
```

**NEVER try to start the application locally.** Do not run `dotnet run`, `dotnet exec`, `run.cmd`, or `start.ps1` inside agent tasks. Build + test is sufficient proof before pushing. The GitHub CI pipeline is the authoritative verification.

> **Why:** Multiple agents share the same machine. Port 5227 and the Debug DLL are shared resources — trying to start the app causes port conflicts and DLL lock errors that waste time and block other agents.

---

## Running E2E / frontend tests (on-demand only)

E2E tests require the app to be running. Only run these when explicitly requested:

```powershell
# Step 1 — start app reliably (kills conflicting processes first)
.\start.ps1   # from repo root

# Step 2 — in a separate terminal, run E2E tests
cd AgentBoard.E2E
dotnet test --configuration Release
```

E2E test files must be force-added to git (`.gitignore` line 130 matches `*.e2e`):
```powershell
git add -f AgentBoard.E2E/
```

---

## Key conventions

- **Services**: always use `IDbContextFactory<ApplicationDbContext>` — never inject `DbContext` directly
- **Primary constructor syntax**: `public class MyService(IDbContextFactory<ApplicationDbContext> factory)`
- **EF migrations**: write manually — `dotnet ef migrations add` fails in this environment
- **MudBlazor 9**: never put `MudSelect`/`MudRadio` inside `@if` — hide with `display:none` CSS instead
- **MudBlazor generics**: always specify `T=` on `MudChip`, `MudSelect`, `MudSelectItem`, `MudRadio`
- **`@rendermode`**: never add to `<Routes>` in `App.razor`
- **UserManager**: never inject in interactive Blazor pages
- **Family/project isolation**: every data query must scope to the relevant ID
- **PR rule**: all changes go through PRs — no direct commits to `main`
- **Commit trailer**: always include `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`

---

## Port & paths

| Item | Value |
|------|-------|
| App URL | http://localhost:5227 |
| Run script | `.\start.ps1` (repo root) |
| Solution file | `AgentBoard.slnx` |
| Main project | `AgentBoard/AgentBoard.csproj` |
| Test project | `AgentBoard.Tests/AgentBoard.Tests.csproj` |
