---
artifact_type: entity_model
story: US-056
version: 1
status: DRAFT
created_at: 2026-10-25T17:40:00Z
updated_at: 2026-10-25T17:40:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-056-spec.md
    version: 3
  - path: docs/designs/database/US-056-db-design.md
    version: 1
supersedes: null
---

# US-056 Entity Model — Members seen when a course is first loaded count from the course's creation

## 1. Entities

**No entity changes.** `CourseMembership` (`FirstSeenAt`, `LastSeenAt`,
`OnRoster`, `CreatedAt`, `UpdatedAt`) and `Course` (`CreationTime`,
`CreatedAt`) keep their properties, mappings and invariants. `FirstSeenAt` keeps
its meaning — when synchronization first saw the person — and is never
rewritten (FR-004).

## 2. New Domain rule

`src/ClassroomAgent.Domain/Rules/MembershipStart.cs`:

```csharp
namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// The start of a course membership for journals, reports and Meet (trebovaniya.md §3 «Начало участия», v98;
/// BR-051; US-056 spec FR-001, db-design §1).
/// </summary>
public static class MembershipStart
{
    public static DateTimeOffset Of(
        DateTimeOffset firstSeenAt,
        DateTimeOffset courseFirstLoadedAt,
        DateTimeOffset? courseCreationTime) =>
        firstSeenAt <= courseFirstLoadedAt ? courseCreationTime ?? firstSeenAt : firstSeenAt;
}
```

| Parameter | Source column | Business concept |
|---|---|---|
| `firstSeenAt` | `course_membership.first_seen_at` | «впервые замечен» |
| `courseFirstLoadedAt` | `course.created_at` | the course's first committed load (FR-002) |
| `courseCreationTime` | `course.creation_time` | Google `creationTime` (absent → FR-001 b) |

## 3. Application records — `FirstSeenAt` becomes `StartsAt`

The four records that carry a membership to the period / date rules carry its
start instead of its first-seen instant. Position and type are unchanged
(`DateTimeOffset`); only the name and the value change:

| Record | Was | Becomes |
|---|---|---|
| `Application.Models.JournalMemberRecord` | `DateTimeOffset FirstSeenAt` (2nd) | `DateTimeOffset StartsAt` |
| `Application.Models.JournalCourseMemberRecord` | `DateTimeOffset FirstSeenAt` (3rd) | `DateTimeOffset StartsAt` |
| `Application.Models.MeetReportMemberRecord` | `DateTimeOffset FirstSeenAt` (5th) | `DateTimeOffset StartsAt` |
| `Application.MeetLinking.RosterMembership` | `DateTimeOffset FirstSeenAt` (4th) | `DateTimeOffset StartsAt` |

Readers fill `StartsAt` with `MembershipStart.Of(m.FirstSeenAt,
m.Course.CreatedAt, m.Course.CreationTime)` (db-design §3). No record carries
the first-seen instant any more: no consumer of these records uses it for
anything but the start (ripgrep, Spec §1).

Port signatures (`IJournalSource`, `IJournalFieldSource`, `IMeetReportSource`,
`IMeetCodeScoringSource`, `IMeetCodesReadSource`) are unchanged.

## 4. Application rules — signature names only

| Rule | Change |
|---|---|
| `JournalCellRule.IsOfPeriod(DateTimeOffset firstSeenAt, …)` | first parameter renamed `startsAt`; body unchanged (`startsAt < end && (onRoster \|\| lastSeenAt >= start)`) |
| `RosterOnDate.Covers(RosterMembership, DateOnly, TimeZoneInfo)` | reads `membership.StartsAt`; logic unchanged |
| callers `GetJournalQuery.cs:124`, `ReportBuilder.cs:97,156`, `GetMeetReportsQuery.cs:156`, `MeetReportRules.cs:148` | pass `m.StartsAt` |

## 5. Mapping to DTOs

None. No view model, request or response DTO changes (API design: NOT_APPLICABLE);
the start never reaches a page (Spec §10).
