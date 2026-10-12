---
artifact_type: entity_model
story: US-022
version: 1
status: DRAFT
created_at: 2026-10-12T09:10:48Z
updated_at: 2026-10-12T09:10:48Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-022-spec.md
    version: 1
  - path: docs/designs/api/US-022-openapi.yaml
    version: 1
  - path: docs/designs/database/US-022-db-design.md
    version: 1
supersedes: null
---

# US-022 Entity Model — Teachers and their courses

The signatures below were compiled in a scratch project referencing
`ClassroomAgent.Infrastructure` (nullable enabled, warnings as errors: 0
warnings, 0 errors). Names are binding unless marked indicative.

## 1. Domain entities

**No change.** The Story reads existing entities only:

| Entity | Used for | Business concept |
|---|---|---|
| `ClassroomParticipant` | `Id`, `Surname`, `GivenName`, `Email` | a person synced from Google (teacher here) |
| `CourseMembership` | `ParticipantId`, `CourseId`, `Role`, `OnRoster` | "currently teaches this course" (spec FR-002) |
| `Course` | `State` | the status filter (spec FR-005) |

No entity crosses into `Web` or into a DTO (AD-8).

## 2. Application port and record

`Application/Ports/ITeacherListSource.cs` — a new port, separate from
`ICourseListSource` (which stays unchanged): the teacher list has its own two
queries and nothing in common with the course list's six except the label key,
which is shared inside `Infrastructure` (db-design §3.3).

```csharp
public interface ITeacherListSource
{
    /// T1 — number of participants with at least one current teacher membership in a course of the state (null = all).
    Task<int> CountTeachersAsync(CourseState? state, CancellationToken cancellationToken);

    /// T2 — those participants with their course counts, in label-key order, skipping offset, at most limit.
    Task<IReadOnlyList<TeacherListRecord>> GetTeacherPageAsync(
        CourseState? state, int offset, int limit, CancellationToken cancellationToken);
}
```

`Application/Models/TeacherListRecord.cs`:

```csharp
public sealed record TeacherListRecord(
    long ParticipantId, string? Surname, string? GivenName, string? Email, int CourseCount);
```

`CourseState? state` null means `all`, as `CourseListFilter.State` does.

Implementation: `Infrastructure/Persistence/TeacherListSource.cs`
(`sealed class TeacherListSource(ClassroomAgentDbContext db) : ITeacherListSource`),
registered scoped beside `CourseListSource` in `InstallationServices`.

## 3. Request, query and outcome

| Type | Location | Shape |
|---|---|---|
| `TeacherListRequest` | `Models/Requests` | `record (IReadOnlyList<string?> Status, IReadOnlyList<string?> Page)` — every occurrence as received |
| `GetTeacherListQuery` | `UseCases` | `sealed class (ITeacherListSource source)`; `Task<TeacherListPageResult> ExecuteAsync(TeacherListRequest request, CancellationToken cancellationToken)`; `const int PageSize = 50` |
| `TeacherListPageResult` | `Models` | `record (TeacherListPageOutcome Outcome, TeacherListPageModel Page)` |
| `TeacherListPageOutcome` | `Models` | `enum { Shown, Invalid }` — 200, 400 |
| `TeacherListUrl` | `UseCases` | `static class`; `const string Path = "/workspace/teachers"`; `static string Build(CourseStatusFilter status, int page)` — `status`, `page`, each omitted at its default |

`status` and `page` are parsed by the same rules as `GetCourseListQuery`
(US-020 VR-001, VR-004). Extracting its private `ReadStatus` / `ReadPage` into a
shared internal helper is in scope as a supporting change, provided the course
list's behaviour and tests are unchanged.

The query never calls `IReadOnlyModeGuard` or any Google port (spec FR-013).

## 4. DTOs (`Models/Dtos`) — openapi mapping

| DTO | Openapi schema | Shape |
|---|---|---|
| `TeacherListPageModel` | `TeacherListPageModel` | `record (CourseStatusFilter Status, IReadOnlyList<TeacherListMessageKey> MessageKeys, TeacherListResult? List, string ReturnPath)` |
| `TeacherListResult` | `TeacherListResult` | `record (bool IsEmpty, int TotalCount, int Page, int PageCount, int PageSize, IReadOnlyList<TeacherListRow> Rows)` |
| `TeacherListRow` | `TeacherListRow` | `record (long ParticipantId, CourseListPersonName Name, string? Email, int CourseCount, string CoursesUrl)` |
| `TeacherListMessageKey` | `TeacherListMessageKey` | `enum { StatusMalformed, PageMalformed }` |
| `CourseStatusFilter` | `CourseStatusFilter` | existing, unchanged |
| `CourseListPersonName` | `PersonName` | existing, unchanged |

Mapping, all in `GetTeacherListQuery`:

- `Name` — `CoursePersonNameRule.NameOf(Surname, GivenName, Email)` (existing).
- `Email` — the record's email, `null` when null or empty.
- `CoursesUrl` — `CourseListUrl.Build(status, ParticipantId, null, 1)`
  (existing).
- `IsEmpty` — `TotalCount == 0`; then `Rows` is empty, `Page` and `PageCount`
  are 1.
- `ReturnPath` — `TeacherListUrl.Build(effective status, page shown)`; on
  `Invalid`, `TeacherListUrl.Build(effective status, 1)`.

## 5. Host (`Web`)

- `SignInRoutes.Teachers = "/workspace/teachers"` (equal to `TeacherListUrl.Path`).
- A controller action under `[Authorize(Policy = InstallationPolicies.ViewCourses)]`
  collects `Request.Query["status"]` and `Request.Query["page"]`, calls the
  query, sets `400` on `Invalid`, logs per spec FR-016, renders the view. Which
  controller holds it is an implementation choice.
- Home page: one link between "Courses" and "Journal".
- No `DbContext` in `Web` (AD-3).

## 6. Persistence

None — db-design §1. No configuration, migration or snapshot change.
