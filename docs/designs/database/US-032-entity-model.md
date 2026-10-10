---
artifact_type: entity_model
story: US-032
version: 1
status: DRAFT
created_at: 2026-10-10T13:48:55Z
updated_at: 2026-10-10T13:48:55Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-032-spec.md
    version: 1
  - path: docs/designs/database/US-032-db-design.md
    version: 1
  - path: docs/designs/api/US-032-openapi.yaml
    version: 1
  - path: docs/decisions/US-032-open-decisions.md
    version: 1
supersedes: null
---

# US-032 Entity Model — Link meeting codes to courses

Names below are the design's intent. As with earlier Stories, they are verified
only once the skeleton built from them compiles; `TEST_WRITING` may adjust a
signature and record why. The tables, columns, constraints and behaviour in the
DB design may not be adjusted.

## 1. New entity: `MeetingCodeLink` (Domain/Entities)

```csharp
public sealed class MeetingCodeLink
{
    public const int MeetingCodeMaxLength = MeetSession.MeetingCodeMaxLength; // 64

    public long Id { get; private set; }
    public string MeetingCode { get; private set; }
    public long? CourseId { get; private set; }              // null = marked "not a course"
    public bool? LinkedAutomatically { get; private set; }
    public long? LinkedByAppUserId { get; private set; }
    public DateTimeOffset? LinkedAt { get; private set; }
    public long? ConfirmedByAppUserId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public long? MarkedByAppUserId { get; private set; }
    public DateTimeOffset? MarkedAt { get; private set; }
    public string ConcurrencyStamp { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public MeetingCodeLinkState State { get; }               // Linked | Marked (computed)
    public bool CanBeConfirmed { get; }                      // automatic and unconfirmed

    // Creation (an unassigned code has no row).
    public static MeetingCodeLink LinkAutomatically(string meetingCode, long courseId, DateTimeOffset at);
    public static MeetingCodeLink LinkByPerson(string meetingCode, long courseId, long appUserId, DateTimeOffset at);
    public static MeetingCodeLink MarkNotACourse(string meetingCode, long appUserId, DateTimeOffset at);

    // Changes of an existing row; each renews ConcurrencyStamp.
    public void Relink(long courseId, long appUserId, DateTimeOffset at);        // Linked → Linked, other course; clears confirmation
    public void Confirm(long appUserId, DateTimeOffset at);                      // only when CanBeConfirmed
    public void Mark(long appUserId, DateTimeOffset at);                         // Linked → Marked; clears course, maker, confirmation
    public void LinkFromMark(long courseId, long appUserId, DateTimeOffset at);  // Marked → Linked by a person
}

public enum MeetingCodeLinkState { Linked, Marked }
```

Guards (`InvalidOperationException` for a wrong state, `ArgumentException` for a
bad value) mirror `ck_meeting_code_link_*`: blank or over-long code, id ≤ 0,
`Relink` to the current course, `Confirm` on a person's or confirmed link,
`Mark` on a marked row, `LinkFromMark` on a linked row. The use cases check the
expected state **before** calling these (spec FR-013), so a guard firing is a
defect, not an expected outcome (AD-9).

## 2. Changed domain types

- **`SyncStep`** gains `Linking` (stored `linking`, db-design §4).
- **`AuditAction`** gains `MeetCodeAutoLinked`, `MeetCodeCoursePicked`,
  `MeetCodeLinkConfirmed`, `MeetCodeRelinked`, `MeetCodeMarkedNotACourse`,
  `MeetCodeMarkRemoved` (codes in db-design §3.1).
- **`AuditEvent`** gains `MeetCode` (string?), `MeetPreviousCourseId` (long?),
  `PurgedMeetCodeLinks` (int?) and factories:

```csharp
public static AuditEvent MeetCodeAutoLinked(string meetingCode, long courseId, DateTimeOffset occurredAt);

// Person's succeeded change; the factory derives target/previous per db-design §3.2
// and rejects a combination ck_audit_event_meet_code_shape forbids.
public static AuditEvent MeetCodeChanged(AuditAction action, long appUserId, AppRole actorRole,
    string meetingCode, long? courseId, long? previousCourseId, DateTimeOffset occurredAt, string? requestId);

// Read-only refusal: no course ids; the code only when it passed its shape.
public static AuditEvent MeetCodeChangeRefused(AuditAction action, long appUserId, AppRole actorRole,
    string? meetingCode, DateTimeOffset occurredAt, string? requestId);
```

- **`RetentionPurgeCounts`** gains `MeetCodeLinks` (non-negative), and the
  `RetentionPurgeRun` factory writes it to `PurgedMeetCodeLinks` (always
  non-null for new rows).

## 3. Configuration

`InstallationSettings` gains `MeetLinking` — `MeetLinkingThresholds(int
MinSharePercent, int MinGapPoints)` — read by `InstallationSettingsReader` from
`MeetLinking:MinSharePercent` / `MeetLinking:MinGapPoints` (whole numbers 1 …
100, defaults 60 / 30, present-but-invalid → `InstallationSettingException`
naming the key, value not logged; spec FR-005). Registered for `Application`
as an options-like value (as the retention period is).

## 4. Pure Application functions (`Application/MeetLinking`)

```csharp
// Spec FR-002: whole dates in the school's zone.
public static class RosterOnDate
{
    public static bool Covers(RosterMembership membership, DateOnly date, TimeZoneInfo zone);
}

// Spec FR-003 / FR-004. Input is one code's data; output its candidates with exact shares
// (numerator/denominator) and the decision.
public static class MeetCodeScorer
{
    public static MeetCodeScore Score(MeetCodeScoringInput input, TimeZoneInfo zone);
    public static MeetCodeDecision Decide(MeetCodeScore score, MeetLinkingThresholds thresholds);
}

public sealed record RosterMembership(long CourseId, ClassroomRole Role, string Email,
    DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt, bool OnRoster);
public sealed record MeetCodeMeeting(long MeetSessionId, string OrganizerEmail, DateTimeOffset StartedAt,
    IReadOnlyList<string> ParticipantEmails);
public sealed record MeetCodeScoringInput(string MeetingCode, IReadOnlyList<MeetCodeMeeting> Meetings,
    IReadOnlyList<RosterMembership> Memberships);
public sealed record CandidateShare(long CourseId, int Numerator, int Denominator)
{
    public int PercentRoundedDown { get; }
}
public sealed record MeetCodeScore(string MeetingCode, IReadOnlyList<CandidateShare> Candidates); // best first
public sealed record MeetCodeDecision(bool Unambiguous, long? CourseId);
```

Emails are compared with `StringComparison.OrdinalIgnoreCase`. Comparisons of
shares are by cross-multiplication, never by rounded percent (spec I-4).

## 5. Ports (Application/Ports)

```csharp
public interface IMeetingCodeLinkRepository
{
    Task<MeetingCodeLink?> GetByCodeAsync(string meetingCode, CancellationToken cancellationToken);
    Task<bool> CodeHasMeetingsAsync(string meetingCode, CancellationToken cancellationToken);
    void Add(MeetingCodeLink link);
}

// db-design §5.2: bounded reads for scoring.
public interface IMeetCodeScoringSource
{
    Task<IReadOnlyList<string>> GetUnassignedCodesAsync(string? afterCode, int batchSize,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<MeetCodeScoringInput>> GetScoringInputsAsync(IReadOnlyCollection<string> meetingCodes,
        CancellationToken cancellationToken);
}

// db-design §5: the page's lists.
public interface IMeetCodesReadSource
{
    Task<MeetCodeListCounts> GetCountsAsync(CancellationToken cancellationToken);
    Task<PagedRows<UnassignedCodeRow>> GetUnassignedPageAsync(int page, int size, TimeZoneInfo zone,
        CancellationToken cancellationToken);   // ordered: has-candidate first (SQL EXISTS), last meeting desc, code
    Task<PagedRows<LinkedCodeRow>> GetLinkedPageAsync(int page, int size, CancellationToken cancellationToken);
    Task<PagedRows<MarkedCodeRow>> GetMarkedPageAsync(int page, int size, CancellationToken cancellationToken);
    Task<IReadOnlyList<CourseOptionRow>> GetAllCoursesAsync(CancellationToken cancellationToken);
    Task<bool> CourseExistsAsync(long courseId, CancellationToken cancellationToken);
}
```

The row records (`UnassignedCodeRow`, `LinkedCodeRow`, `MarkedCodeRow`,
`CourseOptionRow`, `MeetCodeListCounts`, `PagedRows<T>`) live in
`Application/Models` and carry the columns of OpenAPI's items before mapping
(account emails resolved from `app_user`, null for a deleted account).

`IRetentionPurgeStore` changes:

```csharp
// CourseActivityDates gains DateTimeOffset? LatestLinkedMeetingStart.
Task<CourseDeletionCounts> DeleteCourseAsync(long courseId, CancellationToken cancellationToken);   // was Task
Task<LeaverDeletionCounts> DeleteExpiredLeaversAsync(long courseId, DateTimeOffset cutoff,
    CancellationToken cancellationToken);                                                         // was Task<int>
Task<int> DeleteOrphanedNotACourseMarksAsync(CancellationToken cancellationToken);

public sealed record CourseDeletionCounts(int MeetSessions, int MeetParticipations, int MeetCodeLinks);
public sealed record LeaverDeletionCounts(int Memberships, int MeetParticipations);
```

## 6. Use cases

- **`LinkMeetCodesStep`** (Application) — called by `RunSynchronizationUseCase`
  after the Meet step (spec FR-006): batches of 200 unassigned codes; score,
  decide, and per unambiguous code `MeetingCodeLink.LinkAutomatically` +
  `AuditEvent.MeetCodeAutoLinked` saved in one transaction; a unique-violation
  on save skips the code (db-design §7). Returns `(codesScored, linksCreated)`
  for the run outcome. Any other failure → `FailRun(…, SyncStep.Linking)` in
  `RunSynchronizationUseCase`. `CompleteRun` moves after this step.
- **`GetMeetCodesQuery`** → `MeetCodesPageModel`; validates `list`/`page`/`size`
  (spec VR-004); computes shares for the page's unassigned codes through
  `MeetCodeScorer`.
- **`GetMeetCodeCourseChoiceQuery`** → `MeetCodeCourseChoicePageModel`.
- **`SetMeetCodeCourseUseCase`**, **`ConfirmMeetCodeLinkUseCase`**,
  **`MarkMeetCodeNotACourseUseCase`** — the order of API design §2.5: guard (with
  the refused row, as `ReportTemplateRefusalAudit`), form shape
  (`Application/Validation`), existence, expected state, change, audit, save.
  `DbUpdateConcurrencyException` / unique violation surface through the unit of
  work as a dedicated Application exception mapped to the stale `409`; FK
  violation on the course → course not found.
- **`RunRetentionPurgeUseCase`** — last activity includes
  `LatestLinkedMeetingStart`; collects the new counts; calls
  `DeleteOrphanedNotACourseMarksAsync` after the meeting-date step (db-design §6).

Request types (`Application/Models/Requests`): `MeetCodeLinkForm`,
`MeetCodeConfirmationForm`, `MeetCodeMarkForm` — string fields as OpenAPI.

## 7. Infrastructure

- `Persistence/Configurations/MeetingCodeLinkConfiguration.cs`;
  `AuditEventConfiguration`, `SyncStateConfiguration`,
  `ClassroomParticipantConfiguration` (expression index) extended.
- `Persistence/MeetingCodeLinkRepository.cs`, `MeetCodeScoringSource.cs`,
  `MeetCodesReadSource.cs`; `RetentionPurgeStore` extended.
- Migration `AddMeetingCodeLinks` (db-design §8).

## 8. Mapping to business concepts and DTOs

| Concept (`trebovaniya.md` §3, §4 Epic 4) | Entity / column | DTO (OpenAPI) |
|---|---|---|
| `MeetingCodeLink` — code → course | `MeetingCodeLink.CourseId` | `LinkedCodeItem.course` |
| created automatically / by a person | `LinkedAutomatically`, `LinkedByAppUserId`, `LinkedAt` | `madeAutomatically`, `madeBy`, `madeAt` |
| confirmed, by whom, when | `ConfirmedByAppUserId`, `ConfirmedAt` | `confirmedBy`, `confirmedAt`, `canConfirm` |
| "не курс" mark, who, when (v88) | `CourseId = null`, `MarkedByAppUserId`, `MarkedAt` | `NotACourseCodeItem.markedBy`, `markedAt` |
| unassigned meetings | no row; `meet_session` anti-join | `UnassignedCodeItem` |
| candidate and share (BR-065) | computed (`MeetCodeScorer`) | `CandidateCourse.sharePercent` |
| thresholds (OD-006) | configuration `MeetLinkingThresholds` | none |
| audit of link changes (SC-11) | `AuditEvent` + `MeetCode`, `MeetPreviousCourseId`, target course | none (EPIC-9) |
| purge count | `AuditEvent.PurgedMeetCodeLinks` | none |
| step of a failed run | `SyncStep.Linking` | `LastSynchronizationView.failedStep` |

No entity appears in a controller, request, response or view model (AD-8).
