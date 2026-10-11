---
artifact_type: entity_model
story: US-021
version: 1
status: DRAFT
created_at: 2026-10-11T16:12:00Z
updated_at: 2026-10-11T16:12:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-021-spec.md
    version: 1
  - path: docs/designs/api/US-021-openapi.yaml
    version: 1
  - path: docs/designs/database/US-021-db-design.md
    version: 1
supersedes: null
---

# US-021 Entity Model — Course detail with roster

Companion: `docs/designs/database/US-021-db-design.md`. All type names are
binding for TEST_WRITING and IMPLEMENTATION unless marked indicative.

## 1. Entities

**No entity changes.** `Course`, `CourseMembership`, `ClassroomParticipant` and
`ClassroomRole` are read as they are. No property, constraint or configuration
is added.

| Business concept (spec) | Stored as |
|---|---|
| Course details (FR-002) | `Course.Name`, `Section`, `State`, `Room`, `DescriptionHeading`, `Description`, `CreationTime`, `UpdateTime` |
| Current teacher / student (FR-003) | `CourseMembership` with `Role`, `OnRoster = true` |
| Leaver (FR-003, FR-006) | `CourseMembership` with `OnRoster = false`; `Role`; `LastSeenAt` |
| Owner (FR-004) | `ClassroomParticipant.GoogleUserId` = `Course.OwnerGoogleId` (compared in the database) |
| Person (FR-005) | `ClassroomParticipant.Surname`, `GivenName`, `Email` |

## 2. Port — `Application/Ports/ICourseCardSource.cs`

```csharp
public interface ICourseCardSource
{
    /// Q1 — the course's displayable details, or null when no course has this id.
    Task<CourseCardCourseRecord?> GetCourseAsync(long courseId, CancellationToken cancellationToken);

    /// Q2 — every membership of the course with its participant, in card order
    /// (db-design §2 Q2): named first, label under course_name_uk, email (none first), participant id.
    Task<IReadOnlyList<CourseCardMemberRecord>> GetMembersAsync(long courseId, CancellationToken cancellationToken);
}
```

Implemented by `Infrastructure/Persistence/CourseCardSource.cs`, registered
scoped beside `CourseListSource`. `DbContext` stays in `Infrastructure` (AD-3).

## 3. Records — `Application/Models/`

```csharp
/// Q1 — no Classroom link, teacher folder, calendar id, Google id or owner id (spec FR-002, S-10).
public sealed record CourseCardCourseRecord(
    long CourseId,
    string Name,
    string? Section,
    CourseState State,
    string? Room,
    string? DescriptionHeading,
    string? Description,
    DateTimeOffset? CreationTime,
    DateTimeOffset? UpdateTime);

/// Q2 — no Google id; IsOwner decided in the database (db-design §2).
public sealed record CourseCardMemberRecord(
    long ParticipantId,
    string? Surname,
    string? GivenName,
    string? Email,
    ClassroomRole Role,
    bool OnRoster,
    DateTimeOffset LastSeenAt,
    bool IsOwner);
```

## 4. The shared name rule

`GetCourseListQuery.NameOf` (private) becomes an `internal static` class
`Application/UseCases/CoursePersonNameRule.cs`:

```csharp
internal static class CoursePersonNameRule
{
    /// Spec US-020 FR-007 / US-021 FR-005: profile name, else the full email, else no name — as stored.
    public static CourseListPersonName NameOf(string? surname, string? givenName, string? email);
}
```

`GetCourseListQuery` calls it instead of its private method (behaviour
unchanged; the US-020 tests guard it) and `GetCourseCardQuery` calls it too.
This is a supporting change the Story needs: one rule, one implementation (api
design §2.6). The SQL label used for ordering (db-design §2) mirrors this rule.

## 5. Request, query, result and DTOs

```csharp
// Application/Models/Requests/CourseCardRequest.cs — the raw path value (api-design §2.3)
public sealed record CourseCardRequest(string? CourseId);

// Application/Models/CourseCardOutcome.cs — openapi CourseCardOutcome
public enum CourseCardOutcome { Shown, CourseMalformed, CourseUnknown }

// Application/Models/CourseCardResult.cs — Page is non-null exactly when Outcome is Shown
public sealed record CourseCardResult(CourseCardOutcome Outcome, CourseCardPageModel? Page);

// Application/UseCases/GetCourseCardQuery.cs
public sealed class GetCourseCardQuery(ICourseCardSource source, SchoolTimeZone schoolTimeZone)
{
    public Task<CourseCardResult> ExecuteAsync(CourseCardRequest request, CancellationToken cancellationToken);
}

// Application/Models/Dtos/
public sealed record CourseCardPageModel(
    CourseCardDetails Course,
    IReadOnlyList<CourseCardTeacher> Teachers,
    IReadOnlyList<CourseCardPerson> Students,
    IReadOnlyList<CourseCardLeaver> Leavers,
    string ReturnPath);

public sealed record CourseCardDetails(
    long CourseId,
    string Name,
    string? Section,
    CourseState State,
    string? Room,
    string? DescriptionHeading,
    string? Description,
    DateTime? CreatedAt,     // school-local, to the minute
    DateTime? UpdatedAt);    // school-local, to the minute

public sealed record CourseCardPerson(long ParticipantId, CourseListPersonName Name, string? Email);

public sealed record CourseCardTeacher(long ParticipantId, CourseListPersonName Name, string? Email, bool IsOwner);

public sealed record CourseCardLeaver(
    long ParticipantId, CourseListPersonName Name, string? Email, ClassroomRole Role, DateOnly LastSeenOn);
```

Mapping rules in `GetCourseCardQuery`:

- **Parse** (spec VR-001): `long.TryParse(value, NumberStyles.None,
  CultureInfo.InvariantCulture, out id) && id > 0`, else `CourseMalformed`
  with no read. Null or empty is malformed.
- **Q1 null** → `CourseUnknown`, Q2 not run.
- **Optional strings** (spec I-1): `string.IsNullOrWhiteSpace(v) ? null : v` —
  the value itself is passed unchanged, never trimmed or re-cased.
- **Times**: `TimeZoneInfo.ConvertTime(instant, schoolTimeZone.Zone)`; course
  times truncated to the minute as US-020 does; `LastSeenOn =
  DateOnly.FromDateTime(local.DateTime)`.
- **Sections** by `Role`/`OnRoster` (db-design §4), each keeping Q2's order;
  `IsOwner` copied only into `CourseCardTeacher`.
- **ReturnPath**: the card route with the parsed id in invariant decimal form
  (spec FR-010).

| DTO (openapi) | C# |
|---|---|
| `CourseCardPageModel` | `CourseCardPageModel` |
| `CourseCardDetails` | `CourseCardDetails` (`createdAt`/`updatedAt` → `DateTime?`) |
| `PersonName` | existing `CourseListPersonName` |
| `CourseCardPerson` / `CourseCardTeacher` / `CourseCardLeaver` | flat records, no inheritance (`lastSeenOn` → `DateOnly`) |
| `CourseCardOutcome` | `CourseCardOutcome` |

`CourseState` and `ClassroomRole` are Domain enums already used in US-020 DTOs
(`CourseListRow.State`); the view renders them through translation keys.

## 6. Verification

The skeleton of §2–§5 (port, records, enum, result, DTOs, the query and the
rule's signature with `throw new NotImplementedException()` bodies) was compiled
in a scratch project referencing `ClassroomAgent.Application` with nullable
enabled and warnings as errors: **build succeeded, 0 warnings**. Nothing was
added to the solution.
