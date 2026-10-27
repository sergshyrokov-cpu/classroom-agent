---
artifact_type: entity_model
story: US-059
version: 1
status: DRAFT
created_at: 2026-10-27T11:20:00Z
updated_at: 2026-10-27T11:20:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-059-spec.md
    version: 1
  - path: docs/designs/database/US-059-db-design.md
    version: 1
supersedes: null
---

# US-059 Entity Model

Signatures below are the contract TEST_WRITING compiles its skeleton from (standing rule); a
signature that does not compile loops back here. No API DTO is involved (API_DESIGN
NOT_APPLICABLE); nothing of this Story reaches a controller or view, and `WorkspaceConnectionView`
(the settings screen DTO) is deliberately **not** extended.

## 1. `CourseAbsenceMark` (Domain/Enums, new)

Business concept: the two course marks of §4 Epic 1 (BR-048, BR-049).

```csharp
namespace ClassroomAgent.Domain.Enums;

/// <summary>Why a stored course is marked absent from Google's course list (US-059 VR-003). Stored as a lower-case code.</summary>
public enum CourseAbsenceMark
{
    NotInClassroom,
    NotVisibleToTechnicalAccount,
}
```

Stored through a value converter as `not_in_classroom` / `not_visible_to_technical_account`
(the `CourseSkipReason` pattern), `HasMaxLength(40)`.

## 2. `Course` (Domain/Entities, existing) — the mark and the sighting

```csharp
/// <summary>US-059 FR-001, FR-006: the course's mark; null while the course is on the list. Bookkeeping, not mirror.</summary>
public CourseAbsenceMark? AbsenceMark { get; private set; }

/// <summary>US-059: the run instant that set <see cref="AbsenceMark"/>; null exactly when the mark is null.</summary>
public DateTimeOffset? AbsentSince { get; private set; }

/// <summary>US-059 FR-003: the run instant of the latest complete, non-empty list that yielded the course; null until one.</summary>
public DateTimeOffset? LastListedAt { get; private set; }

/// <summary>
/// FR-001.2, FR-006.5: marks the course absent from the list. Returns true when the mark is new; a course already
/// carrying either mark keeps its kind and instant and the call returns false. No Google-sourced field changes.
/// An undefined <paramref name="mark"/> is refused (ArgumentOutOfRangeException).
/// </summary>
public bool MarkAbsent(CourseAbsenceMark mark, DateTimeOffset noticedAt);

/// <summary>
/// FR-003.1, FR-004.1: the course was on the list — records <paramref name="listedAt"/> as its last sighting and
/// removes any mark. Returns true when a mark was removed (for the FR-007 count).
/// </summary>
public bool RecordListed(DateTimeOffset listedAt);
```

- `Import` and `UpdateFrom` — unchanged; they never touch the three properties (the mark is
  tied to the list, not to a read — spec I-7).
- The class remark "There is deliberately no `MarkMissing`" is superseded by v100; IMPLEMENTATION
  rewrites it (the course is still never deleted by synchronization).
- Configuration (`CourseConfiguration`): `AbsenceMark` with the converter and `HasMaxLength(40)`,
  `AbsentSince`, `LastListedAt` nullable; check constraint `ck_course_absence_mark` exactly as
  db-design §2 (added to the existing `ToTable` builder beside `ck_course_course_state`).

## 3. `CourseAbsenceRule` (Domain/Rules, new)

Business concept: BR-049 — which mark a newly absent course gets. Pure, no clock (the
`IncrementalSyncRule` pattern).

```csharp
namespace ClassroomAgent.Domain.Rules;

/// <summary>US-059 FR-006.1…FR-006.3, I-1: the mark for a stored course the complete list did not yield.</summary>
public static class CourseAbsenceRule
{
    /// <summary>
    /// NotVisibleToTechnicalAccount when a change is recorded and the course was last listed strictly before it, or
    /// never listed (I-1); NotInClassroom otherwise, and always when no change is recorded.
    /// </summary>
    public static CourseAbsenceMark MarkFor(DateTimeOffset? lastListedAt, DateTimeOffset? technicalAccountChangedAt);
}
```

## 4. `SyncState` (Domain/Entities, existing) — the empty-list flag

```csharp
/// <summary>US-059 FR-005: true exactly when the latest list read to its end was empty while courses were stored.</summary>
public bool LastCourseListEmpty { get; private set; }

/// <summary>
/// FR-005.2, FR-005.3: the course list was read to its end — <paramref name="emptyWhileCoursesStored"/> sets or clears
/// the flag. Never called by a run that stopped before the end (FR-005.4); BeginRun, CompleteRun and FailRun leave it.
/// </summary>
public void RecordCourseListRead(bool emptyWhileCoursesStored);
```

Configuration (`SyncStateConfiguration`): `builder.Property(s => s.LastCourseListEmpty).IsRequired().HasDefaultValue(false);`

## 5. `WorkspaceConnection` (Domain/Entities, existing) — the account change

```csharp
/// <summary>US-059 FR-002: the instant of the last save that changed the technical account; null until one.</summary>
public DateTimeOffset? TechnicalAccountChangedAt { get; private set; }

/// <summary>
/// A change of the stored connection; the record keeps its identity (US-009 FR-006). When the normalised email differs
/// from the stored one (ordinal, VR-002), <paramref name="changedAt"/> becomes <see cref="TechnicalAccountChangedAt"/>;
/// the same email in other letter case or with spaces leaves it unchanged.
/// </summary>
public void ChangeTo(string domain, string impersonationUserEmail, DateTimeOffset changedAt);
```

- Replaces `ChangeTo(string, string)` — one call site, `SaveWorkspaceConnectionUseCase.StoreAsync`,
  which passes its existing `now`. `Create` is unchanged and records nothing (FR-002.2).
- Configuration: `builder.Property(c => c.TechnicalAccountChangedAt);`.

## 6. Ports

`ICourseRepository` (Application/Ports, existing) gains:

```csharp
/// <summary>
/// US-059 entity model §6: every stored course, tracked for a change — read once per run at the end of a complete
/// course list (a few hundred rows per school).
/// </summary>
Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken);
```

`IWorkspaceConnectionRepository` is unchanged: the run reads the change instant through the existing
`GetForReadAsync` (untracked) at the end of the list (db-design §6 R2).
`RunSynchronizationUseCase` gains a constructor dependency on `IWorkspaceConnectionRepository`.

## 7. Use-case touch points (for TEST_WRITING orientation, not a design of the code)

| Spec | `RunSynchronizationUseCase` |
|---|---|
| db-design §6 R1 | `observedAt := timeProvider.GetUtcNow()` before `connectionQuery.ExecuteAsync`; passed into `ImportCoursesAsync` |
| FR-005 | after the `await foreach` ends: `stored := courses.GetAllAsync`; `listed` empty and `stored` non-empty → `state.RecordCourseListRead(true)`, nothing else (no mark, no sighting, no skip-record removal) |
| FR-003, FR-004 | otherwise, each stored course in `listed` → `RecordListed(observedAt)` (count `true` as unmarked) |
| FR-001, FR-006 | each stored course not in `listed` and `AbsenceMark is null` → `MarkAbsent(CourseAbsenceRule.MarkFor(course.LastListedAt, connection?.TechnicalAccountChangedAt), observedAt)` with `connection` from `GetForReadAsync` (R2); count per kind |
| FR-005.3 | then `state.RecordCourseListRead(false)` |
| US-058 FR-007.3 | the existing removal of unlisted skip records, held back on an empty list |
| — | one `SaveChangesAsync` for all of the above (db-design §5) |
| FR-007 | counts carried in `ImportProgress`; two log events in the host (ids chosen at IMPLEMENTATION) |
| FR-008 | `GoogleClassroomReader.Points`: a value `< 0` → `GoogleReadFailedException(ValueOutOfRange)`, as on overflow; the use case is unchanged |
| FR-002 | `SaveWorkspaceConnectionUseCase.StoreAsync`: `existing.ChangeTo(domain, email, now)` |

Matching a stored course with `listed` is ordinal on `Course.GoogleId` (VR-001); `listed` already
holds ids trimmed and cut as `Course.Import` stores them.

## 8. Mapping to business concepts and DTOs

| Element | Business concept | DTO |
|---|---|---|
| `Course.AbsenceMark`, `AbsentSince` | «нет в Classroom» / «не виден техническому аккаунту» + date (BR-048, BR-049) | none in this Story (Stories 2, 3) |
| `Course.LastListedAt` | "когда синхронизация последний раз видела курс в списке" (§3 `Course` v101) | none |
| `CourseAbsenceRule` | BR-049 choice of mark | — |
| `SyncState.LastCourseListEmpty` | technical problem "Google не вернул ни одного курса" (BR-047, BR-048) | none (Story 3) |
| `WorkspaceConnection.TechnicalAccountChangedAt` | "время последней смены технического аккаунта" (§3 `WorkspaceConnection` v101) | none — `WorkspaceConnectionView` unchanged |
