---
artifact_type: test_strategy
story: US-065
version: 1
status: DRAFT
created_at: 2026-10-28T12:13:33Z
updated_at: 2026-10-28T12:13:33Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-065-erasure-exclusion-by-email-in-advance.md
    version: null
  - path: docs/specifications/US-065-spec.md
    version: 2
  - path: docs/designs/api/US-065-api-design.md
    version: 1
  - path: docs/designs/api/US-065-openapi.yaml
    version: 1
  - path: docs/designs/database/US-065-db-design.md
    version: 1
  - path: docs/designs/database/US-065-entity-model.md
    version: 1
supersedes: null
---

# US-065 Test Strategy — An Admin enters an erasure exclusion by email before the person is loaded

## 1. Scope

Entering an exclusion by email (one `POST`, one use case), the synchronization
match by fingerprint and the one fill of the Google user id, the schema change
(nullable id, value check, the fill-only trigger, the new audit action), and the
unchanged Meet load and lifting as they meet an exclusion entered in advance.
Out of scope: everything spec §10 lists.

## 2. Levels

| Level | Class (namespace `ClassroomAgent.Tests`) | Why this level |
|---|---|---|
| Unit (Application, every port substituted — TC-1) | `Application.UseCases.EnterErasureExclusionUseCaseTests` | Evaluation order (guard → email → transaction { fence → loaded → salt → excluded → insert + audit }), outcomes, read-only refusal in Application (TC-5), shared VR-001 with the lifting |
| Persistence (migrated PostgreSQL — TC-2) | `Infrastructure.Persistence.ErasureExclusionInAdvanceSchemaTests` | Nullable id, `ck_erased_participant_values`, trigger `tg_erased_participant_fill_only` (exists; one fill only — DB_DESIGN N-1), audit action and shape check, migration applied |
| Integration (host container + PostgreSQL) | `Web.UseCases.ErasureExclusionEntryTests` | Real read source and salt store: fingerprint with the stored salt, "already loaded" in any letter case on or off a roster, "already excluded", entry then lifting, the entry waiting on a course transaction's shared fence |
| Integration (background synchronization + PostgreSQL, Google ports substituted — TC-4) | `Web.UseCases.ErasureExclusionInAdvanceSyncTests` | Match, skip, fill, owner, renamed address, I-1, I-2, FR-008, lifting after the match, the run waiting on the entry's exclusive fence with and without a prior salt, the Meet load, no personal data in the log |
| HTTP | `Web.Pages.ErasureExclusionEntryPageTests` | The block, PRG and message, the three re-renders, `400` with the value kept, read-only notice and disabled button |
| HTTP / security | `Web.Security.ErasureExclusionEntryAccessTests` | Admin allowed; Dean `403`; Dean on the temporary password → change; anonymous → sign-in; policy on the endpoint; antiforgery `400`; read-only `409` with one refused audit row; GET stores nothing |
| HTTP / localization | `Web.Localization.ErasureExclusionEntryTranslationTests` | Every new key in `uk` and `en`; the block and the outcomes in both languages |
| HTTP / logging | `Web.Logging.ErasureExclusionEntryLoggingTests` | The entered email and the fingerprint never reach the log (SC-10) |

Existing tests adapted to the approved design (they turn green only with the
migration): `ParticipantErasureSchemaTests.TheExclusionTable_HasExactlyTheDesignedColumns`
(`google_user_id` nullable), `ParticipantErasureSchemaTests.AnExclusion_CannotBeChanged_ButCanBeDeleted`
(the rule is now the trigger, so the error is `check_violation` with no
constraint name), `PermissionMatrixTests` (new row
`POST settings/erasure/exclusions` → `EraseParticipantData`).

## 3. Scenarios

**Positive.** Entry on an empty installation (no connection, no salt) stores one
exclusion with the fingerprint and no id, creates the salt, writes one audit row
(AC-001, AC-002). PRG with the one-time message, email not echoed (FR-003). The
first run skips the matched student entirely and fills the id (AC-003); the
owner on the roster is cleared (AC-013); after the match a renamed address does
not bring the student back (AC-004); the Meet load skips by fingerprint before
and after the match (AC-005); entry then lifting, and lifting after the match
then a run that loads the person (AC-008, AC-021).

**Negative.** "Already loaded" (stored email in another case, on or off a
roster) — listed in the US-038 search shape, nothing written, salt untouched
(AC-006, AC-014). "Already excluded" for an exclusion from an erasure or entered
in advance (AC-007). Loaded and excluded at once → "already loaded" (I-5). An
exclusion of another address or without a fingerprint does not count (FR-005).
An erasure-created exclusion is not matched by fingerprint and not changed
(AC-015, I-1). An id already held by another exclusion → skipped, entry left
without id, run completes (AC-016). An id already stored as a participant →
synchronized as usual, exclusion left without id, re-entry answers "already
loaded" (AC-017, OD-003 a). A roster entry without an email is not matched
(VR-004). A participant whose email merely contains the address is not "loaded".

**Boundary.** 254 characters with surrounding spaces accepted, 255 rejected;
more than 50 loaded participants → 50 and the truncation flag; two roster
entries with one fingerprint → both skipped, the ordinally first id fills (I-2).

**Validation (VR-001).** Required (null, empty, blank), too long, malformed
(six shapes) — each the same field error as the lifting gives (one shared
rule); `400` re-render with the value kept only in the entry field; nothing
stored, no audit; any domain accepted (OD-001 a).

**Security.** Admin only; Dean `403`; temporary-password Dean → change;
anonymous → sign-in; the endpoint declares `EraseParticipantData` and is in the
permission matrix; antiforgery required (AC-018); read-only refused in
Application for all three BR-025 causes with one refused audit row and nothing
else, the guard before validation (AC-009); `409` over HTTP; no email,
fingerprint or Google id in an audit row or a log line (S-06); email only in the
body (S-08).

**Persistence.** See the schema class above, and FR-011 serialization both ways:
the entry waits for a course transaction holding the shared fence and answers
"already loaded"; the run's course transaction waits for the entry's exclusive
fence and sees the exclusion — also when the entry created the salt (AC-012).

## 4. Markup hooks (rendered exactly by IMPLEMENTATION)

| Id / attribute | Element |
|---|---|
| `erasure-enter-form` | The form posting to `/settings/erasure/exclusions`; field `email`; its submit button carries `disabled` in read-only mode |
| `erasure-enter-error` | The VR-001 field error of the entry form |
| `erasure-already-loaded` | The "already loaded — erase the ordinary way" message |
| `erasure-loaded-results` | The list of loaded participants; each entry carries `data-participant-id="<id>"` and links to `/settings/erasure/participants/<id>` |
| `erasure-loaded-truncated` | More than 50 loaded participants |
| `erasure-already-excluded` | The "already excluded" message |
| `erasure-message` | Reused: the one-time message (`ExclusionEntered`) |
| `erasure-read-only-notice` | Reused: the page's read-only notice |

The entered email appears in the entry field only in a `400` response
(api-design §2.4).

## 5. Translation keys

`Erasure.Enter.Title`, `Erasure.Enter.Explanation`, `Erasure.Enter.Label`,
`Erasure.Enter.Button`, `Erasure.Enter.AlreadyLoaded`,
`Erasure.Enter.AlreadyExcluded`, `Erasure.Enter.Truncated`,
`Erasure.Message.ExclusionEntered`. The field errors reuse
`Erasure.Lift.Error.EmailRequired|EmailTooLong|EmailInvalid` (one shared
VR-001, one set of `EmailFieldError` values), the notice reuses
`Erasure.ReadOnlyNotice`.

## 6. Fixed names

Operation name for the guard: `ErasureExclusion.Enter`. Audit code
`erasure_exclusion_entered`; check `ck_audit_event_exclusion_entry_shape`;
trigger `tg_erased_participant_fill_only` calling `erased_participant_fill_only()`;
migration `*_AddErasureExclusionInAdvance`.

## 7. Fixtures

`ErasureUnitWorld` (gains `Enter`, the "loaded" lookup and `AnyWithFingerprintAsync`);
`SyncWorld.NoErasures` (new members); `ErasureHostExtensions`
(`SeedExclusionInAdvanceAsync`, `EnterExclusionAsync`, `PostEntryAsync`,
`ExclusionRow.GoogleUserId` nullable, the audit query includes the entry);
`ErasureTestData` (path, pattern, markup, keys, codes, names). Exclusions entered
in advance are seeded by SQL with the known salt, independently of the code
under test. Synthetic people and addresses only (TC-4).

## 8. Excluded scenarios

- An owner not on the course's roster (spec §10, I-4) — not matchable by design.
- A failure of the fill inside a course transaction — no new handling (spec §8).
- The Down migration — not exercised by the suite (as for every earlier migration).
- Carrying exclusions between installations, a list of exclusions, bulk entry — out of scope.

## 9. Known limitations and Open Decisions

None open; OD-001 … OD-003 resolved. FR-008's known limitation (Classroom keeps
loading until the Admin erases) is tested as behaviour (AC-017); its wording in
`trebovaniya.md` is the deferred SPEC_REVIEW m-1.
