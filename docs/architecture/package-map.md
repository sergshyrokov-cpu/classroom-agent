# Package Map

Six production projects (`architecture.md` AD-2). Test namespaces mirror the
production tree under `tests/ClassroomAgent.Tests/`. Adding a namespace, folder
or project not listed here requires an approved decision.

## Project dependency graph

```
Domain        ← Application ← Infrastructure
                    ↑              ↑
                   Web ────────────┘        (Data Plane host)
                    │
Contracts ←─────────┴───────────→ ControlPlane   (separate host, own DB)
```

| Project | May reference | Must not reference |
|---|---|---|
| `ClassroomAgent.Domain` | — | anything |
| `ClassroomAgent.Application` | `Domain` | `Infrastructure`, `Web`, `ControlPlane` |
| `ClassroomAgent.Infrastructure` | `Application`, `Domain`, `Contracts` | `Web`, `ControlPlane` |
| `ClassroomAgent.Web` | `Application`, `Domain`, `Infrastructure`, `Contracts` | `ControlPlane` |
| `ClassroomAgent.Contracts` | — | anything |
| `ClassroomAgent.ControlPlane` | `Contracts` | `Domain`, `Application`, `Infrastructure`, `Web` |

`Application → Infrastructure` is the violation to watch for: it is how Clean
Architecture usually rots. `Application` declares a port; `Infrastructure`
implements it; `Web` wires them.

`ControlPlane → Domain` is equally forbidden. The Control Plane knows nothing
about courses, journals or students — only `Installation`, `AllowedAdmin`,
`Owner` and check results (`trebovaniya.md` section 9).

## Namespaces within each project

### `ClassroomAgent.Domain`

| Namespace | Contains | Notes |
|---|---|---|
| `Entities` | `AppUser`, `Course`, `ClassroomParticipant`, `CourseMembership`, `CourseWork`, `Submission`, `MeetSession`, `MeetParticipation`, `MeetingCodeLink`, `SyncState`, `WorkspaceConnection`, `ReportTemplate`, `AuditEvent`, `LegitimacyState` | persisted domain state; leaf |
| `Enums` | `AppRole` (Admin, Dean), `CourseState`, `ClassroomRole` (on `CourseMembership`), `CourseWorkKind` (graded work, ungraded work, material — v32), `SyncStatus`, `SubmissionState`, `MeetingCodeLinkStatus` | |
| `Rules` | invariants that hold regardless of use case | no I/O |

`AppRole` has exactly two members in the first version. Teacher and Student are
Epic 7 — do not add them speculatively.

### `ClassroomAgent.Application`

| Namespace | Contains | Depends on |
|---|---|---|
| `UseCases` | one class per use case: orchestration, transaction boundary, read-only-mode check | `Domain`, `Ports`, `Models`, `Exceptions` |
| `Ports` | interfaces for every external system (`architecture.md` AD-4) | `Domain`, `Models` |
| `Models.Dtos` | API and view-model **response** types | leaf |
| `Models.Requests` | API **request** types with Data Annotations | `Validation` |
| `Validation` | custom `ValidationAttribute` / `IValidatableObject` | `Domain` (read-only) |
| `Exceptions` | domain/application exceptions, no HTTP concepts | `Models.Dtos` |
| `Authorization` | the permission matrix from `trebovaniya.md` section 2, as policy definitions | `Domain` |

### `ClassroomAgent.Infrastructure`

| Namespace | Contains | Notes |
|---|---|---|
| `Persistence` | `ClassroomAgentDbContext`, entity configurations, migrations | Npgsql; see `persistence-conventions.md` |
| `Persistence.Repositories` | repository implementations | queries and staged writes only; no `SaveChangesAsync()` |
| `Google` | `IClassroomReader`, `IMeetReportsReader` implementations | the only place Google SDK types exist |
| `Secrets` | `IWorkspaceCredentialProvider` implementation | reads the service-account key from the configured secret store, never from the database |
| `ControlPlane` | `IControlPlaneClient` implementation | uses `Contracts` types |
| `Export` | `IReportRenderer` implementation | ClosedXML/EPPlus, OpenXML/DocX |

### `ClassroomAgent.Web` (Data Plane host)

| Namespace | Contains | Notes |
|---|---|---|
| `Controllers` | REST API controllers | no business logic, no `DbContext` |
| `Pages` / `Views` | Razor pages and views | presentation only |
| `BackgroundServices` | `SyncBackgroundService` | `architecture.md` AD-5 |
| `Security` | Identity setup, Google OAuth external login, authorization policy registration | `security-conventions.md` |
| `Configuration` | `IServiceCollection` extensions wiring `Infrastructure` | no business logic |
| `Exceptions` | the single `IExceptionHandler` | |

### `ClassroomAgent.Contracts`

Wire types for the Control Plane ↔ Data Plane channel only: the legitimacy
check request/response, the status-change push payload, and the Admin login
check request/response. No behaviour, no dependencies. Both hosts reference it
so the contract cannot drift. It never carries teaching data or school
statistics — only installation id, versions, status, compatibility state and the
email checked at an Admin login (SC-12).

### `ClassroomAgent.ControlPlane`

| Namespace | Contains | Notes |
|---|---|---|
| `Controllers` | Owner UI + the check endpoints called by installations | HTTP mapping only: no business rules, no `DbContext` (AD-3) |
| `Services` | business rules and transaction boundaries: Installation status, `AllowedAdmin`, legitimacy and compatibility checks; return DTOs | the only callers of `Persistence` |
| `Persistence` | its **own** `DbContext`: `Owner`, `Installation`, `AllowedAdmin`, `InstanceLicenseCheck`, `AuditEvent` | separate database |
| `Security` | Owner authentication (Identity, first-run setup) | |
| `Push` | outbound status-change notification to installations | `architecture.md` AD-1, `trebovaniya.md` section 9 |

## Test namespace rule

For a production class `ClassroomAgent.<Project>.<Namespace>.<Name>`, its tests
live in `ClassroomAgent.Tests.<Project>.<Namespace>` under
`tests/ClassroomAgent.Tests/`. Integration tests spanning layers may sit in a
`ClassroomAgent.Tests.<Feature>` namespace under the same root.
