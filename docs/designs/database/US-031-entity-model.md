---
artifact_type: entity_model
story: US-031
version: 1
status: DRAFT
created_at: 2026-10-10T05:54:15Z
updated_at: 2026-10-10T05:54:15Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-031-spec.md
    version: 1
  - path: docs/designs/database/US-031-db-design.md
    version: 1
  - path: docs/designs/api/US-031-openapi.yaml
    version: 1
  - path: docs/decisions/US-031-open-decisions.md
    version: 1
supersedes: null
---

# US-031 Entity Model — Pull Meet events and keep history beyond 180 days

Names below are the design's intent. As with earlier Stories, they are verified
only once the skeleton built from them compiles; `TEST_WRITING` may adjust a
signature and record why. The tables, columns, constraints and behaviour in the
DB design may not be adjusted.

## 1. New entity: `MeetSession` (Domain/Entities)

```csharp
public sealed class MeetSession
{
    public const int ConferenceIdMaxLength = 128;
    public const int MeetingCodeMaxLength = 64;
    public const int EmailMaxLength = 254;

    public long Id { get; private set; }
    public string ConferenceId { get; private set; }
    public string MeetingCode { get; private set; }
    public string OrganizerEmail { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset EndedAt { get; private set; }
    public IReadOnlyCollection<MeetParticipation> Participations { get; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // A new meeting with its first connection; start/end from it (FR-005, FR-008).
    public static MeetSession Store(string conferenceId, string meetingCode, string organizerEmail,
        MeetConnection firstConnection);

    // Adds a new endpoint or updates a known one, then recomputes StartedAt/EndedAt over
    // all participations (FR-009, AC-002). Code and organizer are never changed (I-4).
    public void Record(MeetConnection connection);
}
```

Guards (`ArgumentException`): blank or over-long values; the invariants match
`ck_meet_session_*`. Start = min `JoinedAt`, end = max `JoinedAt + Duration`.

## 2. New entity: `MeetParticipation` (Domain/Entities)

```csharp
public sealed class MeetParticipation
{
    public const int EndpointIdMaxLength = 128;
    public const int MaxDurationSeconds = 86_400;

    public long Id { get; private set; }
    public long MeetSessionId { get; private set; }
    public string EndpointId { get; private set; }
    public string? Email { get; private set; }          // null = other participant
    public bool IsOtherParticipant => Email is null;    // not mapped
    public DateTimeOffset JoinedAt { get; private set; }
    public int DurationSeconds { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
```

Created and updated only through `MeetSession.Record` (internal factory /
`Apply`), so a participation never exists without its session.

## 3. New value types (Domain)

```csharp
// Domain/Rules — one connection as the domain sees it, already validated.
public sealed record MeetConnection(string EndpointId, string? DomainEmail,
    DateTimeOffset JoinedAt, int DurationSeconds);

// Domain/Rules — FR-006: exact, case-insensitive match after the last '@'; no subdomains.
public static class SchoolDomainAccount
{
    public static bool IsDomainAccount(string? email, string schoolDomain);
}

// Domain/Enums — FR-010.
public enum SyncStep { Classroom, Meet }
```

## 4. Changed domain types

**`SyncState`**

```csharp
public DateTimeOffset? MeetLoadedUpTo { get; private set; }
public SyncStep? FailedStep { get; private set; }

// Completed run: the Meet step finished, so the watermark moves (FR-007).
// Throws if meetLoadedUpTo is earlier than the stored one or later than finishedAt (VR-002).
public void CompleteRun(DateTimeOffset finishedAt, int processedCount, DateTimeOffset meetLoadedUpTo);

// Failed run: the watermark stays (FR-007, FR-010).
public void FailRun(DateTimeOffset finishedAt, int processedCount, SyncDiagnosis diagnosis, SyncStep step);
```

The existing two-argument `CompleteRun` and three-argument `FailRun` are
replaced, not overloaded — every run now has both steps. `Begin` clears
`FailedStep`. Stored as `classroom` / `meet` (lower-case, like
`SyncRunStatus`).

**`RetentionPurgeCounts`** gains `int MeetSessions, int MeetParticipations`
(non-nullable). **`AuditEvent.RetentionPurgeRun`** guards them as non-negative
and writes `PurgedMeetSessions` / `PurgedMeetParticipations` (`int?` properties,
null on every other action and on pre-Story purge rows).

## 5. Ports (Application/Ports)

```csharp
public interface IMeetReportsReader : IGoogleDataPort
{
    // call_ended events in [from, to), page by page (spec FR-003, FR-004).
    IAsyncEnumerable<MeetEventPage> ReadCallEndedAsync(string impersonationUser,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

public interface IMeetSessionRepository
{
    // The sessions of these conference ids, with all their participations (db-design §7).
    Task<IReadOnlyList<MeetSession>> GetByConferenceIdsAsync(IReadOnlyCollection<string> conferenceIds,
        CancellationToken cancellationToken);
    void Add(MeetSession session);
}
```

`IRetentionPurgeStore` gains:

```csharp
Task<IReadOnlyList<long>> GetExpiredMeetSessionIdsAsync(DateTimeOffset cutoff, int batchSize,
    CancellationToken cancellationToken);
Task<(int Sessions, int Participations)> DeleteMeetSessionsAsync(IReadOnlyCollection<long> sessionIds,
    CancellationToken cancellationToken);
```

Application models (`Application/Models`):

```csharp
public sealed record MeetEventPage(IReadOnlyList<MeetCallEndedEvent> Events, int UnreadableCount);

// Raw values as mapped by the adapter — still unvalidated (spec VR-001).
public sealed record MeetCallEndedEvent(DateTimeOffset? OccurredAt, string? ConferenceId,
    string? MeetingCode, string? OrganizerEmail, string? EndpointId, string? Identifier,
    string? IdentifierType, long? DurationSeconds);
```

No Google SDK type appears in either (AD-4). Failures leave the adapter as
`GoogleReadFailedException` (US-017), unchanged.

## 6. Use cases

- **`RunSynchronizationUseCase`** gains `IMeetReportsReader`,
  `IMeetSessionRepository` and the saved domain (from the connection query) and
  runs the Meet step after the Classroom step (spec FR-001 … FR-010): window,
  per-page validation (`MeetEventValidator`, `Application/Validation`), FR-005
  per conference, `MeetSession.Store` / `Record`, one `SaveChangesAsync` per
  page, then `CompleteRun(…, to)`. A `GoogleReadFailedException` thrown in the
  Meet step → `FailRun(…, SyncStep.Meet)`; in the Classroom step →
  `SyncStep.Classroom`. The Meet counts travel in `SynchronizationRunOutcome`
  for the log line (FR-012).
- **`RunRetentionPurgeUseCase`** gains the meeting step (db-design §6), batches of
  500, one transaction per batch, and the two counts.
- **`GetLastSynchronizationQuery`** gains `SchoolTimeZone` and fills
  `MeetLoadedUpTo` (converted to the school's local time) and `FailedStep`
  (OpenAPI `LastSynchronizationView`).

## 7. Infrastructure

- `Persistence/Configurations/MeetSessionConfiguration.cs`,
  `MeetParticipationConfiguration.cs`; `SyncStateConfiguration` and
  `AuditEventConfiguration` extended (db-design §4, §5).
- `Persistence/MeetSessionRepository.cs`; `RetentionPurgeStore` extended.
- `Google/GoogleMeetReportsReader.cs` — `activities.list(all, meet,
  eventName=call_ended, startTime, endTime)`, `maxResults` 1 000, every page,
  through `GoogleRetryHandler` / `GoogleFailureClassifier`; the parameter-name →
  model mapping in one private method (OD-009).
- Migration `AddMeetPull` (db-design §8).

## 8. Mapping to business concepts and DTOs

| Concept (`trebovaniya.md` §3) | Entity / column | DTO |
|---|---|---|
| `MeetSession` | `MeetSession` / `meet_session` | none in this Story (US-033) |
| `MeetParticipation`, "прочий участник" | `MeetParticipation`, `email IS NULL` | none in this Story |
| Meet loaded up to (§4 Epic 6 v87) | `SyncState.MeetLoadedUpTo` | `LastSynchronizationView.meetLoadedUpTo` (school local) |
| step of a failed run | `SyncState.FailedStep` | `LastSynchronizationView.failedStep` |
| purge counts (§5, SC-11) | `AuditEvent.PurgedMeetSessions/Participations` | none (audit viewer is EPIC-9) |

No entity appears in a controller, request, response or view model (AD-8).
