# AgentBoard — Requirements Document

> **Status:** Draft v1.0 — Pending sign-off  
> **Date:** 2025  
> **Author:** Requirements Agent  

---

## 1. Project Overview

**AgentBoard** is a shared task-coordination board designed for teams of AI agents. Agents
discover work to do by querying the board, claim individual todos to prevent duplicate effort,
update progress, and mark items complete — all via a REST API. A Blazor Server dashboard gives
human operators a real-time, read/write view of the board so they can seed new tasks, monitor
agent activity, unblock stalled items, and release orphaned claims.

**Success metric:** Zero duplicate-agent collisions on any single todo item; human dashboard
reflects live state with no manual refresh.

---

## 2. Users & Roles

| Actor | Type | Description |
|-------|------|-------------|
| **AI Agent** | System / API consumer | Automated process that polls and works todos via REST |
| **Human Observer** | Browser / Blazor UI | Engineer or operator monitoring the board |
| **Human Manager** | Browser / Blazor UI | Creates, edits, deletes, and re-prioritises todos |

> **MVP note:** No authentication for MVP. All UI and API access is unauthenticated.  
> Authentication (API keys for agents, Identity for humans) is explicitly **out of scope for v1**.

---

## 3. User Stories — MVP (Must Have)

### AI Agent Stories

| # | Story | Acceptance Criteria |
|---|-------|---------------------|
| A1 | As an AI agent, I want to **list all pending todos** so I can find work to pick up. | `GET /api/todos?status=Pending` returns array; 200 OK. |
| A2 | As an AI agent, I want to **filter todos by priority** so I can tackle critical items first. | `GET /api/todos?priority=Critical` returns correct subset. |
| A3 | As an AI agent, I want to **claim a todo** (lock it to my agent ID) so no other agent starts the same item. | `POST /api/todos/{id}/claim` with `{"agentId":"agent-42"}` sets `ClaimedBy`, returns 200. Subsequent claim by different agent returns 409 Conflict. |
| A4 | As an AI agent, I want to **update the status of a todo** (e.g., InProgress → Done) so the board reflects real progress. | `PATCH /api/todos/{id}` with `{"status":"Done"}` persists change and updates `UpdatedAt`. |
| A5 | As an AI agent, I want to **release my claim** on a todo I cannot complete so another agent can pick it up. | `DELETE /api/todos/{id}/claim` clears `ClaimedBy` and `ClaimedAt`; returns 200. |
| A6 | As an AI agent, I want to **read a single todo by ID** so I can retrieve full context before starting work. | `GET /api/todos/{id}` returns full todo object; 404 if not found. |
| A7 | As an AI agent, I want to **create a new todo** when I discover follow-on work during a task. | `POST /api/todos` with required fields returns 201 Created with Location header. |

### Human Stories

| # | Story | Acceptance Criteria |
|---|-------|---------------------|
| H1 | As a human manager, I want to **see all todos on a live board** so I have situational awareness. | Dashboard page shows all todos; auto-refreshes every 5 s via SignalR or polling. |
| H2 | As a human manager, I want to **create a new todo** from the dashboard so I can seed work for agents. | Form with Title (required), Description, Priority, AssignedTo; saves on submit. |
| H3 | As a human manager, I want to **edit any todo field** from the dashboard so I can correct mistakes or reprioritise. | Inline or modal edit; persists on save. |
| H4 | As a human manager, I want to **delete a todo** so I can remove cancelled or erroneous items. | Confirmation dialog; hard delete for MVP. |
| H5 | As a human observer, I want to **see which agent has claimed each todo** so I know what is actively being worked. | `ClaimedBy` and `ClaimedAt` visible per todo row. |
| H6 | As a human manager, I want to **force-release a claim** on a todo so I can unblock a stalled item. | "Release claim" button on any claimed todo; calls `DELETE /api/todos/{id}/claim`. |
| H7 | As a human observer, I want to **filter/sort the board** by status, priority, or assigned agent. | Filter bar and sortable column headers on dashboard. |

---

## 4. User Stories — Nice-to-Have (Post-MVP)

| # | Story |
|---|-------|
| N1 | As an AI agent, I want to authenticate via API key so the board is protected from rogue callers. |
| N2 | As a human, I want to log in with username/password so my actions are audited. |
| N3 | As a human manager, I want claim expiry (auto-release after N minutes of inactivity) so crashed agents don't block work. |
| N4 | As a human manager, I want **subtasks** (parent/child todos) so large tasks can be decomposed. |
| N5 | As a human manager, I want **due dates** on todos so time-sensitive work is visible. |
| N6 | As a human manager, I want **tags/labels** on todos for categorisation. |
| N7 | As a human observer, I want an **audit log** of all status transitions and claim events. |
| N8 | As a human observer, I want a **Kanban view** in addition to the list view. |
| N9 | As a human manager, I want **bulk operations** (bulk status update, bulk delete). |

---

## 5. Domain Model

### Todo Entity

| Field | Type | Nullable | Constraints | Notes |
|-------|------|----------|-------------|-------|
| `Id` | `Guid` | No | PK, auto-generated | |
| `Title` | `string` | No | Required, max 200 chars | |
| `Description` | `string` | Yes | Max 2 000 chars | |
| `Status` | `TodoStatus` (enum) | No | Default: `Pending` | See enum values below |
| `Priority` | `TodoPriority` (enum) | No | Default: `Medium` | See enum values below |
| `AssignedTo` | `string` | Yes | Max 100 chars | Logical assignment (not a claim/lock) |
| `ClaimedBy` | `string` | Yes | Max 100 chars | Agent ID holding the exclusive lock |
| `ClaimedAt` | `DateTime` | Yes | UTC | Set when claim is taken; cleared on release |
| `CreatedAt` | `DateTime` | No | UTC, auto-set on insert | |
| `UpdatedAt` | `DateTime` | No | UTC, auto-updated on every write | |

### Enums

```csharp
public enum TodoStatus
{
    Pending,      // Not started — available for agents to pick up
    InProgress,   // Actively being worked
    Blocked,      // Waiting on something external
    Done          // Complete
}

public enum TodoPriority
{
    Low,
    Medium,
    High,
    Critical
}
```

### Relationships
Single-entity MVP. No foreign keys in v1.

### Indexes (EF Core `OnModelCreating`)

| Index | Reason |
|-------|--------|
| `(Status, Priority DESC)` | Primary agent query pattern |
| `(ClaimedBy)` | Look up all todos held by a given agent |
| `(AssignedTo)` | Filter board by agent |

---

## 6. REST API Endpoints

Base path: `/api/todos`  
Content-Type: `application/json`

| Method | Route | Description | Request Body | Success Response |
|--------|-------|-------------|--------------|-----------------|
| `GET` | `/api/todos` | List todos. Supports query params: `status`, `priority`, `assignedTo`, `claimedBy` | — | `200 OK` `Todo[]` |
| `GET` | `/api/todos/{id}` | Get single todo | — | `200 OK` `Todo` / `404` |
| `POST` | `/api/todos` | Create todo | `CreateTodoRequest` | `201 Created` + `Location` header |
| `PUT` | `/api/todos/{id}` | Full update of todo | `UpdateTodoRequest` | `200 OK` `Todo` / `404` |
| `PATCH` | `/api/todos/{id}` | Partial update (status or priority change) | `PatchTodoRequest` | `200 OK` `Todo` / `404` |
| `DELETE` | `/api/todos/{id}` | Hard delete | — | `204 No Content` / `404` |
| `POST` | `/api/todos/{id}/claim` | Claim/lock a todo for an agent | `ClaimRequest` | `200 OK` `Todo` / `409 Conflict` if already claimed |
| `DELETE` | `/api/todos/{id}/claim` | Release claim | — | `200 OK` `Todo` / `404` |

### Request/Response Shapes

```json
// CreateTodoRequest
{
  "title": "Analyse training data",          // required
  "description": "Run outlier detection...", // optional
  "priority": "High",                        // optional, default Medium
  "assignedTo": "agent-summariser"           // optional
}

// UpdateTodoRequest (full replace of mutable fields)
{
  "title": "Analyse training data v2",
  "description": "Updated scope...",
  "status": "InProgress",
  "priority": "Critical",
  "assignedTo": "agent-summariser"
}

// PatchTodoRequest (any subset of mutable fields)
{
  "status": "Done"
}

// ClaimRequest
{
  "agentId": "agent-42"    // required — identifies the claiming agent
}

// Todo (response shape)
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Analyse training data",
  "description": "Run outlier detection on batch-7",
  "status": "InProgress",
  "priority": "High",
  "assignedTo": "agent-summariser",
  "claimedBy": "agent-42",
  "claimedAt": "2025-01-15T10:30:00Z",
  "createdAt": "2025-01-15T09:00:00Z",
  "updatedAt": "2025-01-15T10:30:00Z"
}
```

### Claim Conflict Rule
A `POST /api/todos/{id}/claim` where `ClaimedBy` is already set (and `claimedBy != agentId`) **MUST** return `409 Conflict` with:
```json
{ "error": "Todo is already claimed by agent-99", "claimedBy": "agent-99", "claimedAt": "2025-01-15T10:00:00Z" }
```
An agent **may** re-claim its own todo (idempotent).

---

## 7. Blazor UI Pages & Components

### Pages

| Page | Route | Description |
|------|-------|-------------|
| `Board.razor` | `/` | Main todo board — filterable list with live refresh |
| `TodoDetail.razor` | `/todo/{id}` | Full detail view + edit form for a single todo |
| `NewTodo.razor` | `/new` | Create todo form (or modal triggered from Board) |

### Key Components

| Component | Description |
|-----------|-------------|
| `TodoTable.razor` | Sortable/filterable MudDataGrid of all todos |
| `TodoRow.razor` | Single row: status badge, priority chip, claimed-by indicator, action buttons |
| `TodoForm.razor` | Shared create/edit form — used by Board (modal) and TodoDetail |
| `StatusBadge.razor` | Colour-coded MudChip for TodoStatus |
| `PriorityBadge.razor` | Colour-coded MudChip for TodoPriority |
| `ClaimIndicator.razor` | Shows lock icon + agent ID if claimed; "Release" button for managers |
| `FilterBar.razor` | Status/Priority/AssignedTo dropdowns + search box |
| `ConfirmDialog.razor` | Reusable MudDialog for destructive actions |

### UI Behaviour
- **Auto-refresh:** Board polls `GET /api/todos` every **5 seconds** (simple polling for MVP; SignalR in v2).
- **Claim state:** Claimed todos show a lock icon (🔒) and the agent ID. The "Claim" action is hidden for already-claimed rows.
- **Colour scheme:** Status badges — Pending=grey, InProgress=blue, Blocked=orange, Done=green. Priority chips — Low=default, Medium=info, High=warning, Critical=error (MudBlazor severity colours).

---

## 8. Technical Stack

| Layer | Technology | Version / Notes |
|-------|-----------|-----------------|
| Framework | .NET | 10 (latest) |
| Web UI | Blazor Server | Interactive Server rendering |
| UI Components | MudBlazor | 9.x (matching workspace) |
| REST API | ASP.NET Core Minimal API | Co-hosted in same process as Blazor |
| ORM | Entity Framework Core | 10.x, code-first migrations |
| Database (dev) | SQL Server LocalDB | `(localdb)\\mssqllocaldb` |
| Database (prod) | Azure SQL | Elastic Pool or Serverless tier |
| Hosting | Azure App Service | Linux container or Windows plan |
| CI/CD | GitHub Actions | Pattern from existing workspace |
| Testing (unit) | xUnit + NSubstitute | |
| Testing (E2E) | Playwright | `AgentBoard.E2E` project |
| Solution format | `.slnx` | Modern solution format (matching workspace) |

### Minimal API vs Controllers Decision
**Use Minimal API** (`/Api/TodoEndpoints.cs`). Rationale:
- Single resource (Todo), small surface area — no benefit from controller ceremony
- Minimal API integrates cleanly alongside Blazor Server in the same `Program.cs`
- Matches the lean, agent-first design of this project
- Easy to test via `WebApplicationFactory<Program>` integration tests

### EF Core Strategy
- Code-first migrations (`dotnet ef migrations add`)
- `ApplicationDbContext` in `AgentBoard/Data/`
- `Todo` model in `AgentBoard/Data/Models/`
- `UpdatedAt` auto-set via `SaveChangesAsync` override in `DbContext`
- Dev connection string: `"Server=(localdb)\\mssqllocaldb;Database=AgentBoard;Trusted_Connection=True;"`

---

## 9. Project Structure

Matches the workspace convention at `C:\data\itm8\Copilot\`.

```
C:\data\itm8\Copilot\AgentBoard\
│
├── AgentBoard\                          # Main web project
│   ├── Api\
│   │   └── TodoEndpoints.cs            # Minimal API route registrations
│   ├── Components\
│   │   ├── App.razor
│   │   ├── Routes.razor
│   │   ├── Layout\
│   │   │   ├── MainLayout.razor
│   │   │   └── NavMenu.razor
│   │   └── Pages\
│   │       ├── Board.razor             # / — main board
│   │       ├── TodoDetail.razor        # /todo/{id}
│   │       └── Error.razor
│   ├── Components\Shared\
│   │   ├── TodoTable.razor
│   │   ├── TodoRow.razor
│   │   ├── TodoForm.razor
│   │   ├── StatusBadge.razor
│   │   ├── PriorityBadge.razor
│   │   ├── ClaimIndicator.razor
│   │   ├── FilterBar.razor
│   │   └── ConfirmDialog.razor
│   ├── Data\
│   │   ├── ApplicationDbContext.cs
│   │   └── Models\
│   │       ├── Todo.cs                 # Entity
│   │       ├── TodoStatus.cs           # Enum
│   │       └── TodoPriority.cs         # Enum
│   ├── Migrations\                     # EF Core auto-generated
│   ├── Services\
│   │   └── TodoService.cs             # Business logic + EF queries
│   ├── Contracts\                      # Request/response DTOs
│   │   ├── CreateTodoRequest.cs
│   │   ├── UpdateTodoRequest.cs
│   │   ├── PatchTodoRequest.cs
│   │   └── ClaimRequest.cs
│   ├── Properties\
│   │   └── launchSettings.json
│   ├── wwwroot\
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Program.cs
│   └── AgentBoard.csproj
│
├── AgentBoard.Tests\                   # Unit + integration tests (xUnit)
│   ├── Services\
│   │   └── TodoServiceTests.cs
│   ├── Api\
│   │   └── TodoEndpointsTests.cs       # WebApplicationFactory integration tests
│   └── AgentBoard.Tests.csproj
│
├── AgentBoard.E2E\                     # Playwright end-to-end tests
│   ├── BoardTests.cs
│   └── AgentBoard.E2E.csproj
│
├── .github\
│   └── workflows\
│       ├── ci.yml                      # Build + unit tests on PR
│       └── deploy.yml                  # Deploy to Azure on merge to main
│
├── AgentBoard.slnx                     # Modern solution file
├── AGENTS.md                           # Agent brief (this doc summary)
└── requirements.md                     # This document
```

---

## 10. Non-Functional Requirements

| Category | Requirement |
|----------|-------------|
| **Performance** | API responses < 500 ms at P99 for up to 10 concurrent agents; dashboard load < 2 s |
| **Concurrency** | Claim endpoint must be atomic (EF Core optimistic concurrency or `UPDLOCK` hint) to prevent race conditions under simultaneous agent load |
| **Reliability** | API must return appropriate 4xx/5xx with `ProblemDetails` (RFC 7807) |
| **Security** | No auth for MVP; no sensitive data stored; all input validated + length-capped |
| **Accessibility** | WCAG 2.1 AA target for dashboard (MudBlazor default compliance) |
| **Browser support** | Latest Chrome, Edge, Firefox (Blazor Server requires WebSockets) |
| **Internationalisation** | English only for MVP |
| **Logging** | Structured logging via `ILogger`; log all claim/release events at `Information` level |

---

## 11. Integrations

| Integration | Status | Notes |
|-------------|--------|-------|
| AI Agents (REST consumers) | In scope — MVP | Agents call the REST API; no special SDK required |
| Azure SQL | In scope — prod | Swap LocalDB connection string; migrations run on startup |
| Azure App Service | In scope — prod | Single-plan deployment |
| Email / SMS | Out of scope | Not required |
| External APIs | Out of scope | No third-party integrations in MVP |

---

## 12. Out of Scope — MVP

The following are explicitly deferred to post-MVP:

- Authentication (API keys for agents, ASP.NET Identity for humans)
- Claim expiry / automatic release of stale claims
- Due dates on todos
- Tags / labels
- Subtasks / parent-child todo relationships
- Audit log / event history
- Kanban board view
- Bulk operations
- Email or webhook notifications
- Multi-tenancy / team isolation
- Recurring todos
- SignalR real-time push (polling used in MVP)

---

## 13. Open Questions & Assumptions

| # | Question / Assumption | Resolution |
|---|-----------------------|------------|
| OQ1 | **Claim expiry** — if an agent crashes mid-task, its claim will be held indefinitely. | Assumed: human manager manually releases via dashboard for MVP. Auto-expiry is v2. |
| OQ2 | **Concurrency mechanism** — optimistic concurrency (EF `RowVersion`) vs. `SELECT … WITH (UPDLOCK)` for claim atomicity. | Assumption: use EF Core optimistic concurrency (`[Timestamp]` on Todo) for claim. Architect agent to validate. |
| OQ3 | **Soft vs. hard delete** — no preference stated. | Assumption: hard delete for MVP (simpler). Soft delete (`DeletedAt`) is v2. |
| OQ4 | **API versioning** — not discussed. | Assumption: no versioning for MVP. Add `/api/v1/` prefix from day one as a convention so it's non-breaking later. |
| OQ5 | **Pagination** — not specified. | Assumption: `GET /api/todos` returns all todos for MVP (board is small). Add cursor/page pagination in v2. |
| OQ6 | **`AssignedTo` vs `ClaimedBy` semantics** — are they the same concept? | Decision: **`AssignedTo`** = logical/intended assignment (set by humans or at creation); **`ClaimedBy`** = runtime exclusive lock (set by agents). They are independent fields. |
| OQ7 | **Local dev database** — SQL Server LocalDB assumed available on dev machines. | Devs without LocalDB can swap to SQL Server Express or Docker (`mcr.microsoft.com/mssql/server`). |

---

## 14. Definition of Done

A feature is complete when:

1. ✅ Code compiles with zero warnings on `Release` configuration
2. ✅ All unit tests pass (`dotnet test`)
3. ✅ Integration tests pass for the API endpoint(s) involved
4. ✅ EF Core migration exists for any schema change
5. ✅ API endpoint documented in this spec is reachable and returns correct status codes
6. ✅ Blazor UI renders correctly in Chrome and Edge
7. ✅ No secrets committed to source control
8. ✅ PR reviewed and merged via `pr-review` agent
9. ✅ CI pipeline green on `main`
