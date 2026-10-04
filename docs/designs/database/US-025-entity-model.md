---
artifact_type: entity_model
story: US-025
version: 1
status: DRAFT
created_at: 2026-10-04T10:20:00Z
updated_at: 2026-10-04T10:40:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-025-spec.md
    version: 1
  - path: docs/designs/api/US-025-api-design.md
    version: 1
  - path: docs/designs/api/US-025-openapi.yaml
    version: 1
  - path: docs/decisions/US-025-open-decisions.md
    version: 2
  - path: docs/designs/database/US-025-db-design.md
    version: 1
supersedes: null
---

# US-025 Entity Model — Journal view for a period

**No entity changes.** `Course`, `CourseWork`, `CourseMembership`,
`ClassroomParticipant` and `Submission` (US-014, US-015) are read as they are;
no property, enum member or configuration is added. The Story adds one read
port and its read records. Names are indicative where the Specification says
so; signatures are binding for TEST_WRITING.

## 1. Entities read (unchanged)

| Entity | Business concept | Used for |
|---|---|---|
| `Course` | a Classroom course (glossary) | drop-down option; existence of `courseId` |
| `CourseWork` | an item — graded work, ungraded work or material (BR-052) | a column |
| `CourseMembership` | a person's place in a course, with BR-051 first/last seen | a candidate row |
| `ClassroomParticipant` | a synced person, never an account | the row label |
| `Submission` | a student's submission of one item (BR-056 … BR-058) | a cell |

`CourseWork.Kind` (computed, not stored — PC-3) remains the only source of the
graded / ungraded / material distinction; the port computes it from the
materialised entity, never re-implements it.

## 2. New port (`Application/Ports`): `IJournalSource`

Reads only. Implemented in `Infrastructure/Persistence/Repositories` as
`JournalSource` with `ClassroomAgentDbContext`, every query `AsNoTracking()`
(db-design §2). It never calls `SaveChangesAsync`, never opens a transaction and
never touches a Google port.

```csharp
public interface IJournalSource
{
    /// Q1 — every stored course, unordered (db-design §2).
    Task<IReadOnlyList<JournalCourseRecord>> GetCoursesAsync(CancellationToken cancellationToken);

    /// Q2 — items of the course with start <= ItemDate < endExclusive; both bounds UTC (offset zero).
    Task<IReadOnlyList<JournalItemRecord>> GetItemsAsync(
        long courseId, DateTimeOffset start, DateTimeOffset endExclusive, CancellationToken cancellationToken);

    /// Q3 — every membership of the course with role Student, with its participant, unordered.
    Task<IReadOnlyList<JournalMemberRecord>> GetStudentMembersAsync(long courseId, CancellationToken cancellationToken);

    /// Q4 — every submission to an item of the course dated in [start, endExclusive), unordered, not deduplicated.
    Task<IReadOnlyList<JournalSubmissionRecord>> GetSubmissionsAsync(
        long courseId, DateTimeOffset start, DateTimeOffset endExclusive, CancellationToken cancellationToken);
}
```

The implementation throws `ArgumentException` when `start` or `endExclusive`
has a non-zero offset or `start >= endExclusive` — a programming error, not an
expected outcome (AD-9); the use case always passes valid UTC bounds.

## 3. Read records (`Application/Models`)

One public type per file. Positional `sealed record`s, like `RosterEntry`.

| Record | Fields | Source |
|---|---|---|
| `JournalCourseRecord` | `long Id`, `string Name`, `string? Section` | `course` |
| `JournalItemRecord` | `long Id`, `string Title`, `DateTimeOffset ItemDate`, `DateTimeOffset? DueAt`, `decimal? MaxPoints`, `CourseWorkKind Kind` | `course_work`; `Kind` from `CourseWork.Kind` |
| `JournalMemberRecord` | `long ParticipantId`, `DateTimeOffset FirstSeenAt`, `DateTimeOffset LastSeenAt`, `bool OnRoster`, `string? FullName`, `string? Email` | `course_membership` ⋈ `classroom_participant` |
| `JournalSubmissionRecord` | `long Id`, `long ItemId`, `long ParticipantId`, `DateTimeOffset? UpdateTime`, `SubmissionState State`, `string? RawState`, `decimal? AssignedGrade`, `decimal? DraftGrade`, `DateTimeOffset? TurnedInAt`, `bool Late` | `submission` |

Rules:

- No entity escapes the port: the implementation projects or maps to these
  records before returning (AD-8 applies one layer later, but no tracked or
  untracked entity reaches the use case either).
- `JournalItemRecord.MaxPoints` is `null` for ungraded work and materials
  (the `ck_course_work_material_has_no_grading` constraint guarantees it for
  materials).
- `JournalSubmissionRecord.RawState` is non-null exactly when `State` is
  `Unrecognised` (`ck_submission_raw_state`).
- `Id` and `UpdateTime` exist only to choose among duplicates (OD-008 a); neither
  reaches the page DTO.
- No Google identifier and no `created_at` / `updated_at` is carried.

## 4. Use case (`Application/UseCases`): `GetJournalQuery`

Not a persistence artifact; listed for the mapping only. Depends on
`IJournalSource`, `TimeProvider` and the school time zone setting (spec FR-010).
It does **not** depend on `IUnitOfWork`, `IReadOnlyModeGuard` or any Google port
(spec FR-013) — the constructor signature is itself evidence for S-04.

It applies, in memory: the FR-005 row rule over `JournalMemberRecord` and
`JournalSubmissionRecord`; the FR-006 cell function; the FR-002 / FR-004 / FR-005
orders with a culture-aware comparer; the OD-008 (a) choice of one submission
per student and item (latest `UpdateTime`, absent is oldest, then larger `Id`) of the UI language; the conversion of
`ItemDate` and `TurnedInAt` to calendar dates in the school's time zone.

## 5. Mapping to business concepts and DTOs

| DTO (openapi) | Field | From |
|---|---|---|
| `CourseOption` | `id`, `name`, `section` | `JournalCourseRecord` |
| `JournalPageModel` | `noCoursesStored` | Q1 empty |
| `JournalPageModel` | `messageKeys: CourseUnknown` | `courseId` not in Q1 |
| `JournalColumn` | `title` | `JournalItemRecord.Title` |
| `JournalColumn` | `date` | `ItemDate` → school time zone → date |
| `JournalColumn` | `kind` | `JournalItemRecord.Kind` |
| `JournalColumn` | `maxPoints` | `JournalItemRecord.MaxPoints` (graded only) |
| `JournalRow` | `displayName`, `nameKind` | `FullName` → `FullName`; else `Email` → `Email`; else `Unnamed` |
| `JournalRow` | order, tie-break | display value, then `ParticipantId` (not emitted — api-design §2.5) |
| `JournalCell` | `state` | FR-006 over `State`, `AssignedGrade`, `DueAt`, `Kind`, instant B |
| `JournalCell` | `points`, `maxPoints` | `AssignedGrade`, `MaxPoints` |
| `JournalCell` | `rawState` | `RawState` |
| `JournalCell` | `late` | `Late` |
| `JournalCell` | `draftPoints` | `DraftGrade` (full view, graded, state shown) |
| `JournalCell` | `turnedInOn` | `TurnedInAt` → school time zone → date (full view) |

When Q4 returns two submissions for one student and item, the cell is fed by the
one OD-008 (a) chooses; FR-005 condition 2 is met by any of them.

## 6. Registration

`JournalSource` is registered as `IJournalSource` with a scoped lifetime beside
the existing repositories (the Infrastructure DI extension). No other
registration changes.
