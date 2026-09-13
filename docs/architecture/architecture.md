# Architecture

Explicit architecture decisions for classroom-agent. These are project
decisions, not general framework advice. Skills (`openapi-designer`,
`db-designer`, `dotnet-implementor`, `security-reviewer`) treat this file as
authoritative.

Derived from `trebovaniya.md` sections 5, 8 and 9. If this file contradicts
`trebovaniya.md`, this file is wrong.

## AD-1 Two deployables, physically separated

The system ships as **two independent applications**:

| Deployable | Instances | Database | Purpose |
|---|---|---|---|
| **Data Plane** (`ClassroomAgent.Web`) | one per school | one per school | the application school staff use: courses, journals, Meet statistics, sync |
| **Control Plane** (`ClassroomAgent.ControlPlane`) | one, shared | its own | Owner authentication, `Installation` registry, `AllowedAdmin` list, Admin login check, legitimacy checks and push |

They never share a database and never reference each other's projects. The only
coupling is the wire contract in `ClassroomAgent.Contracts`, referenced by both.

This is not an optimization — `trebovaniya.md` section 9 requires physical
isolation of school data. A multi-tenant database is an architecture violation.

## AD-2 Solution & project layout

Solution file `ClassroomAgent.sln`.

```
src/
  ClassroomAgent.Domain/          entities, enums, domain rules — no dependencies
  ClassroomAgent.Application/     use cases, ports (interfaces), DTOs
  ClassroomAgent.Infrastructure/  EF Core (Npgsql), Google API clients, secrets
  ClassroomAgent.Web/             Data Plane host: Controllers + Razor + BackgroundService
  ClassroomAgent.Contracts/       Control Plane ↔ Data Plane wire contracts (leaf)
  ClassroomAgent.ControlPlane/    Control Plane host + its own EF Core context
tests/
  ClassroomAgent.Tests/           xUnit, namespace mirrors the production tree
```

Root namespace per project matches the folder name. No new solution project
without an approved decision.

## AD-3 Layered architecture (Clean Architecture)

`A → B` means A references B.

```
Web            → Application → Domain
Web            → Infrastructure            (wiring only, at startup)
Infrastructure → Application, Domain       (implements the ports)
```

| Layer | Project | Responsibility | Must not |
|---|---|---|---|
| Presentation | `Web` | HTTP mapping, Razor views, DTO binding, auth wiring, hosted services | contain business rules; touch `DbContext`; call a Google API directly |
| Application | `Application` | use cases, orchestration, transaction boundaries, entity↔DTO mapping, **port interfaces** | depend on EF Core, `HttpContext`, or any Google SDK type |
| Domain | `Domain` | entities, value objects, enums, invariants | depend on anything else in the solution |
| Infrastructure | `Infrastructure` | EF Core `DbContext` and repositories, Google API clients, secret access | contain business logic; be referenced by `Domain` or `Application` at compile time except through DI |

**Dependency rule:** `Domain` depends on nothing. `Application` depends on
`Domain` only. `Infrastructure` depends on `Application` and `Domain` and
*implements* the ports declared in `Application`. `Web` depends on
`Application` and wires `Infrastructure` implementations at startup.

**The Control Plane is one project with internal boundaries.** It references only
`Contracts` (AD-1), so the layers above do not apply to it as projects. Inside
`ClassroomAgent.ControlPlane`:

- `Services` holds the business rules (Installation status, `AllowedAdmin`,
  legitimacy and compatibility checks) and opens transactions (AD-7);
- `Persistence` holds its own `DbContext`, used only from `Services`;
- `Controllers` do HTTP mapping only: no business rules, no `DbContext`, and no
  persistence entity in a signature or body — `Services` return DTOs.

The boundary is kept by review and tests, not by the compiler; a controller
touching `DbContext` is a defect.

`Application → Infrastructure` as a project reference is forbidden. Anything
`Application` needs from the outside world is a port interface it declares and
`Infrastructure` implements.

## AD-4 Ports for every external system

Each external dependency is reached through an interface declared in
`Application/Ports`:

| Port | Implemented by | Wraps |
|---|---|---|
| `IClassroomReader` | `Infrastructure/Google` | Google Classroom API |
| `IMeetReportsReader` | `Infrastructure/Google` | Admin Reports API (Meet `call_ended` audit events) |
| `IWorkspaceCredentialProvider` | `Infrastructure/Secrets` | service-account key resolution (see SC-7) |
| `IControlPlaneClient` | `Infrastructure/ControlPlane` | legitimacy check and Admin login `AllowedAdmin` check calls to the Control Plane (SC-3) |
| `IReportRenderer` | `Infrastructure/Export` | Excel/Word generation |

No Google SDK type crosses into `Application` or `Domain`. Sync code works with
domain entities, not `Google.Apis.Classroom.v1.Data.*`.

Google and the Control Plane are the only external systems that receive or
provide school data. A port to any other external system — an AI or
speech-recognition service (future Epic 13), analytics, telemetry — requires a
separate approved decision (SC-13, BR-078).

## AD-5 Background synchronization

- Synchronization runs in a `BackgroundService` hosted by `ClassroomAgent.Web`,
  never inside a web request (`trebovaniya.md` section 5 — the Streamlit
  prototype's in-request thread is exactly what is being replaced).
- A request that "starts a sync" enqueues work and returns immediately; progress
  and errors are read from `SyncState`.
- Sync is idempotent: repeated runs upsert, never duplicate.
- Transient Google failures (`429`, `5xx`) are retried with exponential backoff.
  Permission failures (`403 unauthorized_client`, `access_denied`, missing
  scope) are **not** retried — they mean the school has not completed
  domain-wide delegation. They are recorded in `SyncState` with a diagnosable
  message and surfaced to the Admin (`trebovaniya.md` Epic 1, Epic 6).
- The Meet event pull is part of synchronization. Two more background services
  run in `ClassroomAgent.Web`: the retention purge, once a day (PC-11), and the
  legitimacy check, every 6 hours (BR-024) (`package-map.md`).

## AD-6 Read-only mode is enforced in Application, not the UI

When the installation is in read-only mode — grace period expired, or the Owner
suspended the `Installation` — every write use case refuses except the service
writes listed below. The check lives in the Application layer so it cannot be
bypassed by calling an API endpoint directly; hiding a button in Razor is
presentation polish, not enforcement.

Permitted in read-only mode: viewing and exporting already-synced data, plus the
closed list of service writes in BR-026 — that list is the only source; do not
restate it here. Everything else is blocked — for example
synchronization, Meet meeting-code linking, account management, connection
settings, "check access", report template edits. No port that calls Google
(`IClassroomReader`, `IMeetReportsReader`) is invoked in read-only mode. See
`trebovaniya.md` sections 2 and 9.

## AD-7 Transaction boundary policy

- Transactions begin and end in the **Application** layer; in the Control Plane,
  in its `Services` namespace (AD-3).
- Repositories stage changes and never call `SaveChangesAsync()`; the owning use
  case commits.
- Several writes that must be atomic are wrapped in an explicit
  `IDbContextTransaction`.
- Read-only queries use `AsNoTracking()` by default.
- Presentation and Infrastructure repositories never open a transaction.

## AD-8 DTO / entity boundary

- API request bodies bind to classes in `Application/Models/Requests`.
- API response bodies are classes in `Application/Models/Dtos`.
- Domain entities never appear in a controller signature, a request body, a
  response body, or a Razor view model.
- Mapping happens in the Application layer. No mapping library is added without
  an approved decision.
- A response DTO carries only fields the API contract lists. Credentials,
  password hashes and service-account material are never on a DTO, even as
  `null`.

## AD-9 Exception handling

- One `IExceptionHandler` per host project, registered via
  `AddExceptionHandler<T>()` / `UseExceptionHandler()`, is the single place
  mapping exceptions to HTTP responses.
- Domain and application exceptions live in `Application/Exceptions` and carry
  no HTTP concepts.
- Mapping: validation → 400, authn → 401, authz → 403, not found → 404,
  conflict → 409, read-only mode → 409, unmapped → 500.
- Error bodies follow `api-conventions.md` AC-6. Stack traces, SQL, entity
  names, file paths and secrets never appear in a response.

## AD-10 Configuration boundaries

- Startup wiring lives in `Program.cs` and `IServiceCollection` extension
  methods under `Configuration`.
- No business logic in a `Configuration` extension method.
- Nothing school-specific is hard-coded — the prototype's hard-coded
  `admin@dac.ukr.education` is a defect being fixed, not a pattern to copy
  (`trebovaniya.md` section 5). Values come from two different places:
  - **Installation configuration** — `appsettings.json` /
    `appsettings.{Environment}.json` / environment variables, set by the Owner at
    deployment (DC-3): the database connection string, the Control Plane
    endpoint, the reference to the service-account key, the retention period N,
    the school's time zone and the default UI language. They are validated at
    startup; without N or the time zone the installation refuses to start.
  - **Settings entered by the Admin** and stored in `WorkspaceConnection`: the
    Google Workspace domain and the technical account used as impersonation user
    (v30, v33). They are not deployment configuration.

## AD-11 Reuse over duplication

Before creating a component, check for an existing one that can be extended
within these rules. New namespaces or folders beyond `package-map.md` require an
approved decision — an Open Decision resolved by a human, not a silent addition.
