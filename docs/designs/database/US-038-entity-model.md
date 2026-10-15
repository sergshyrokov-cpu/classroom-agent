---
artifact_type: entity_model
story: US-038
version: 1
status: DRAFT
created_at: 2026-10-15T06:05:00Z
updated_at: 2026-10-15T06:05:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-038-spec.md
    version: 1
  - path: docs/designs/api/US-038-openapi.yaml
    version: 1
  - path: docs/designs/database/US-038-db-design.md
    version: 1
supersedes: null
---

# US-038 Entity Model — Erase all data of one participant on request

Signatures are indicative until the TEST_WRITING skeleton compiles them; names
may be adjusted there without changing meaning. Tables and constraints:
`US-038-db-design.md`.

## 1. New entity: `ErasedParticipant` (Domain/Entities)

```csharp
public sealed class ErasedParticipant
{
    public const int MaxGoogleUserIdLength = ClassroomParticipant.MaxGoogleUserIdLength; // 64
    public const int FingerprintLength = 32;

    public long Id { get; private set; }
    public string GoogleUserId { get; private set; }
    public byte[]? EmailFingerprint { get; private set; }   // null: participant had no email (OD-001 a)
    public DateTimeOffset ErasedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ErasedParticipant Record(string googleUserId, EmailFingerprint? fingerprint, DateTimeOffset erasedAt);
}
```

- `Record` rejects an empty or over-long Google user id. No method changes it
  afterwards (`ck_erased_participant_immutable`).
- No name, no email, no navigation.

## 2. New entity: `ErasureSalt` (Domain/Entities)

```csharp
public sealed class ErasureSalt
{
    public const int Length = 32;
    public long Id { get; private set; }
    public byte[] Value { get; private set; }              // column `salt`
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ErasureSalt Generate();                   // RandomNumberGenerator.GetBytes(Length)
}
```

`ToString()` is not overridden with the value; the value never reaches a log.
Shadow `Singleton` as `SyncState`.

## 3. New value: `EmailFingerprint` (Domain/Rules)

```csharp
public readonly record struct EmailFingerprint
{
    public ReadOnlyMemory<byte> Bytes { get; }
    public static EmailFingerprint Of(string email, ErasureSalt salt);
    // = HMACSHA256(key: salt.Value, data: UTF-8(WorkspaceConnection.NormalizeEmail(email)))
}
```

The single place erasure, lifting and the Meet load compute it (spec FR-010).
Equality compares bytes. `ToString()` prints nothing of the value.

## 4. New value: `ErasureCounts` (Domain/Rules)

```csharp
public sealed record ErasureCounts(
    int Memberships, int Submissions, int MeetParticipations,
    int ClearedMeetOrganizers, int ClearedCourseOwners);
```

Each non-negative (constructor check, as `RetentionPurgeCounts`).

## 5. Changed domain types

| Type | Change |
|---|---|
| `MeetSession.OrganizerEmail` | `string` → `string?`. `Store(…)` accepts a null organizer. The compiler (nullable, warnings as errors) then forces every reader listed in db-design §4's lookup — `MeetCodesReadSource`, `MeetCodeScoringSource`, `MeetReportSource`, `MeetReportRules`, `MeetCodeScorer`, `GetMeetReportsQuery`, `GetMeetReportMeetingQuery`, `RunSynchronizationUseCase` — to handle "organizer unknown" (spec I-9). |
| `AuditAction` | + `ParticipantErased`, `ErasureExclusionLifted` |
| `AuditTargetType` | + `ClassroomParticipant`, `ErasedParticipant` |
| `AuditEvent` | + `int? ErasedMemberships, ErasedSubmissions, ErasedMeetParticipations, ClearedMeetOrganizers, ClearedCourseOwners`; factories below |

```csharp
public static AuditEvent ParticipantErased(long adminId, long participantId, ErasureCounts counts, DateTimeOffset occurredAt, string? requestId);
public static AuditEvent ErasureExclusionLifted(long adminId, long exclusionId, DateTimeOffset occurredAt, string? requestId);
public static AuditEvent ErasureRefused(AuditAction action, long adminId, long? participantId, AuditRefusalCategory category, DateTimeOffset occurredAt, string? requestId);
// ErasureRefused: action ∈ {ParticipantErased, ErasureExclusionLifted}; category must be ReadOnlyMode;
// participantId only with ParticipantErased.
```

Parameter lists follow the existing factories' shape (`DeanAccountManaged`,
`DeanAccountManagementRefused`); the skeleton aligns them.

## 6. Configuration (Infrastructure/Persistence/Configurations)

- `ErasedParticipantConfiguration` — table, keys, lengths, `bytea`, checks and
  indexes of db-design §2.
- `ErasureSaltConfiguration` — db-design §3, singleton as `SyncStateConfiguration`.
- `MeetSessionConfiguration` — `OrganizerEmail` without `IsRequired()`; check
  text of db-design §4.
- `CourseConfiguration` — `ix_course_owner_google_id`.
- `AuditEventConfiguration` — db-design §9.
- `ClassroomAgentDbContext` — `DbSet<ErasedParticipant> ErasedParticipants`,
  `DbSet<ErasureSalt> ErasureSalts`.

## 7. Ports (Application/Ports)

```csharp
public interface IErasureFence
{
    // Must run inside a transaction; released at commit/rollback (db-design §7.3).
    Task EnterAsWriterAsync(CancellationToken cancellationToken);   // shared
    Task EnterAsErasureAsync(CancellationToken cancellationToken);  // exclusive
}

public interface IErasureSaltStore
{
    Task<ErasureSalt?> FindAsync(CancellationToken cancellationToken);
    Task<ErasureSalt> GetOrCreateAsync(CancellationToken cancellationToken); // INSERT … ON CONFLICT DO NOTHING
}

public interface IErasedParticipantRepository
{
    void Add(ErasedParticipant exclusion);
    Task<IReadOnlySet<string>> GetExcludedGoogleUserIdsAsync(IReadOnlyCollection<string> googleUserIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmailFingerprint>> GetExcludedFingerprintsAsync(IReadOnlyCollection<EmailFingerprint> candidates, CancellationToken cancellationToken);
    Task<IReadOnlyList<ErasedParticipant>> GetByFingerprintAsync(EmailFingerprint fingerprint, CancellationToken cancellationToken);
    void Remove(ErasedParticipant exclusion);
}

public interface IParticipantErasureStore
{
    // Set-based, inside the caller's transaction, child-first (db-design §7.1).
    // Returns null when the participant row no longer exists (→ 404, rollback).
    Task<ErasureCounts?> EraseAsync(long participantId, string googleUserId, string? normalizedEmail, CancellationToken cancellationToken);
}

public interface IParticipantErasureReadSource
{
    Task<ParticipantSearchPage> SearchAsync(string term, int limit, CancellationToken cancellationToken);
    Task<ErasureConfirmationData?> GetConfirmationAsync(long participantId, CancellationToken cancellationToken);
    Task<ParticipantErasureTarget?> GetTargetAsync(long participantId, CancellationToken cancellationToken); // id, Google user id, email
}
```

`ParticipantSearchPage`, `ErasureConfirmationData` and `ParticipantErasureTarget`
are read records in `Application/Models`, not entities.

## 8. Use cases and queries (Application/UseCases)

| Type | Uses |
|---|---|
| `SearchParticipantsForErasureQuery` | `IParticipantErasureReadSource.SearchAsync` (limit 51) → `ErasurePageModel.search` |
| `GetErasureConfirmationQuery` | `GetConfirmationAsync` + legitimacy mode → `ErasureConfirmationPageModel` |
| `EraseParticipantUseCase` | guard → mark → transaction { `IErasureFence.EnterAsErasureAsync`, `GetTargetAsync`, `EraseAsync`, `IErasureSaltStore.GetOrCreateAsync`, `EmailFingerprint.Of`, `ErasedParticipant.Record`, `AuditEvent.ParticipantErased` } → log |
| `LiftErasureExclusionUseCase` | guard → email → `FindAsync` salt → `GetByFingerprintAsync` → `Remove` each + `ErasureExclusionLifted` each |
| `RunSynchronizationUseCase` (changed) | per course transaction: `EnterAsWriterAsync`, then `GetExcludedGoogleUserIdsAsync` for roster + submitters; skip excluded; owner → null when excluded |
| Meet load in the same use case (changed) | per page transaction: `EnterAsWriterAsync`; salt via `FindAsync` (cached per run); fingerprints of the page's emails; `GetExcludedFingerprintsAsync`; skip participations, store organizer as null |

Read-only refusals follow `CreateDeanAccountUseCase`: on `ReadOnlyModeException`
add `AuditEvent.ErasureRefused`, save under
`ServiceWriteScope.Declare(PermittedServiceWrite.AuditEvent)`, rethrow.

## 9. Infrastructure

- `ErasureFence` — `ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock_shared({0})", key)`
  / `pg_advisory_xact_lock`; throws if no transaction is open.
- `ErasureSaltStore`, `ErasedParticipantRepository`, `ParticipantErasureStore`
  (`ExecuteDeleteAsync` / `ExecuteUpdateAsync`, as `RetentionPurgeStore`),
  `ParticipantErasureReadSource`.
- Migration `AddParticipantErasure` (db-design §11).

## 10. Mapping to business concepts and DTOs

| Business concept (§3, Epic 10) | Entity / column | DTO field (openapi) |
|---|---|---|
| Exclusion (`ErasedParticipant`) | `erased_participant` | none — never shown (spec I-14) |
| Google user id | `erased_participant.google_user_id` | none |
| Email fingerprint | `erased_participant.email_fingerprint` | none |
| Erasure date | `erased_participant.erased_at` | none |
| Installation salt | `erasure_salt.salt` | none |
| Participant found | `classroom_participant` | `ParticipantSearchResult` (`participantId`, `fullName`, `email`, `teacherCourses`, `studentCourses`) |
| What will be erased | counts over `submission`, `meet_participation`, `meet_session`, `course` | `ErasureConfirmationPageModel` counts, `courses[]` |
| No email (OD-001) | `classroom_participant.email IS NULL` | `noEmailNotice` |
| Erasure audit | `audit_event` + five count columns | none |
| Organizer unknown | `meet_session.organizer_email IS NULL` | existing Meet report DTOs show "unknown" |
