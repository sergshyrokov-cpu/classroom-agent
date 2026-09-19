# Package Map

Six production projects (`architecture.md` AD-2). Test namespaces mirror the
production tree under `tests/ClassroomAgent.Tests/`. Adding a namespace, folder
or project not listed here requires an approved decision.

## Project dependency graph

`A ──→ B` means A references B. The table below is authoritative.

```
Web (Data Plane host) ──→ Application ──→ Domain
  │  │                         ↑             ↑
  │  └──────→ Infrastructure ──┴─────────────┘
  │                │
  └──────→ Contracts ←── ControlPlane   (separate host, own DB)
```

`Web` and `ControlPlane` both reference `Contracts`; they never reference each
other.

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
`Owner`, check results and its own `AuditEvent` (`trebovaniya.md` section 9).

## Namespaces within each project

### `ClassroomAgent.Domain`

| Namespace | Contains | Notes |
|---|---|---|
| `Entities` | `AppUser`, `Course`, `ClassroomParticipant`, `CourseMembership`, `CourseWork`, `Submission`, `MeetSession`, `MeetParticipation`, `MeetingCodeLink`, `SyncState`, `WorkspaceConnection`, `ReportTemplate`, `AuditEvent`, `LegitimacyState` | persisted domain state; leaf |
| `Enums` | `AppRole` (Admin, Dean), `CourseState`, `ClassroomRole` (on `CourseMembership`), `CourseWorkKind` (graded work, ungraded work, material — v32; computed from the Classroom resource and maximum points, never stored, PC-3), `SyncStatus`, `SubmissionState`, `MeetingCodeLinkStatus` | |
| `Rules` | invariants that hold regardless of use case | no I/O |

`AppRole` has exactly two members in the first version. Teacher and Student are
Epic 7 — do not add them speculatively.

### `ClassroomAgent.Application`

| Namespace | Contains | Depends on |
|---|---|---|
| `UseCases` | one class per use case: orchestration, transaction boundary, read-only-mode check | `Domain`, `Ports`, `Models`, `Exceptions` |
| `Ports` | interfaces for every external system (`architecture.md` AD-4) — only Google and the Control Plane; a port to any other external system needs a separate decision (SC-13) | `Domain`, `Models` |
| `Models.Dtos` | API and view-model **response** types | leaf |
| `Models.Requests` | API **request** types with Data Annotations | `Validation` |
| `Validation` | custom `ValidationAttribute` / `IValidatableObject` | `Domain` (read-only) |
| `Exceptions` | domain/application exceptions, no HTTP concepts | `Models.Dtos` |
| `Authorization` | the permission matrix from `trebovaniya.md` section 2, as policy definitions | `Domain` |
| `Localization` | translation files (Ukrainian, English) for screens, error messages, the super-admin instructions and export labels — shared by `Web` and `Infrastructure/Export` (NFR-073) | leaf |

### `ClassroomAgent.Infrastructure`

| Namespace | Contains | Notes |
|---|---|---|
| `Persistence` | `ClassroomAgentDbContext`, entity configurations, migrations | Npgsql; see `persistence-conventions.md` |
| `Persistence.Repositories` | repository implementations | queries and staged writes only; no `SaveChangesAsync()` |
| `Google` | `IClassroomReader`, `IMeetReportsReader` implementations | the only place Google SDK types exist |
| `Secrets` | `IWorkspaceCredentialProvider` implementation | reads the service-account key from the configured secret store, never from the database |
| `ControlPlane` | `IControlPlaneClient` implementation | uses `Contracts` types |
| `Export` | `IReportRenderer` implementation | ClosedXML/EPPlus, OpenXML/DocX |
| `ReadOnly` | the decorators that log a read-only refusal and rethrow it (US-007 spec FR-009) | they observe the refusal; the rule itself stays in `Application` (AD-6) |

### `ClassroomAgent.Web` (Data Plane host)

| Namespace | Contains | Notes |
|---|---|---|
| `Controllers` | REST API controllers | no business logic, no `DbContext` |
| `Pages` / `Views` | Razor pages and views | presentation only |
| `BackgroundServices` | `SyncBackgroundService` (Classroom and the Meet event pull — both are synchronization, `trebovaniya.md` §2), `RetentionPurgeBackgroundService` (daily, PC-11), `LegitimacyCheckBackgroundService` (every 6 hours, BR-024) | `architecture.md` AD-5 |
| `Security` | Identity setup, Google OAuth external login, authorization policy registration, the deny-by-default fallback policy and the closed list of anonymous endpoints; global antiforgery validation, the closed list of antiforgery exemptions and the result filter for an antiforgery refusal; the anonymous fallback catch-all answering `404` and the error page for `400`, `403`, `404` and `500`; the private route group (push receiver, liveness, readiness) and its filter checking the connection's local port; cookie settings, HTTPS redirection and HSTS on the public port, the Data Protection key ring location | `security-conventions.md` SC-2, SC-4, SC-7; `api-conventions.md` API-7; `deployment-conventions.md` DC-6 |
| `Configuration` | `IServiceCollection` extensions wiring `Infrastructure` | no business logic |
| `Exceptions` | the single `IExceptionHandler` | API-6 body under `/api/v1`, the error page elsewhere (AD-9) |

### `ClassroomAgent.Contracts`

Wire types for the Control Plane ↔ Data Plane channel only: the legitimacy
check request/response, the status-change push payload (the installation id only, v76), and the Admin login
check request/response. No behaviour, no dependencies. Both hosts reference it
so the contract cannot drift. It never carries teaching data or school
statistics — only installation id, versions, status, compatibility state, the
`Installation`'s domain and client ID (in the legitimacy check response, v54) and
the email checked at an Admin login (SC-12).

### `ClassroomAgent.ControlPlane`

| Namespace | Contains | Notes |
|---|---|---|
| `Controllers` | Owner UI + the check endpoints called by installations | HTTP mapping only: no business rules, no `DbContext` (AD-3) |
| `Services` | business rules and transaction boundaries: Owner first-run setup with the one-time setup code, Owner sign-in and its audit, Installation status, `AllowedAdmin`, legitimacy and compatibility checks; return DTOs | the only callers of `Persistence` |
| `Persistence` | its **own** `DbContext`: `Owner`, `Installation`, `AllowedAdmin`, `InstanceLicenseCheck`, `AuditEvent` | separate database |
| `Security` | Identity and cookie wiring (cookie settings, the Data Protection key ring location), the deny-by-default fallback policy and the anonymous endpoints of SC-4, global antiforgery validation, the closed list of antiforgery exemptions and the result filter for an antiforgery refusal, the anonymous fallback catch-all answering `404` and the error page for `400`, `403`, `404` and `500` | no business rules — setup and sign-in logic is in `Services` |
| `Localization` | its own translation files (Ukrainian, English) for the Owner UI — it cannot reference `Application.Localization` | NFR-073 |
| `Push` | outbound status-change notification to installations | `architecture.md` AD-1, `trebovaniya.md` section 9 |

## Test namespace rule

For a production class `ClassroomAgent.<Project>.<Namespace>.<Name>`, its tests
live in `ClassroomAgent.Tests.<Project>.<Namespace>` under
`tests/ClassroomAgent.Tests/`. Integration tests spanning layers may sit in a
`ClassroomAgent.Tests.<Feature>` namespace under the same root.
