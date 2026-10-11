---
artifact_type: entity_model
story: US-020
version: 1
status: DRAFT
created_at: 2026-10-11T14:40:00Z
updated_at: 2026-10-11T14:40:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-020-spec.md
    version: 1
  - path: docs/designs/api/US-020-openapi.yaml
    version: 1
  - path: docs/designs/database/US-020-db-design.md
    version: 1
supersedes: null
---

# US-020 Entity Model — Course list with filters and search

## 1. Entities

**No entity is added or changed.** The list reads three US-014 entities as
stored (`trebovaniya.md` §3, v83 "a mirror nobody edits"):

| Entity | Attributes read | Business concept |
|---|---|---|
| `Course` | `Id`, `Name`, `Section`, `State`, `UpdateTime` | a Classroom course (§3) |
| `CourseMembership` | `CourseId`, `ParticipantId`, `Role`, `OnRoster` | a person's role in a course; current = `OnRoster` (OD-003) |
| `ClassroomParticipant` | `Id`, `Surname`, `GivenName`, `Email` | a teacher as synced data, never an account |

`FullName`, `AlternateLink`, `OwnerGoogleId` and the remaining columns are not
read (spec FR-003, FR-007, OD-008). The only persistence additions are the
collation and partial index of db-design §2 and §4.1.

## 2. Read port and read models (`Application`)

Signatures compiled in a scratch project (nullable, warnings as errors):
0 errors. One public type per file.

`Application/Models`:

```csharp
public sealed record CourseListFilter(CourseState? State, long? TeacherParticipantId, string? SearchText);

public sealed record CourseListPersonRecord(long ParticipantId, string? Surname, string? GivenName, string? Email);

public sealed record CourseListCourseRecord(
    long CourseId, string Name, string? Section, CourseState State, DateTimeOffset? UpdateTime);

public sealed record CourseListTeacherRecord(long CourseId, CourseListPersonRecord Teacher);

public sealed record CourseListStudentCountRecord(long CourseId, int StudentCount);
```

- `CourseListFilter.State` null means `all`; `SearchText` is the trimmed,
  validated text or null. Built only by `GetCourseListQuery` after validation.
- `CourseState` is `ClassroomAgent.Domain.Enums.CourseState` — a Domain enum, so
  `Application` may use it (AD-3). No entity type appears in a record.

`Application/Ports/ICourseListSource.cs`:

```csharp
public interface ICourseListSource
{
    /// Q1 — participants with a current-teacher membership in any course, ordered by label key, then id.
    Task<IReadOnlyList<CourseListPersonRecord>> GetTeacherChoiceAsync(CancellationToken cancellationToken);

    /// Q2 — number of courses matching the filter.
    Task<int> CountCoursesAsync(CourseListFilter filter, CancellationToken cancellationToken);

    /// Q3 — whether any course is stored.
    Task<bool> AnyCourseStoredAsync(CancellationToken cancellationToken);

    /// Q4 — the courses matching the filter in list order, skipping offset, at most limit.
    Task<IReadOnlyList<CourseListCourseRecord>> GetCoursePageAsync(
        CourseListFilter filter, int offset, int limit, CancellationToken cancellationToken);

    /// Q5 — current teachers of the given courses, ordered by course, label key, participant id.
    Task<IReadOnlyList<CourseListTeacherRecord>> GetCurrentTeachersAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken);

    /// Q6 — current-student count of each given course that has at least one; a missing course means 0.
    Task<IReadOnlyList<CourseListStudentCountRecord>> GetStudentCountsAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken cancellationToken);
}
```

Implemented in `Infrastructure/Persistence/CourseListSource.cs` with EF Core,
`AsNoTracking()`, no `SaveChanges`, no Google port. `IJournalSource` is not
changed (spec FR-016). Ordering lives in the port (db-design §3.3, §3.4) because
paging must happen in the database; `Application` keeps the received order.

## 3. Mapping to the API view model

| View DTO (`US-020-openapi.yaml`) | Source |
|---|---|
| `CourseListPageModel.teacherChoice[]` | Q1 records, in order |
| `TeacherOption.participantId` | `CourseListPersonRecord.ParticipantId` |
| `TeacherOption.name` | `PersonName` rule (§4) on the record |
| `TeacherOption.disambiguatingEmail` | record `Email` when another option renders the same label (spec I-3) |
| `CourseListResult.totalCount` | Q2 |
| `CourseListResult.emptyState` | Q2 = 0 → Q3 true → `NoMatch`; Q3 false → `NoCoursesStored` |
| `CourseListResult.page`, `pageCount` | computed in `Application` from Q2 and the requested page |
| `CourseListRow.courseId`, `name`, `section` | `CourseListCourseRecord` |
| `CourseListRow.state` | `CourseState` → enum member name |
| `CourseListRow.teachers[]` | Q5 records of that course, in order, through `PersonName` |
| `CourseListRow.studentCount` | Q6, or 0 when absent |
| `CourseListRow.updatedAt` | `UpdateTime` converted by `SchoolTimeZone` to local `yyyy-MM-ddTHH:mm`; null stays null (spec I-8) |

## 4. The `PersonName` rule

In `Application` (one place, used for rows and choice):

1. `Surname` and `GivenName`, each counted present when not null and not empty;
   both present → `"{Surname} {GivenName}"`; one → that one; kind
   `ProfileName`.
2. Otherwise `Email` when not null and not empty → kind `Email`, full address.
3. Otherwise kind `NoName`, text null.

Values are used exactly as stored — not trimmed, re-cased or translated
(spec FR-007, NFR-073). This is the same rule as the SQL label key of db-design
§3.4, which is what keeps database order and rendered labels consistent.

## 5. Business-concept traceability

| Concept (spec) | Model element |
|---|---|
| Course row (FR-003) | `CourseListCourseRecord` + Q5 + Q6 |
| Status filter (FR-004) | `CourseListFilter.State` → `course.course_state` |
| Current teacher (OD-003, FR-005) | `role = 'teacher' AND on_roster` — `ix_course_membership_current_teacher` |
| Current student count (FR-003) | Q6, `role = 'student' AND on_roster` |
| Search (FR-006) | `CourseListFilter.SearchText`, db-design §3.2 |
| Ukrainian-aware order (FR-008, I-7) | collation `course_name_uk` |
| Teacher name (FR-007) | `CourseListPersonRecord` + `PersonName` |
| Bounded reading (FR-016, AC-020) | Q1–Q6, one call each |
