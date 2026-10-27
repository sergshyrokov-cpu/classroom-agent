---
artifact_type: entity_model
story: US-062
version: 1
status: DRAFT
created_at: 2026-10-27T19:19:41Z
updated_at: 2026-10-27T19:19:41Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-062-spec.md
    version: 1
  - path: docs/designs/api/US-062-api-design.md
    version: 1
  - path: docs/designs/api/US-062-openapi.yaml
    version: 1
  - path: docs/designs/database/US-062-db-design.md
    version: 1
supersedes: null
---

# US-062 Entity Model

Signatures are binding in name and shape; the skeleton TEST_WRITING builds from them is the compile
check (standing rule). Placement follows `package-map.md`.

## 1. Domain

### 1.1 `SyncState` (changed)

```csharp
/// US-062 FR-008: the start of the first run of the current streak of empty course lists; null exactly
/// when LastCourseListEmpty is false.
public DateTimeOffset? CourseListEmptySince { get; private set; }

// Signature unchanged; behaviour extended:
// true  → LastCourseListEmpty = true;  CourseListEmptySince ??= StartedAt;
// false → LastCourseListEmpty = false; CourseListEmptySince = null.
public void RecordCourseListRead(bool emptyWhileCoursesStored);
```

Mapping: `course_list_empty_since timestamptz NULL` (db-design §2).

### 1.2 `Course` (new operation)

```csharp
/// US-062 FR-006.2 step 4, BR-049: the Admin confirmed the course was deleted in Classroom. Only a
/// course marked NotVisibleToTechnicalAccount changes: mark := NotInClassroom, AbsentSince :=
/// confirmedAt; returns true. Any other state is left as it is and returns false.
public bool ConfirmDeletedInClassroom(DateTimeOffset confirmedAt);
```

`MarkAbsent` and `RecordListed` unchanged; synchronization then treats the course as BR-048 (FR-006.3).

### 1.3 `AuditAction` (new value)

```csharp
/// US-062 FR-006.2 step 5 (SC-11, v101): an Admin confirmed that courses not visible to the technical
/// account were deleted in Classroom.
CoursesConfirmedDeleted,
```

Code `courses_confirmed_deleted`.

### 1.4 `AuditEvent` (new column, two factories)

```csharp
/// US-062 db-design §3: the internal ids of the confirmed courses, ascending and distinct; only on a
/// succeeded CoursesConfirmedDeleted row.
public long[]? ConfirmedCourseIds { get; private set; }

/// Succeeded row: actor the Admin (role Admin), no target, the ids sorted and de-duplicated; throws
/// ArgumentOutOfRangeException for an empty set, more than 1000 ids or a non-positive id.
public static AuditEvent CoursesConfirmedDeleted(
    long adminId, IReadOnlyCollection<long> courseIds, DateTimeOffset occurredAt, string? requestId);

/// Refused in read-only mode (API design §2.5): actor the Admin, no target, no ids, category
/// ReadOnlyMode only.
public static AuditEvent CoursesConfirmedDeletedRefused(
    long adminId, AuditRefusalCategory category, DateTimeOffset occurredAt, string? requestId);
```

Mapping: `confirmed_course_ids bigint[] NULL`; CHECKs in db-design §3.2.

## 2. Application

### 2.1 Ports

```csharp
// Application/Ports/IDataHealthSource.cs — reads only (db-design §5)
public interface IDataHealthSource
{
    Task<IReadOnlyList<CourseHealthFact>> GetCourseFactsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AboveMaximumGradeRow>> GetAboveMaximumGradesAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<ItemNotInClassroomRow>> GetItemsNotInClassroomAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken);
}

// Application/Ports/ICourseRepository.cs — added
/// Row-locked read (FOR UPDATE) of the given courses; call only inside ExecuteInTransactionAsync
/// (db-design §4). Unknown ids are simply absent from the result.
Task<IReadOnlyList<Course>> GetForAbsenceConfirmationAsync(
    IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken);
```

Implementations: `Infrastructure/Persistence/DataHealthSource.cs`, `CourseRepository`.

### 2.2 Read models (`Application/Models`)

```csharp
public sealed record CourseHealthFact(
    long CourseId, string GoogleId, string Name, CourseState State,
    CourseAbsenceMark? AbsenceMark, DateTimeOffset? AbsentSince, DateTimeOffset? LastCompleteReadAt,
    bool HasGradeAboveMaximum, bool HasItemNotInClassroom);

public sealed record AboveMaximumGradeRow(
    long CourseId, string ItemTitle, string StudentName, decimal Points, decimal Maximum, bool IsDraft);

public sealed record ItemNotInClassroomRow(long CourseId, string Title, DateTimeOffset Since);
```

### 2.3 Request model (`Application/Models/Requests`)

```csharp
/// API design §2.7: the repeated form field, bound as strings and parsed in Application (VR-001).
public sealed class CourseDeletionConfirmationForm
{
    public IReadOnlyList<string>? CourseIds { get; init; }
}
```

### 2.4 Use cases and queries (`Application/UseCases`)

```csharp
public sealed class GetDataHealthCountsQuery   // banner
{
    Task<DataHealthBannerModel?> ExecuteAsync(AppRole role, CancellationToken cancellationToken);
}

public sealed class GetDataHealthQuery         // page; role-shaped (API design §2.8)
{
    Task<DataHealthPageModel> ExecuteAsync(AppRole role, CancellationToken cancellationToken);
}

public sealed class ConfirmCoursesDeletedUseCase
{
    Task<CourseDeletionConfirmationResult> ExecuteAsync(
        long adminId, CourseDeletionConfirmationForm form, string? requestId, CancellationToken cancellationToken);
}

public enum CourseDeletionConfirmationOutcome { Confirmed, Invalid, ListChanged }
public sealed record CourseDeletionConfirmationResult(CourseDeletionConfirmationOutcome Outcome, int ConfirmedCount);
```

`ConfirmCoursesDeletedUseCase` order: read-only guard (refused audit row, rethrow
`ReadOnlyModeException` → 409) → parse VR-001 (`Invalid`) → transaction {lock, check (`ListChanged`),
`ConfirmDeletedInClassroom`, `AuditEvent.CoursesConfirmedDeleted`} → `Confirmed`.

Both queries compute the problems from the same facts through one shared internal assessor (the
FR-001 rules, counting of FR-001.5); the banner query returns null when both counts are zero.

### 2.5 View-model DTOs (`Application/Models`)

The OpenAPI schemas `DataHealthBannerModel`, `DataHealthPageModel`, `ReadOnlyNotice`,
`TechnicalProblem`, `TechnicalDetails`, `NotVisibleCourse`, `CourseLabel`, `CourseProblems`,
`SkippedCourse`, `ItemNotInClassroom`, `AboveMaximumGrade` become sealed records of the same names
and members (enum members as C# enums: `TechnicalProblemKind`, `SkippedCourseVariant`,
`DataHealthMessage`). No domain entity appears in them (AD-8).

## 3. Entity → business concept → DTO

| Stored | Concept | DTO member |
|---|---|---|
| `SyncState.Status = Failed`, `LastError`, `FailedStep`, `FinishedAt` | X1 | `TechnicalProblem{LastRunFailed}.details` |
| `SyncState.LastSuccessfulRunAt` | X2 | `NoRecentSuccess` / `NoSuccessYet`, `details.lastSuccessAt` |
| `SyncState.CourseListEmptySince` | X4 | `CourseListEmpty.emptySince` |
| `Course.AbsenceMark = NotVisibleToTechnicalAccount` | X5 | `CoursesNotVisible.details.notVisibleCourses` |
| `LegitimacyState.LastSuccessfulCheckAt`, `Compatibility` | X6 | `ControlPlaneUnreachable.lastCheckAt/readOnlyFrom`, `details.upgradeRequired` |
| `SyncSkippedCourse(UnknownCourseState)` | X3 | `UnknownCourseState.details.course/skippedSince`; archived: `CourseProblems.unknownState` |
| `SyncSkippedCourse(ValueOutOfRange)` + `Course.LastCompleteReadAt` | T3 | `CourseProblems.skipped` |
| `Course.AbsenceMark = NotInClassroom`, `AbsentSince` | T4 | `CourseProblems.notInClassroomSince` |
| `CourseWork.NotInClassroomSince` | T2 | `CourseProblems.itemsNotInClassroom` |
| `Submission.AssignedGrade/DraftGrade` vs `CourseWork.MaxPoints` | T1 | `CourseProblems.aboveMaximum` |
| `Course.State = Archived` | archived list | `DataHealthPageModel.archived` |
| `AuditEvent.ConfirmedCourseIds` | confirmation audit | — (not shown) |
