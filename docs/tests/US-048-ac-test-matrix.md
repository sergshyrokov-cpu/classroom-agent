---
artifact_type: ac_test_matrix
story: US-048
version: 1
status: DRAFT
created_at: 2026-10-15T19:40:00Z
updated_at: 2026-10-15T19:40:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-048-spec.md
    version: 1
  - path: docs/designs/api/US-048-api-design.md
    version: 1
  - path: docs/designs/database/US-048-db-design.md
    version: 1
  - path: docs/tests/US-048-test-strategy.md
    version: 1
supersedes: null
---

# US-048 Acceptance Criteria → Test Matrix

Classes: **R** = `ClassroomAgent.Tests.Application.UseCases.DeanLockoutCounterResetTests` (unit);
**C** = `ClassroomAgent.Tests.Application.UseCases.DeanSignInConcurrencyTests` (integration, PostgreSQL);
**T** = `ClassroomAgent.Tests.Infrastructure.Persistence.AppUserConflictTranslationTests` (integration, PostgreSQL).
Status is the red-phase state recorded in the test generation report.

| Acceptance Criterion | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | P-1 five failures lock, count 0 | Unit | R | `TheFifthFailure_LocksAndReturnsTheCountToZero` | locked until now+15 min, count 0 | RED |
| AC-001 | N-1 during lockout correct password refused at step 2 | Unit | R | `DuringTheLockout_TheCorrectPasswordIsRefused_AndTheCountStaysZero` | `LockedOut` ×2, rows "locked out", count 0 | RED |
| AC-001 / FR-001 | B-3 entity zeroes count at the caller's maximum | Unit | R | `RecordFailedSignIn_AtTheMaximum_StartsTheLockoutAndZeroesTheCount` | count 0, lockout set | RED |
| AC-002 | P-2 one wrong password after lockout, then correct | Unit | R | `AfterTheLockout_OneWrongPassword_DoesNotLockAgain` | `WrongPassword`, row "wrong password", count 1, not locked; then `SignedIn` | RED |
| AC-003 | B-1 four failures after lockout do not lock | Unit | R | `AfterTheLockout_FourFailures_DoNotLock` | count 4, not locked | RED |
| AC-003 | P-3 five more lock again; lock lifts by itself | Unit | R | `AfterTheLockout_FiveMoreFailures_LockAgain_AndTheLockLiftsByItself` | locked now+15 min, count 0; after 15 min sign-in proceeds | RED |
| AC-004 | US-012 sequence unchanged | Unit / Integration | `DeanSignInSequenceTests`, `DeanLockoutTests`, `DeanAccountAdministrationTests`, `DeanPasswordChangeTests`, `PermittedServiceWriteTests`, `DeanSignInPageTests` | all | stay green | GREEN (regression) |
| AC-005 | P-4 two wrong passwords at once | Integration | C | `TwoWrongPasswordsAtOnce_BothGetTheCommonRefusal_AndTheCountIsTwo` | both `WrongPassword`, 2 runs, 2 rows "wrong password", count 2 | RED |
| AC-006 | N-2 conflict at the fifth failure | Integration | C | `AConflictAtTheFifthFailure_EndsInTheLockoutRefusal` | competing `WrongPassword`, under test `LockedOut`; count 0, locked | RED |
| AC-007 | N-3 conflict with an Admin disable | Integration | C | `TheReRun_SeesWhatTheOtherWriterSaved` | `AccountDisabled`, one sign-in row, no success recorded | RED |
| AC-008 | N-4 three conflicts propagate | Integration | C | `ThreeConflictsInARow_AreNotHidden` | `AppUserConflictException`, 3 runs, no row, count 0 | RED |
| AC-008 / FR-004 | P-5 two conflicts absorbed | Integration | C | `TwoConflicts_TheThirdRunSaves` | `WrongPassword`, 3 runs, one row, count 1 | RED |
| AC-009 | S-1 unchanged hash: one verification | Integration | C | `AReRunOnAnUnchangedHash_DoesNotVerifyAgain` | 2 runs, 1 verification | RED |
| AC-009 | S-1 changed hash: verified again | Integration | C | `AReRunOnAChangedHash_VerifiesAgain` | 2 runs, 2 verifications, one row, count 1 | RED |
| AC-005 / BR-026 | S-4 re-run in read-only mode | Integration | C | `InReadOnlyMode_TheReRunStillSaves` | `WrongPassword`, 2 rows, count 2 | RED |
| FR-007 | N-5 cancelled request not re-run | Integration | C | `ACancelledRequest_IsNotRunAgain` | `OperationCanceledException`, 1 run, no row | GREEN (regression guard) |
| FR-005 | D-1 stale save translated | Integration | T | `AStaleAppUserSave_IsAnAppUserConflict` | `AppUserConflictException` | RED |
| FR-005 | D-2 clean-up after the conflict | Integration | T | `AfterTheConflict_TheNextReadReturnsTheCurrentRow_AndNoStagedAuditRowSurvives` | re-read count 3; only "r-kept" row; count 4 | RED |
| VR-001 | V-1 maximum below 1 rejected | Unit | R | `RecordFailedSignIn_WithAMaximumBelowOne_IsRejected` | `ArgumentOutOfRangeException` | GREEN (regression) |
| VR-002 | B-2 count ≤ 4 without a lockout across three lockouts | Unit | R | `AcrossSeveralLockouts_TheCountNeverExceedsFourWithoutALockout` | 1,2,3,4,0 per lockout | RED |

Every Acceptance Criterion AC-001…AC-009 has at least one mapped test.
