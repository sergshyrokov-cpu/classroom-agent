---
artifact_type: entity_model
story: US-024
version: 1
status: DRAFT
created_at: 2026-10-12T12:56:53Z
updated_at: 2026-10-12T12:56:53Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-024-spec.md
    version: 1
  - path: docs/designs/api/US-024-openapi.yaml
    version: 1
  - path: docs/designs/database/US-024-db-design.md
    version: 1
supersedes: null
---

# US-024 Entity Model — Database statistics and last synchronization

The signatures below were compiled in a scratch project referencing
`ClassroomAgent.Infrastructure` (net10.0, nullable enabled, warnings as errors:
0 warnings, 0 errors). Names are binding unless marked indicative. One public
type per file in the real code.

## 1. Domain entities

**Unchanged.** Read only: `Course` (`State`), `CourseMembership` (`Role`,
`OnRoster`, `ParticipantId`), `CourseWork`, `Submission`, `MeetSession`
(`MeetingCode`), `MeetingCodeLink` (`MeetingCode`), `SyncState` (`Status`,
`LastSuccessfulRunAt`, `LastFullRunAt`, `MeetLoadedUpTo`). No entity gains a
member; no Domain enum changes.

## 2. Application port and record

```csharp
namespace ClassroomAgent.Application.Models;

/// Q1's result (db-design §3.1). Counts only.
public sealed record DatabaseCounts(
    int ActiveCourses, int ArchivedCourses, int ProvisionedCourses, int DeclinedCourses, int SuspendedCourses,
    int Teachers, int Students, int CourseWorkItems, int Submissions, int MeetMeetings, int MeetCodesAwaitingDecision);
```

```csharp
namespace ClassroomAgent.Application.Ports;

public interface IDatabaseStatisticsSource
{
    Task<DatabaseCounts> GetCountsAsync(CancellationToken cancellationToken);
}
```

Reused unchanged: `ISyncStateRepository.GetForReadAsync`, `SchoolTimeZone`.

## 3. Query

```csharp
namespace ClassroomAgent.Application.UseCases;

public sealed class GetDatabaseStatisticsQuery(
    IDatabaseStatisticsSource counts, ISyncStateRepository states, SchoolTimeZone schoolTimeZone)
{
    public Task<DatabaseStatisticsView> ExecuteAsync(AppRole role, CancellationToken cancellationToken);
}
```

- `role` is the session's role (`ClassroomAgent.Domain.Enums.AppRole`), supplied
  by the host; it decides `FailureGuidance` only.
- No `IReadOnlyModeGuard`, no write, no Google port (spec FR-008).
- An unknown `SyncRunStatus` throws `ArgumentOutOfRangeException` (spec VR-003),
  as `GetLastSynchronizationQuery`.
- `Total` = sum of the five state counts (spec I-3).

## 4. DTOs (`Models/Dtos`) — openapi mapping

```csharp
namespace ClassroomAgent.Application.Models.Dtos;

public sealed record CourseCounts(int Active, int Archived, int Provisioned, int Declined, int Suspended, int Total);

public enum SynchronizationDisplayState { NotYet, InProgress, Completed, Failed }

public enum FailureGuidance { None, AskAdmin, ConnectionPageLink }

public sealed record SynchronizationBlock(
    SynchronizationDisplayState State, DateTime? LastSuccessfulRunAt, DateTime? LastFullRunAt, DateTime? MeetLoadedUpTo);

public sealed record DatabaseStatisticsView(
    CourseCounts Courses, int Teachers, int Students, int CourseWorkItems, int Submissions, int MeetMeetings,
    int MeetCodesAwaitingDecision, SynchronizationBlock Synchronization, FailureGuidance FailureGuidance);
```

| openapi (`DatabaseStatisticsPageModel`) | C# | Source |
|---|---|---|
| `courses.*` | `DatabaseStatisticsView.Courses` | Q1 five states; `Total` computed |
| `teachers`, `students` | `Teachers`, `Students` | Q1 |
| `courseWorkItems`, `submissions`, `meetMeetings` | same names | Q1 |
| `meetCodesAwaitingDecision` | `MeetCodesAwaitingDecision` | Q1 |
| `synchronization.state` | `SynchronizationBlock.State` | Q2: none → `NotYet`; Running → `InProgress`; Completed → `Completed`; Failed → `Failed` |
| `synchronization.lastSuccessfulRunAt` | `LastSuccessfulRunAt` | `SyncState.LastSuccessfulRunAt` in the school's zone |
| `synchronization.lastFullRunAt` | `LastFullRunAt` | `SyncState.LastFullRunAt` in the school's zone |
| `synchronization.meetLoadedUpTo` | `MeetLoadedUpTo` | `SyncState.MeetLoadedUpTo` in the school's zone |
| `failureGuidance` | `FailureGuidance` | `Failed` + Dean → `AskAdmin`; `Failed` + Admin → `ConnectionPageLink`; else `None` |
| `synchronizationMessageKey` | host page model (§5) | TempData / `409` re-render |

`DateTime` values are wall-clock in the school's zone (`Kind` Unspecified), as
`LastSynchronizationView.MeetLoadedUpTo` already is.

## 5. Host (`Web`)

Indicative, fixed in IMPLEMENTATION within the api-design:

- `DatabaseStatisticsPageModel(DatabaseStatisticsView Statistics, string? SynchronizationMessageKey)` —
  the view model of `/database`; the key is a US-019 translation key from
  `SynchronizationRequestTextKeys`.
- `DatabaseController` — `GET /database` (`ViewDatabaseStatistics`) and
  `POST /database/synchronization-requests` (`StartSynchronization`), calling
  `GetDatabaseStatisticsQuery` and `RequestSynchronizationUseCase`.
- `SignInRoutes.Database`, `SignInRoutes.DatabaseSynchronizationRequests`;
  `InstallationPolicies.ViewDatabaseStatistics`.

## 6. Persistence

```csharp
namespace ClassroomAgent.Infrastructure.Persistence;

public sealed class DatabaseStatisticsReadSource(ClassroomAgentDbContext db) : IDatabaseStatisticsSource
{
    public Task<DatabaseCounts> GetCountsAsync(CancellationToken cancellationToken);
}
```

- Runs Q1 (db-design §3.1) through `db.Database.SqlQuery<T>`; `T` is an
  internal keyless record of eleven `long` columns, narrowed with `checked`.
- Registered in the installation's composition root beside the other read
  sources (scoped).
- No entity configuration, no migration, no model snapshot change.
