---
artifact_type: implementation_verification
story: US-065
version: 1
status: APPROVED
created_at: 2026-10-28T13:51:29Z
updated_at: 2026-10-28T13:51:29Z
produced_by: implementation-verifier
inputs:
  - path: docs/stories/US-065-erasure-exclusion-by-email-in-advance.md
    version: null
  - path: docs/specifications/US-065-spec.md
    version: 2
  - path: docs/decisions/US-065-open-decisions.md
    version: 2
  - path: docs/designs/api/US-065-api-design.md
    version: 1
  - path: docs/designs/api/US-065-openapi.yaml
    version: 1
  - path: docs/designs/database/US-065-db-design.md
    version: 1
  - path: docs/designs/database/US-065-entity-model.md
    version: 1
  - path: docs/tests/US-065-test-strategy.md
    version: 1
  - path: docs/tests/US-065-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-065-test-generation-report.md
    version: 1
  - path: docs/evidence/US-065-implementation-report.md
    version: 1
supersedes: null
verdict: PASS
---

# US-065 Implementation Verification

## 1. Summary

**PASS.** The code does what Specification v2 and the designs say. No Critical
or Major finding. Two Minor findings (m-1 migration Down, m-2 duplicated cap).
All six disclosed deviations (D-1 … D-6) are sound. The 20 Story and adapted
test classes ran green here: 361 of 361.

Judged from files and the working tree only, in a fresh context.

## 2. Reviewed Artifacts

All inputs are listed in the front matter with their versions. None is
`SUPERSEDED`. The implementation report names the current Specification (v2)
and the current designs (v1). The database design and entity model are `DRAFT`:
this workflow variant has no design reviewer, so DRAFT is their final state.

## 3. AC Verification

| AC | Tests read | Verdict |
|---|---|---|
| AC-001 | U `WithNoSaltYet_…`, `TheEntry_WritesOneAuditRow_…`; EN `OnAnEmptyInstallation_…`; P `AnEntry_RedirectsToThePage_…` | Proven: row with fingerprint and null id, audit row with target id, PRG |
| AC-002 | P `ThePage_ShowsTheEntryForm_WithNoGoogleConnectionSaved`; EN empty installation | Proven |
| AC-003 | SY `TheFirstRun_StoresNothingOfTheExcludedStudent_…` | Proven: 0 participant/email/submission of the target. The other student and the teacher are stored. The row is filled and its other columns unchanged |
| AC-004 | SY `AfterTheMatch_ARenamedAddress_…` | Proven: second run with a renamed address; row identical |
| AC-005 | SY `TheMeetLoad_…(onARoster)` both cases | Proven: before and after the match |
| AC-006 | U loaded cases, truncation; EN real read source; P list with links | Proven |
| AC-007 | U both exclusion kinds, other address, no fingerprint; EN; P | Proven |
| AC-008 | U lifting after entry; SY `ALiftingAfterTheMatch_…` | Proven: next run loads the person as new |
| AC-009 | U read-only three causes, guard before validation; A `409` cases | Proven in `Application` (AD-6) |
| AC-010 | A Admin / Dean / temporary password / anonymous / policy / GET; PM | Proven |
| AC-011 | U required / length / malformed / shared rule; P `400`; L value not logged | Proven |
| AC-012 | EN `AnEntryDuringACourseWrite_…`; SY `AnEntryCommittedWhileTheRunWaitsOnTheFence_IsSeenAtTheWrite(saltBefore)`; U order | Proven. The `saltBefore: false` case would fail if the salt were read once per run before the course transaction, so the test is not vacuous (see D-4) |
| AC-013 | SY `TheCoursesOwner_MatchedOnItsRoster_…` | Proven: owner null |
| AC-014 | U normalized lookup; EN mixed-case stored email; SY `GooglesAddressInAnotherLetterCase_StillMatches` | Proven. `EmailFingerprint.Of` normalizes internally, so the sync side agrees with the entry |
| AC-015 | SY erasure exclusion not matched and not changed; DB trigger cases | Proven |
| AC-016 | SY `AMatchedIdAlreadyHeldByAnotherExclusion_…`, `TwoEntriesWithOneFingerprint_…`; DB unique index | Proven |
| AC-017 | SY `AMatchedIdAlreadyStoredAsAParticipant_…` | Proven: participant synchronized, row without id, re-entry answers AlreadyLoaded |
| AC-018 | A `WithoutAnAntiforgeryToken_…` | Proven |
| AC-019 | P read-only notice and `disabled`; enabled outside read-only mode | Proven |
| AC-020 | T keys in both languages; page and outcomes rendered in uk/en | Proven |
| AC-021 | U and EN `AfterTheFirstEntry_ALiftingOfTheSameEmail_RemovesIt` | Proven |

The tests that pass by design (test generation report §5) each assert a
property that still has to hold after implementation. They are not counted as
the only proof of any AC.

## 4. Specification Coverage

- FR-001 … FR-005, FR-011 … FR-013: `EnterErasureExclusionUseCase` follows FR-002
  in order. The guard comes first, with the refusal audited under
  `PermittedServiceWrite.AuditEvent`. Validation is shared with the lifting
  (`LiftErasureExclusionUseCase.Validate`, now `internal`). In one transaction:
  the exclusive fence, the loaded check, `GetOrCreateAsync`, the fingerprint
  check, then the insert and the audit row. "Already loaded" and "already
  excluded" leave through a private exception, so the transaction rolls back.
  `EraseParticipantUseCase.ParticipantGoneException` is the precedent.
- FR-006 … FR-008: `RunSynchronizationUseCase.AddMatchedInAdvanceAsync`, inside
  `ImportCourseAsync` after the shared fence and after
  `GetExcludedGoogleUserIdsAsync`. Roster entries with no email are not
  candidates (VR-004). The fill is per exclusion, in id order, ordinally first
  id, skipped for ids already held. The stored-id guard follows FR-008. The
  returned set feeds the existing owner, membership and submission skips.
- FR-009, FR-010: unchanged code paths. `GetExcludedGoogleUserIdsAsync` now
  ignores null ids.
- FR-014: controller-level `EraseParticipantData` policy, route
  `/settings/erasure/exclusions`.
- FR-015: 8 new keys plus the extended read-only notice, in uk and en.
- Out-of-scope behaviour: none found.

## 5. Design Conformance

- **OpenAPI.** Route, `POST` only, antiforgery, `302` to `/settings/erasure` with
  `ExclusionEntered`, `200` for both "nothing" outcomes with the field empty,
  `400` keeping the typed value, `409` from the host handler. `EntryBlock` and
  `ErasurePageModel.Enter` match the schema.
- **Database.** The column becomes nullable. The values check matches §2.2
  verbatim. The immutability check is dropped. The trigger function's
  conditions match §2.3. The audit action list and
  `ck_audit_event_exclusion_entry_shape` match §6. Both indexes are unchanged.
  The fill is a guarded `ExecuteUpdateAsync` that sets `updated_at` from
  `TimeProvider` (PC-6).
- **Entity model.** `GoogleUserId` is `string?`, and `EnterInAdvance`,
  `ErasureExclusionEntered` and `ErasureRefused` are as specified. The three
  repository members match §3. `FindParticipantsByEmailAsync` is on
  `IParticipantErasureReadSource` and returns 51 rows to detect truncation.
- **Architecture.** AD-3: the controller only maps. AD-4: no Google type is
  involved. AD-6: the guard is in `Application`. AD-8: the view model holds
  DTOs only.

## 6. Deviations

| Id | Departs from | Reason | Verdict |
|---|---|---|---|
| D-1 | `AuditDeletionConfinementTests` (US-038 guard) | db-design §4 step 6 requires `ExecuteUpdateAsync` for the fill | **Sound.** The exemption is one clause for one file. The new test pins that this file names `db.ErasedParticipants` and never `AuditEvent` / `audit_event`. A rename of that file would make the exemption stale but not unsafe, because the file would then be checked again |
| D-2 | four earlier registry tests | a new migration and the trigger | **Sound.** `DataHealthPersistenceTests` drops "ours is last" for "ours is after the then-last one", following the `GradeColumnsPersistenceTests` precedent. The purge test seeds the old row instead of using an `UPDATE` the trigger forbids. No behaviour assertion is weakened |
| D-3 | entity model's indicative `GetStoredGoogleUserIdsAsync` | the existing `GetByGoogleUserIdsAsync` serves it; the entity model left the port to IMPLEMENTATION | **Sound.** The guard is called only when something matched |
| D-4 | db-design §4 step 1, "salt once per run" | the literal reading misses a salt created by an entry the course waited for (spec FR-011) | **Sound.** The spec outranks the design. `AnEntryCommittedWhileTheRunWaitsOnTheFence_IsSeenAtTheWrite(false)` proves it |
| D-5 | US-038 notice text | FR-012 shows the notice with the new block | **Sound** |
| D-6 | db-design §7 Down wording | an explicit refusal instead of the `NOT NULL` failure | **Sound** for unmatched rows; see m-1 for filled rows |

## 7. Report Accuracy

- The change set in report §4 equals `git status` / `git diff` for `src/`. The
  remaining test-file changes come from TEST_WRITING, which is uncommitted and
  traced in the test generation report.
- Every AC location in report §3 exists.
- No secret, generated database file or IDE file is in the change set.
- `TODO` / `TBD` / `FIXME` / `NotImplementedException` / skipped or
  commented-out tests in the changed and new files: none (ripgrep over the
  tracked diff plus untracked files).

## 8. Story Test Run

Delegated to `cheap-worker`: no files touched, facts reported verbatim. Docker
was running.

- `dotnet build ClassroomAgent.sln`: exit 0, 0 warnings, 0 errors.
- Each class was run alone with `dotnet test ClassroomAgent.sln --no-build
  --filter FullyQualifiedName~<Class>`. All exited 0 with no reruns.

| Class | Passed / Total |
|---|---|
| EnterErasureExclusionUseCaseTests | 36/36 |
| ErasureExclusionInAdvanceSchemaTests | 25/25 |
| ParticipantErasureSchemaTests | 29/29 |
| ErasureExclusionEntryTests | 7/7 |
| ErasureExclusionInAdvanceSyncTests | 15/15 |
| ErasureExclusionEntryPageTests | 13/13 |
| ErasureExclusionEntryAccessTests | 13/13 |
| ErasureExclusionEntryTranslationTests | 5/5 |
| ErasureExclusionEntryLoggingTests | 4/4 |
| PermissionMatrixTests | 3/3 |
| LiftErasureExclusionUseCaseTests | 21/21 |
| ErasureExclusionSyncTests | 7/7 |
| ErasureExclusionMeetTests | 5/5 |
| ErasurePageTests | 22/22 |
| ErasureAuthorizationTests | 25/25 |
| ParticipantErasureTests | 20/20 |
| AppUserMigrationTests | 6/6 |
| AuditDeletionConfinementTests | 4/4 |
| DataHealthPersistenceTests | 47/47 |
| InstallationAuditEventSchemaTests | 29/29 |

Total: 361/361. Free RAM was 7.4 GB at the start and 4.7 GB at the end. The
full suite is not rerun here; SECURITY_REVIEW runs it (implementation INFO-1
stays open until then).

## 9. Findings

- **m-1 (Minor): migration Down fails with an unclear error once an exclusion has
  been filled.** `AddErasureExclusionInAdvance.Down` refuses only while a row
  with a null id exists. It then re-adds `ck_erased_participant_immutable`
  (`updated_at = created_at`). A row filled by synchronization has
  `updated_at > created_at`, so the `ADD CONSTRAINT` fails. The migration
  transaction rolls back and nothing is lost, but the error is a generic
  constraint violation instead of the stated refusal. db-design §7 ("restore
  the two checks") does not foresee this. This is advisory: Down is not used in
  operation (DC). The fix, if wanted: extend the refusal to filled rows
  (`updated_at <> created_at`) and the db-design §7 text. Loop-back key if
  acted on: `invalid_database_design`.
- **m-2 (Minor): the 50 cap is written twice.**
  `ParticipantErasureReadSource.MaxEmailMatches = 50` duplicates
  `SearchParticipantsForErasureQuery.MaxResults`, and the uk/en text
  `Erasure.Enter.Truncated` names "50" literally. If one changes, the
  truncation flag and the text drift. Loop-back key if acted on:
  `changes_required`.

On the OPEN findings passed in (none closed here; this stage closes only FIXED or
WITHDRAWN):

- TEST_WRITING INFO-2: still true. The trigger's message
  (`erased_participant allows only the one fill of google_user_id`) is
  distinctive and would let a test tell the trigger from a check. Advisory.
- IMPLEMENTATION D-1 … D-6: judged in §6, all sound. They stay open for the
  Owner at HUMAN_PR_APPROVAL.
- IMPLEMENTATION INFO-1: the Story classes are green here; a single fully green
  full-suite run is still for SECURITY_REVIEW.

## 10. Verdict

**PASS** → SECURITY_REVIEW. Non-blocking: m-1, m-2, and the accepted
deviations D-1 … D-6.
