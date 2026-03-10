# AgentBoard — Agent Brief

Shared task-coordination board for AI agents. Agents discover, claim, work, and complete
todos via a REST API. Human operators observe and manage the board through a Blazor Server dashboard.

---

## Stack

.NET 10 · Blazor Server · MudBlazor 9.x · ASP.NET Core Minimal API · EF Core 10.x ·
SQL Server (LocalDB dev / Azure SQL prod) · Azure App Service · GitHub Actions

---

## Key Facts

- **No auth for MVP** — all UI and API endpoints are unauthenticated
- **Single entity:** `Todo` — no foreign keys in v1
- **Core mechanic:** Agents call `POST /api/todos/{id}/claim` to take exclusive ownership;
  `DELETE /api/todos/{id}/claim` to release. 409 Conflict if already claimed by another agent.
- **Todo fields:** `Id` · `Title` · `Description` · `Status` · `Priority` · `AssignedTo` ·
  `ClaimedBy` · `ClaimedAt` · `CreatedAt` · `UpdatedAt`
- **Status enum:** `Pending` | `InProgress` | `Blocked` | `Done`
- **Priority enum:** `Low` | `Medium` | `High` | `Critical`
- **Dashboard auto-refresh:** 5-second polling (SignalR is v2)
- **API style:** Minimal API in `/Api/TodoEndpoints.cs` — NOT MVC controllers

---

## MVP Scope

1. `GET/POST/PUT/PATCH/DELETE /api/todos` — full CRUD for AI agents
2. `POST /api/todos/{id}/claim` — atomic claim with 409 on conflict
3. `DELETE /api/todos/{id}/claim` — release claim
4. Blazor board page (`/`) — filterable, sortable list with live refresh
5. Todo detail/edit page (`/todo/{id}`)
6. Human create/edit/delete/force-release via dashboard

---

## Project Structure

```
AgentBoard/                   ← solution root
├── AgentBoard/               ← main web project (Blazor Server + Minimal API)
│   ├── Api/                  ← TodoEndpoints.cs
│   ├── Components/Pages/     ← Board.razor, TodoDetail.razor
│   ├── Components/Shared/    ← TodoTable, TodoForm, StatusBadge, ClaimIndicator…
│   ├── Contracts/            ← Request/response DTOs
│   ├── Data/                 ← ApplicationDbContext + Models/
│   ├── Migrations/           ← EF Core migrations
│   └── Services/             ← TodoService.cs
├── AgentBoard.Tests/         ← xUnit unit + WebApplicationFactory integration tests
├── AgentBoard.E2E/           ← Playwright E2E tests
└── .github/workflows/        ← ci.yml, deploy.yml
```

---

## Agent Responsibilities

| Agent | Tasks |
|-------|-------|
| **backend** | `TodoService`, `ApplicationDbContext`, `TodoEndpoints`, DTOs, EF migrations, claim atomicity |
| **frontend** | All Razor components, MudBlazor layout, filter/sort, auto-refresh, claim UI |
| **dba** | Review `Todo` schema, indexes `(Status,Priority)` and `(ClaimedBy)`, optimistic concurrency token |
| **qa** | Unit tests for `TodoService`, integration tests for all 8 API endpoints, Playwright board smoke tests |
| **devops** | `.slnx` solution setup, `ci.yml` (build+test), `deploy.yml` (Azure App Service) |
| **azure** | Azure App Service plan, Azure SQL database, Key Vault for connection string |
| **security** | Input validation review, no-auth risk acceptance, future API-key design recommendation |
| **architect** | Validate Minimal API + Blazor co-hosting, claim atomicity approach (optimistic concurrency vs UPDLOCK) |

---

## API Quick Reference

```
GET    /api/todos                    List (filter: ?status=&priority=&assignedTo=&claimedBy=)
GET    /api/todos/{id}               Get one
POST   /api/todos                    Create
PUT    /api/todos/{id}               Full update
PATCH  /api/todos/{id}               Partial update (status, priority)
DELETE /api/todos/{id}               Hard delete
POST   /api/todos/{id}/claim         Claim — body: {"agentId":"x"} — 409 if taken
DELETE /api/todos/{id}/claim         Release claim
```

---

## Out of Scope (v1)

Auth · API keys · claim expiry · due dates · tags · subtasks · audit log ·
Kanban view · bulk ops · notifications · soft delete · pagination · SignalR

---

## Full requirements: requirements.md
