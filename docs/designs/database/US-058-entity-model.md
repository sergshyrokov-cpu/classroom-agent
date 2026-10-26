---
artifact_type: entity_model
story: US-058
version: 1
status: DRAFT
created_at: 2026-10-26T19:54:01Z
updated_at: 2026-10-26T19:54:01Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-058-spec.md
    version: 1
  - path: docs/designs/database/US-058-db-design.md
    version: 1
supersedes: null
---

# US-058 Entity Model

Signatures below are the contract TEST_WRITING compiles its skeleton from (standing rule); a
signature that does not compile loops back here. No API DTO is involved (API_DESIGN
NOT_APPLICABLE); nothing of this Story reaches a controller or view.

## 1. `CourseWork` (Domain/Entities, existing) — the mark

Business concept: §3 `CourseWork` "пометка «нет в Classroom» и дата" (BR-045).

```csharp
/// <summary>Null while the item is in Classroom; otherwise the run instant that first found it absent.</summary>
public DateTimeOffset? NotInClassroomSince { get; private set; }

/// <summary>
/// FR-001, FR-002: marks the item absent from a full read. Returns true when the mark is new; an item
/// already marked keeps its first instant and the call returns false. No Google-sourced field changes.
/// </summary>
public bool MarkNotInClassroom(DateTimeOffset noticedAt);
```

- `UpdateFrom(CourseWorkDetails details)` — unchanged signature; **also clears
  `NotInClassroomSince`** (FR-003): an item Google returned is in Classroom by definition, so
  the invariant lives in the entity, not in the use case. The use case reads
  `NotInClassroomSince` before calling it to count the unmarked items (FR-004).
- `Import(...)` — unchanged; a new item is never marked.
- Configuration: `builder.Property(c => c.NotInClassroomSince);` (nullable `timestamptz`).

## 2. `Course` (Domain/Entities, existing) — the last successful read

Business concept: "дата последнего успешного чтения курса" (§4 Epic 1, BR-046, OD-001 a).

```csharp
/// <summary>FR-008: the run instant of the course's last committed full read; null until the first one.</summary>
public DateTimeOffset? LastCompleteReadAt { get; private set; }

/// <summary>
/// US-018 entity model §2 + US-058 FR-008: the course has just been read completely in this transaction —
/// remembers the listed UpdateTime (as before) and the run instant of this read. The only writer of both.
/// </summary>
public void RecordCompleteRead(DateTimeOffset readAt);
```

- Replaces the parameterless `RecordCompleteRead()` (one call site in
  `RunSynchronizationUseCase`; two test files adapted at TEST_WRITING). One call keeps the two
  complete-read facts from ever disagreeing.
- Configuration: `builder.Property(c => c.LastCompleteReadAt);`.

## 3. `SyncSkippedCourse` (Domain/Entities, new) and `CourseSkipReason` (Domain/Enums, new)

Business concept: §3 `SyncState` "курсы, которые синхронизация пропускает… номер курса в
Google, название из списка курсов Google, причина пропуска и с какого времени" (BR-046).

```csharp
namespace ClassroomAgent.Domain.Enums;

/// <summary>Why synchronization skipped a course (US-058 VR-003). Stored as a lower-case code.</summary>
public enum CourseSkipReason
{
    ValueOutOfRange,
    UnknownCourseState,
}
```

```csharp
namespace ClassroomAgent.Domain.Entities;

public sealed class SyncSkippedCourse
{
    public long Id { get; private set; }

    /// <summary>Google's course id from the course list; the natural key (unique).</summary>
    public string CourseGoogleId { get; private set; }

    /// <summary>Google's list name, trimmed and cut to Course.MaxNameLength; null when missing or blank.</summary>
    public string? CourseName { get; private set; }

    public CourseSkipReason Reason { get; private set; }

    /// <summary>The run instant that began the current streak of skips; never moved by a repeated skip.</summary>
    public DateTimeOffset SkippedSince { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>FR-005: the first skip of a streak. A blank Google id is refused (ArgumentException).</summary>
    public static SyncSkippedCourse Record(
        string courseGoogleId,
        string? courseName,
        CourseSkipReason reason,
        DateTimeOffset skippedAt);

    /// <summary>FR-006, I-3: a repeated skip — the name and the reason follow this run, the instant stays.</summary>
    public void SkipAgain(string? courseName, CourseSkipReason reason);
}
```

- Cutting follows `Course`: `CourseGoogleId` trimmed and cut to `Course.MaxGoogleIdLength`;
  `CourseName` trimmed, cut to `Course.MaxNameLength`, blank → null. An undefined enum value
  is refused (`ArgumentOutOfRangeException`), as `SyncState.FailRun` does.
- Configuration `SyncSkippedCourseConfiguration` (Infrastructure/Persistence/Configurations):
  table, check constraints and unique index exactly as db-design §4; `Reason` through a value
  converter to `value_out_of_range` / `unknown_course_state` (the `SyncStateConfiguration`
  pattern), `HasMaxLength(32)`. `DbSet<SyncSkippedCourse> SyncSkippedCourses` on
  `ClassroomAgentDbContext`.

## 4. Port `ISyncSkippedCourseRepository` (Application/Ports, new)

```csharp
/// <summary>
/// The installation's sync_skipped_course table (US-058 entity model §4). Stages changes; never saves —
/// the use case owns the transaction boundary through IUnitOfWork (AD-7).
/// </summary>
public interface ISyncSkippedCourseRepository
{
    /// <summary>Every record, tracked for a change — read once per run (the table holds a handful of rows).</summary>
    Task<IReadOnlyList<SyncSkippedCourse>> GetAllAsync(CancellationToken cancellationToken);

    void Add(SyncSkippedCourse skippedCourse);

    void Remove(SyncSkippedCourse skippedCourse);
}
```

Implemented in `Infrastructure/Persistence/Repositories/SyncSkippedCourseRepository.cs`,
registered with the other repositories.

## 5. Use-case touch points (for TEST_WRITING orientation, not a design of the code)

| Spec | `RunSynchronizationUseCase` |
|---|---|
| FR-001…FR-003 | `ImportCourseWorkAsync`: stored items whose key is not in the page → `MarkNotInClassroom(observedAt)`; returned items → `UpdateFrom` (clears) |
| FR-004 | counts carried in `ImportProgress` from committed courses; one log event in the host |
| FR-005, FR-006 | unknown-state and out-of-range branches of `ImportCoursesAsync` → `Record` / `SkipAgain`, then `SaveChangesAsync` |
| FR-007.1, FR-008 | `ImportCourseAsync` after the items: `RecordCompleteRead(observedAt)`, `Remove(record)` |
| FR-007.2 | `ImportCourseAsync` on `CourseResult.AgedOut`: `Remove(record)` |
| FR-007.3 | after the `await foreach` ends normally: remove records whose id was not listed, save |

## 6. Mapping to business concepts and DTOs

| Element | Business concept | DTO |
|---|---|---|
| `CourseWork.NotInClassroomSince` | «нет в Classroom» + date (BR-045) | none in this Story (Story 2) |
| `Course.LastCompleteReadAt` | «данные не обновлялись с <даты>» (BR-046) | none (Story 2) |
| `SyncSkippedCourse` | skipped course in `SyncState` (BR-046) | none (Story 3) |
| `CourseSkipReason` | teaching vs technical notice kind (BR-047) | none (Story 3 translates it) |
