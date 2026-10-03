---
artifact_type: entity_model
story: US-037
version: 1
status: DRAFT
created_at: 2026-10-03T16:58:45Z
updated_at: 2026-10-03T16:58:45Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-037-spec.md
    version: 1
  - path: docs/designs/database/US-037-db-design.md
    version: 1
  - path: docs/decisions/US-037-open-decisions.md
    version: 2
supersedes: null
---

# US-037 Entity Model — Retention purge

Names below are the design's intent. As with earlier Stories, they are verified
only once the skeleton built from them compiles. `TEST_WRITING` may adjust a
signature and record why. The tables, columns, constraints and behaviour in the
DB design may not be adjusted.

## 1. Changed entity: `AuditEvent` (Domain)

Five new read-only properties, private setters, null for every action except
the purge:

| Property | Type | Column |
|---|---|---|
| `PurgedCourses` | `int?` | `purged_courses` |
| `PurgedLeaverMemberships` | `int?` | `purged_leaver_memberships` |
| `PurgedParticipants` | `int?` | `purged_participants` |
| `PurgedAccounts` | `int?` | `purged_accounts` |
| `PurgedAuditRows` | `int?` | `purged_audit_rows` |

New factory, the only way to set them:

```csharp
public static AuditEvent RetentionPurgeRun(RetentionPurgeCounts counts, DateTimeOffset occurredAt)
```

It sets actor `System`, no actor id or role, action `RetentionPurgeRun`, no
target, outcome `Succeeded`, no request id. Each count is rejected if negative
(`ArgumentOutOfRangeException`). The entity still exposes no mutator and no free
string (SC-11).

## 2. New domain types

| Type | Kind | Purpose |
|---|---|---|
| `AuditAction.RetentionPurgeRun` | enum member | code `retention_purge_run` |
| `RetentionPurgeCounts` | `readonly record struct` (Domain) | the five counts; `Zero`; `Add…` helpers or `+` used by the use case to accumulate committed units |
| `RetentionRule` | `static class` (Domain) | the single definition of spec FR-001/FR-002 |

`RetentionRule`:

```csharp
public static DateTimeOffset Cutoff(DateTimeOffset now, int years);           // now.AddYears(-years)
public static bool IsExpired(DateTimeOffset date, DateTimeOffset cutoff);     // date < cutoff
public static DateTimeOffset? LatestActivity(IEnumerable<DateTimeOffset?> dates); // max, nulls ignored
```

- Synchronization (`RunSynchronizationUseCase.IsOlderThanRetention`) is
  rewritten on `LatestActivity` + `IsExpired` + `Cutoff`. Its own policy stays
  the same: no date at all → import.
- The purge applies `LatestActivity(...) ?? course.CreatedAt` (OD-006 (a)), then
  `IsExpired`.

## 3. New port (Application/Ports): `IRetentionPurgeStore`

Implemented in `Infrastructure/Persistence` (`RetentionPurgeStore`) with
`ExecuteDeleteAsync` / one grouped read. It never saves tracked changes. The
use case owns every transaction through `IUnitOfWork.ExecuteInTransactionAsync`.

```csharp
Task<IReadOnlyList<CourseActivityDates>> GetCourseActivityDatesAsync(CancellationToken ct);
Task DeleteCourseAsync(long courseId, CancellationToken ct);                  // §4 four statements
Task<IReadOnlyList<long>> GetCourseIdsWithExpiredLeaversAsync(DateTimeOffset cutoff, CancellationToken ct);
Task<int> DeleteExpiredLeaversAsync(long courseId, DateTimeOffset cutoff, CancellationToken ct); // returns memberships
Task<int> DeleteOrphanedParticipantsAsync(CancellationToken ct);
Task<int> DeleteExpiredAccountsAsync(DateTimeOffset cutoff, CancellationToken ct);
Task<int> DeleteAuditEventsOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct);
```

`CourseActivityDates` is an Application model (`Application/Models`): `CourseId`,
`CourseUpdateTime?`, `CourseCreatedAt`, `LatestItemCreationTime?`,
`LatestItemUpdateTime?`, `LatestSubmissionUpdateTime?`. These are raw maxima. The
rule itself stays in `RetentionRule`.

`IAuditEventRepository` is unchanged: add-only. Its comment is corrected from
"EPIC-10" to US-037 (spec FR-018).

## 4. Use case (Application/UseCases): `RunRetentionPurgeUseCase`

```csharp
Task<RetentionPurgeOutcome> ExecuteAsync(CancellationToken ct);
```

- Declares `ServiceWriteScope.Declare(PermittedServiceWrite.RetentionPurge)` for
  the whole run, and is registered in `PermittedServiceWrites.Declarations`
  (BR-026). It calls no `IReadOnlyModeGuard` and no Google port.
- Reads `Retention:Years` through the existing `RetentionSettings` (Application/Models) already
  injected into synchronization.
- Steps and transactions as in spec FR-009 / DB design §6. It ends by adding
  `AuditEvent.RetentionPurgeRun(counts, startedAt)` and `SaveChangesAsync`.
- `RetentionPurgeOutcome` (Application model) carries the counts and the number
  of failed units, for the background service's completion log. It has no ids
  and no data.

## 5. Web host

- `RetentionPurgeBackgroundService` (`Web/BackgroundServices`, package-map) runs
  the use case in its own DI scope. The schedule is spec FR-013 and the
  shutdown rule is FR-015.
- The gate of spec FR-014 is in-process state shared with
  `SynchronizationBackgroundService`. Recommended shape: extend
  `SyncRunCoordinator` with a purge slot (`TryStartPurge` / `PurgeCompleted`), so
  that one lock decides "nothing else running" for both kinds. Then
  `TryStartScheduledRun` / `TryStartRequestedRun` also return false while a purge
  runs, and `Changed` wakes waiters when it ends. A remembered request survives.
  `IsRunning` keeps meaning "a synchronization run" (API design §3:
  readiness is unaffected).

## 6. Mapping to business concepts and DTOs

| Business concept (§5) | Model |
|---|---|
| срок N | `Retention:Years` → `RetentionRule.Cutoff` |
| последняя активность курса | `CourseActivityDates` + `RetentionRule.LatestActivity` (+ `CreatedAt` fallback, OD-006) |
| курс с истёкшим сроком | `RetentionRule.IsExpired` over the above → `DeleteCourseAsync` |
| выбывший | `CourseMembership` with `OnRoster = false`, `LastSeenAt` expired |
| участник без участий | `ClassroomParticipant` with no `CourseMembership` |
| неиспользуемая учётная запись | `AppUser` by `LastSuccessfulSignInAt ?? CreatedAt` |
| строка аудита со сроком | `AuditEvent.OccurredAt` |
| событие аудита прогона | `AuditEvent.RetentionPurgeRun` + `RetentionPurgeCounts` |

No API DTO: the Story has no endpoint (API design NOT_APPLICABLE).
