---
artifact_type: entity_model
story: US-033
version: 1
status: DRAFT
created_at: 2026-10-10T19:50:00Z
updated_at: 2026-10-10T19:50:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-033-spec.md
    version: 1
  - path: docs/designs/database/US-033-db-design.md
    version: 1
  - path: docs/designs/api/US-033-openapi.yaml
    version: 1
  - path: docs/decisions/US-033-open-decisions.md
    version: 1
supersedes: null
---

# US-033 Entity Model — Built-in Meet reports for a course and period

The names below are the design's intent. As in earlier Stories, they count as
verified only once the skeleton built from them compiles. `TEST_WRITING` may
adjust a signature and must record why. The queries, the matching rules and the
absence of a schema change (DB design §1–§4) may not be adjusted.

## 1. Entities

**No entity is added or changed.** The reports read `MeetSession`,
`MeetParticipation`, `MeetingCodeLink`, `Course`, `CourseMembership` and
`ClassroomParticipant`. Nothing about them changes, and no entity crosses into
`Application` models, controllers or views (AD-8): reads land in the records
of §3.

## 2. Business concept → stored data

| Concept (spec) | Stored as |
|---|---|
| meeting of the course (FR-004) | `MeetSession` whose `MeetingCode` has a `MeetingCodeLink` with that `CourseId` |
| meeting's duration (FR-004) | `EndedAt − StartedAt` |
| connection, its seconds (FR-006) | `MeetParticipation.DurationSeconds`; `Email` null = no account |
| organizer (FR-005) | `MeetSession.OrganizerEmail` |
| course teacher / student on D (FR-005) | `CourseMembership` with `Role`, covered by `RosterOnDate.Covers` (US-032) |
| personal account (FR-005 rule 4) | a `lower(email)` with any `CourseMembership` of its `ClassroomParticipant` |
| person's name (FR-011) | `ClassroomParticipant.FullName` (DB design §4 tie rule) |
| Meet loaded up to (FR-012) | `LastSynchronizationView.MeetLoadedUpTo` (existing) |

## 3. Application port (Application/Ports)

```csharp
/// <summary>The Meet reports' reads (US-033 db-design §3). Read-only, untracked, nothing logged but counts.</summary>
public interface IMeetReportSource
{
    /// <summary>R6 — the course's name, or null when no course has this id.</summary>
    Task<string?> GetCourseNameAsync(long courseId, CancellationToken cancellationToken);

    /// <summary>R1 + R2 — meetings of the course with start in [start, endExclusive), each with its connections.</summary>
    Task<IReadOnlyList<MeetReportMeetingRecord>> GetMeetingsAsync(
        long courseId,
        DateTimeOffset start,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken);

    /// <summary>R3 — one meeting with its connections when it is a meeting of the course; otherwise null.</summary>
    Task<MeetReportMeetingRecord?> GetMeetingOfCourseAsync(
        long courseId,
        long meetingId,
        CancellationToken cancellationToken);

    /// <summary>R4 — every membership of the course, any role, with its participant's email and full name.</summary>
    Task<IReadOnlyList<MeetReportMemberRecord>> GetCourseMembersAsync(long courseId, CancellationToken cancellationToken);

    /// <summary>R5 — one record per requested lower-cased email that matches a participant (db-design §4).</summary>
    Task<IReadOnlyList<MeetReportPersonRecord>> GetPeopleAsync(
        IReadOnlyCollection<string> lowerCaseEmails,
        CancellationToken cancellationToken);
}
```

The name `IMeetReportSource` keeps clear of the existing Google port
`IMeetReportsReader` (US-031). The two must not be confused.

## 4. Read records (Application/Models or Application/MeetReports)

```csharp
public sealed record MeetReportMeetingRecord(
    long Id,
    string MeetingCode,
    string OrganizerEmail,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    IReadOnlyList<MeetReportConnectionRecord> Connections);

/// <summary>Email null = external guest or connection without an account (PC-12).</summary>
public sealed record MeetReportConnectionRecord(string? Email, int DurationSeconds);

/// <summary>Email may be null: the participant is still a report-3 row (db-design §7).</summary>
public sealed record MeetReportMemberRecord(
    long ParticipantId,
    ClassroomRole Role,
    string? Email,
    string? FullName,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    bool OnRoster);

/// <summary>Per lower-cased email: the displayed name (tie rule) and whether it is a personal account.</summary>
public sealed record MeetReportPersonRecord(string LowerCaseEmail, string? FullName, bool HasAnyMembership);
```

The classification of FR-005 is done in Application:

- `MeetReportMemberRecord` maps to the existing `RosterMembership` (US-032)
  whenever `Email` is not null, so `RosterOnDate.Covers` is reused unchanged;
- it is a pure function of the records above, the meeting's start date in
  `SchoolTimeZone.Zone`, and the precedence of spec I-4.

## 5. Mapping to the API DTOs (US-033-openapi.yaml)

| DTO | Built from |
|---|---|
| `MeetReportsPageModel.courses` | `IJournalSource.GetCoursesAsync` (as in the journal) |
| `MeetSummary` | `MeetListRow`s (FR-007) |
| `MeetListRow` | `MeetReportMeetingRecord` + classification; `organizer` via `MeetReportPersonRecord` |
| `StudentParticipationRow` | `MeetReportMemberRecord` (role Student, in the period) + per-meeting capped seconds |
| `MeetingDetail` | `GetCourseNameAsync` + `GetMeetingOfCourseAsync` + classification |
| `PersonRef` | `FullName` from `MeetReportPersonRecord` / `MeetReportMemberRecord`, plus the email |
| `MeetDelayNotice.meetLoadedUpTo` | `GetLastSynchronizationQuery` → `LastSynchronizationView.MeetLoadedUpTo` |

Every DTO is mapped in Application. No record of §4 is exposed to a controller
or a view.

## 6. Infrastructure

`MeetReportSource : IMeetReportSource` goes in
`Infrastructure/Persistence`, following `MeetCodeScoringSource`. It uses
`AsNoTracking` queries over `ClassroomAgentDbContext`, with `lower(email)`
comparisons through `EF.Functions` / `string.ToLower()` translated by Npgsql. It
is registered scoped in DI. There is no migration.
