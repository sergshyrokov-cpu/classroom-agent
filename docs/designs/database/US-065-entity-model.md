---
artifact_type: entity_model
story: US-065
version: 1
status: DRAFT
created_at: 2026-10-28T11:49:36Z
updated_at: 2026-10-28T11:49:36Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-065-spec.md
    version: 2
  - path: docs/designs/api/US-065-openapi.yaml
    version: 1
  - path: docs/designs/database/US-065-db-design.md
    version: 1
supersedes: null
---

# US-065 Entity Model

Signatures are indicative where marked; names, nullability and the split
between layers are binding. The TEST_WRITING skeleton compiles them (standing
rule); a signature that does not compile loops back here.

## 1. `ErasedParticipant` (Domain/Entities) — changed

```csharp
public sealed class ErasedParticipant
{
    public long Id { get; private set; }
    public string? GoogleUserId { get; private set; }          // was string, non-null
    public byte[]? EmailFingerprint { get; private set; }
    public DateTimeOffset ErasedAt { get; private set; }       // entry date for an exclusion entered in advance
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // unchanged (US-038): erasure — Google id required, fingerprint optional
    public static ErasedParticipant Record(string googleUserId, EmailFingerprint? fingerprint, DateTimeOffset erasedAt);

    // new (spec FR-003): entered in advance — fingerprint required, no Google id
    public static ErasedParticipant EnterInAdvance(EmailFingerprint fingerprint, DateTimeOffset enteredAt);
}
```

- No instance method fills the Google id. The fill is a set-based guarded
  `UPDATE` in the repository (db-design §4 step 6), so no tracked entity is ever
  modified; the trigger (db-design §2.3) is the rule.
- The private constructor no longer sets `GoogleUserId = string.Empty`.
- Callers reading `GoogleUserId` must handle `null` (the nullable warning makes
  every one visible at compile time).

Business concept: «исключение» (`trebovaniya.md` §3 v104), glossary
"ErasedParticipant".

## 2. `AuditAction` / `AuditEvent` (Domain) — extended

```csharp
public enum AuditAction { …, ErasureExclusionEntered }

// spec FR-013: target = the new exclusion's internal id
public static AuditEvent ErasureExclusionEntered(long adminId, long exclusionId, DateTimeOffset occurredAt, string? requestId);
```

`AuditEvent.ErasureRefused` accepts `AuditAction.ErasureExclusionEntered`
with `participantId: null` (same rule as the lifting). `exclusionId` is known
only after insert: the use case saves the exclusion first, then the audit row,
inside the same transaction (as the lifting and erasure do with their ids).

## 3. Ports (Application/Ports) — extended

```csharp
public interface IErasedParticipantRepository
{
    // existing members unchanged; GetExcludedGoogleUserIdsAsync ignores rows with no Google id (SQL "= ANY" never matches NULL)

    /// True when any exclusion has this fingerprint (entry, "already excluded").
    Task<bool> AnyWithFingerprintAsync(EmailFingerprint fingerprint, CancellationToken cancellationToken);

    /// Exclusions with no Google id whose fingerprint is among the candidates (sync match).
    Task<IReadOnlyList<UnmatchedExclusion>> GetUnmatchedByFingerprintsAsync(
        IReadOnlyCollection<EmailFingerprint> candidates,
        CancellationToken cancellationToken);

    /// The one fill; false when the row is gone or already has an id (guarded UPDATE, db-design §4 step 6).
    Task<bool> FillGoogleUserIdAsync(long exclusionId, string googleUserId, CancellationToken cancellationToken);
}

// Application/Models — a read model, not the entity
public sealed record UnmatchedExclusion(long Id, EmailFingerprint Fingerprint);
```

The "already loaded" lookup and the stored-id guard (indicative names):

```csharp
// erasure-page read side; returns the US-038 search result DTO shape, ≤ 51 rows to detect truncation
Task<IReadOnlyList<ParticipantSearchResult>> FindParticipantsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

// synchronization; which of these Google ids are stored ClassroomParticipants
Task<IReadOnlySet<string>> GetStoredGoogleUserIdsAsync(IReadOnlyCollection<string> googleUserIds, CancellationToken cancellationToken);
```

They go on whichever existing port already serves the erasure search and the
sync participant writes; IMPLEMENTATION picks the port, not new behaviour.

`IErasureFence`, `IErasureSaltStore` — unchanged; the entry uses
`EnterAsErasureAsync` and `GetOrCreateAsync`.

## 4. Use case and DTOs (Application)

```csharp
// UseCases — indicative name (spec FR-001)
public sealed class EnterErasureExclusionUseCase
{
    Task<ExclusionEntryResult> ExecuteAsync(long adminId, string? email, string? requestId, CancellationToken cancellationToken);
}

// Models
public enum ExclusionEntryOutcome { Entered, AlreadyLoaded, AlreadyExcluded, Invalid }
public sealed record ExclusionEntryResult(
    ExclusionEntryOutcome Outcome,
    LiftFieldError? FieldError,                              // API: EmailFieldError values
    IReadOnlyList<ParticipantSearchResult> LoadedParticipants,
    bool LoadedTruncated);

// Models/Dtos
public enum ErasureMessageKey { Erased, ExclusionLifted, ExclusionEntered }
public sealed record EntryBlock(
    string? Email, LiftFieldError? FieldError, ExclusionEntryOutcome? Outcome,
    IReadOnlyList<ParticipantSearchResult> LoadedParticipants, bool LoadedTruncated);
// ErasurePageModel gains: EntryBlock Enter
```

Read-only refusal is the `ReadOnlyModeException` from the guard (→ `409`), not
an outcome value, as in the lifting.

## 5. Mapping

| Business concept | Entity / column | API DTO |
|---|---|---|
| Exclusion entered in advance | `ErasedParticipant` (`google_user_id NULL`, fingerprint) | none — never shown (spec S-10) |
| Matched exclusion | same row, `google_user_id` filled | none |
| "Already loaded" participants | `ClassroomParticipant` by `lower(email)` | `EntryBlock.loadedParticipants` (`ParticipantSearchResult`) |
| Entry outcome | — | `EntryBlock.outcome`, `ErasureMessageKey.ExclusionEntered` |
| Audit of the entry | `AuditEvent` `erasure_exclusion_entered`, target `erased_participant` id | none |
