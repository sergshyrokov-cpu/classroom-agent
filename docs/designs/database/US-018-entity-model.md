---
artifact_type: entity_model
story: US-018
version: 1
status: DRAFT
created_at: 2026-10-12T06:11:12Z
updated_at: 2026-10-12T06:11:12Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-018-spec.md
    version: 1
  - path: docs/designs/database/US-018-db-design.md
    version: 1
supersedes: null
---

# US-018 Entity Model — Incremental synchronization

No DTO is affected (API_DESIGN NOT_APPLICABLE); `GetLastSynchronizationQuery`
and its view model are unchanged (spec §10). Everything below is in `Domain`
except the repository method, which is an `Application` port implemented in
`Infrastructure` (AD-3, AD-4).

## 1. `Domain/Rules/IncrementalSyncRule` (new, static)

The rule of spec FR-001, FR-005, VR-001 … VR-003, with no clock and no
setting (AD-3; I-6).

```csharp
public static class IncrementalSyncRule
{
    /// spec FR-005, I-6: a constant, not a setting.
    public static readonly TimeSpan FullRunPeriod = TimeSpan.FromDays(7);

    /// spec FR-005, VR-002: null, a stored time later than the start (I-5), or
    /// at least FullRunPeriod before the start → full.
    public static bool IsFullRunDue(DateTimeOffset? lastFullRunAt, DateTimeOffset runStartedAt);

    /// db-design §5, VR-001: UTC, truncated to whole microseconds
    /// (UtcTicks − UtcTicks % 10).
    public static DateTimeOffset ToStoragePrecision(DateTimeOffset value);

    /// spec FR-001, VR-001, VR-003: listed state Archived, both instants present,
    /// equal after ToStoragePrecision on both sides.
    public static bool IsUnchangedArchive(
        CourseState listedState,
        DateTimeOffset? listedUpdateTime,
        DateTimeOffset? completeReadUpdateTime);
}
```

Behaviour verified in the scratch compile: same instant with sub-microsecond
ticks vs its truncated copy → unchanged; `Active` with equal instants → not;
`IsFullRunDue(null, t)` → true; exactly 7 days → true; 6.9 days → false;
stored later than start → true.

## 2. `Course` (changed)

| Member | Maps to | Business concept |
|---|---|---|
| `DateTimeOffset? CompleteReadUpdateTime { get; private set; }` | `course.complete_read_update_time` | the `updateTime` remembered at the last complete read (spec FR-002) |
| `void RecordCompleteRead()` | — | the course has just been read completely in this transaction |

`RecordCompleteRead()` sets `CompleteReadUpdateTime` to
`IncrementalSyncRule.ToStoragePrecision(UpdateTime)` — the value `Import` /
`UpdateFrom` just applied from this run's listing — or to null when
`UpdateTime` is null. It takes no argument, so it cannot record a value other
than the listed one.

Rules:

- `Import` and `UpdateFrom` do **not** touch `CompleteReadUpdateTime`; the mirror
  columns and the bookkeeping column stay separately writable (db-design §1).
- The use case calls `RecordCompleteRead()` inside the course's transaction,
  after the roster, coursework and submission writes have been staged and
  before the transaction's last `SaveChangesAsync`; nothing else calls it
  (spec FR-002). A course refused by the age rule is never created, so it is
  never called for one.
- No other member changes; there is still no `MarkMissing` or `Delete`.

## 3. `SyncState` (changed)

| Member | Maps to | Business concept |
|---|---|---|
| `DateTimeOffset? LastFullRunAt { get; private set; }` | `sync_state.last_full_run_at` | start of the last successful full run (spec FR-006, I-3) |
| `void CompleteRun(DateTimeOffset finishedAt, int processedCount, DateTimeOffset meetLoadedUpTo, bool fullRun = false)` | — | existing method, one parameter added |

- `CompleteRun` keeps every existing check and write. When `fullRun` is true it
  also sets `LastFullRunAt = StartedAt` (the run's start, I-3). When false,
  `LastFullRunAt` is unchanged. The default keeps every existing caller
  compiling with today's meaning.
- `FailRun`, `BeginRun`, `BeginFirstRun` never touch `LastFullRunAt`, so a failed
  or cancelled full run leaves it as it was and the next run is full again
  (OD-002 a, spec FR-006).
- Invariant mirrored from `ck_sync_state_last_full_run_at`: after
  `CompleteRun(..., fullRun: true)`, `LastFullRunAt ≤ LastSuccessfulRunAt`
  holds because `StartedAt ≤ finishedAt` is already enforced by `Finish`.

## 4. `Application/Ports/ICourseRepository` (changed)

```csharp
/// The remembered complete-read updateTime of the course with this Google id,
/// read untracked; null when the course is unknown or has no value
/// (db-design §6, spec FR-001 (c)).
Task<DateTimeOffset?> GetCompleteReadUpdateTimeAsync(string googleId, CancellationToken cancellationToken);
```

Implemented in `Infrastructure` with `AsNoTracking()`, projecting the one
column; no entity is attached, so the later tracked `GetByGoogleIdAsync` inside
the transaction is unaffected.

## 5. How the use case uses the model (for TEST_WRITING and IMPLEMENTATION)

1. `BeginAsync` reads `SyncState` (or none) and computes
   `fullRun = IncrementalSyncRule.IsFullRunDue(state?.LastFullRunAt, startedAt)`
   once, before `BeginRun`/`BeginFirstRun` (spec FR-005).
2. Per listed course, after the unrecognised-state and blank-name checks: if
   `!fullRun` and `IncrementalSyncRule.IsUnchangedArchive(state,
   snapshot.Details.UpdateTime, await courses.GetCompleteReadUpdateTimeAsync(...))`,
   count it as skipped-as-unchanged and continue — no reader call, no
   transaction (spec FR-001, FR-004, FR-008).
3. Otherwise as today; inside the transaction `course.RecordCompleteRead()`
   (spec FR-002).
4. On completion `state.CompleteRun(..., fullRun)` (spec FR-006). The outcome
   carries the skipped count for the host's completion and failure lines
   (spec FR-009, I-2).

## 6. Mapping summary

| Business concept (spec) | Entity member | Column |
|---|---|---|
| updateTime at last complete read (FR-002) | `Course.CompleteReadUpdateTime` | `course.complete_read_update_time` |
| time of last full run (FR-006, §3 v90) | `SyncState.LastFullRunAt` | `sync_state.last_full_run_at` |
| weekly full-run decision (FR-005) | `IncrementalSyncRule.IsFullRunDue` | — |
| skip rule (FR-001) | `IncrementalSyncRule.IsUnchangedArchive` | — |
| equality at storage precision (VR-001) | `IncrementalSyncRule.ToStoragePrecision` | — |
