---
artifact_type: test_generation_report
story: US-051
version: 1
status: DRAFT
created_at: 2026-10-15T21:20:00Z
updated_at: 2026-10-15T21:20:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-051-spec.md
    version: 1
  - path: docs/designs/database/US-051-db-design.md
    version: 1
  - path: docs/tests/US-051-test-strategy.md
    version: 1
  - path: docs/tests/US-051-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-051 Test Generation Report

## 1. Files

**Created (tests):**
- `tests/ClassroomAgent.Tests/Web/UseCases/DeanGoogleSignInRefusalTests.cs` — 15 cases
- `tests/ClassroomAgent.Tests/Application/UseCases/GoogleFirstSignInRaceTests.cs` — 3 cases
- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/NotAnAdminAccountRefusalTests.cs` — 4 cases
- `tests/ClassroomAgent.Tests/Web/Localization/NotAdminAccountTranslationTests.cs` — 4 cases

**Modified (tests):**
- `tests/ClassroomAgent.Tests/TestInfrastructure/SignInTestData.cs` — `TextKeys.RefusedNotAdminAccount`, `RefusalCategories.NotAnAdminAccount` (not added to `TextKeys.All`, the US-008 contract list).

## 2. Compile-only skeleton

| Declaration | File |
|---|---|
| `AuditRefusalCategory.NotAnAdminAccount` (appended after `LockedOut`, db-design §2) | `src/ClassroomAgent.Domain/Enums/AuditRefusalCategory.cs` |
| `SignInRefusal.NotAnAdminAccount` (spec FR-001) | `src/ClassroomAgent.Application/Models/SignInRefusal.cs` |

Enum members only: no mapping code, no translation entry, no migration, no log line,
no DI. The existing switches (`RefusalCategoryCode`, `TextKey`, `SignInLog.Refuse`)
throw on the new members until IMPLEMENTATION adds them.

## 3. Commands and delegation

Test design, the four test classes and the skeleton were written inline. The build
and both test runs were delegated to a `cheap-worker` subagent (no files touched):

- `dotnet build ClassroomAgent.sln` — succeeded, 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~DeanGoogleSignInRefusalTests|FullyQualifiedName~GoogleFirstSignInRaceTests|FullyQualifiedName~NotAnAdminAccountRefusalTests|FullyQualifiedName~NotAdminAccountTranslationTests"` — 26 cases: 10 passed, 16 failed.
- `dotnet test ClassroomAgent.sln` — 5962 total: 5946 passed, 16 failed, 0 skipped. The 16 failures are exactly the new red tests; no existing test fails.

## 4. Expected failures (red, missing production behaviour)

| Test | Failure | Missing behaviour |
|---|---|---|
| R `AnApprovedDeanAccount_IsRefused_AndSeesTheDeanAccountMessage` | Location `/` instead of `/sign-in` | FR-001: Dean is admitted today |
| R `TheRefusal_LeavesTheDeanAccountExactlyAsItWas` | Dean `concurrency_stamp` changed | FR-002: Dean is stamped today |
| R `TheRefusal_IsAuditedAgainstTheDeanAccount_WithTheNewCategory` | outcome `succeeded` | FR-003 |
| R `ADisabledDeanAccount_IsRefusedAsNotAnAdminAccount_NotAsDisabled` | `account_disabled` | FR-004 order |
| R `InReadOnlyMode_TheDeanAccountIsRefusedAndAuditedTheSameWay` ×3 | Location `/` | FR-001/FR-003 in read-only mode |
| R `TheRefusal_IsLoggedAsAWarningWithItsOwnEvent_AndWithoutTheEmail` | level `Information` (the success line) | FR-007 |
| G `ADeanCreatedInTheMeantime_IsRefusedAsNotAnAdminAccount` | refusal `null` (signed in) | FR-005 |
| G `ADisabledAdminCreatedInTheMeantime_IsRefusedAsDisabled` | refusal `null` (signed in) | FR-005 |
| P `TheNewCategory_IsAccepted` | 23514 `ck_audit_event_refusal_category_value` | FR-008 migration |
| P `TheCategory_RoundTripsThroughTheMapping` | `ArgumentOutOfRangeException` in `RefusalCategoryCode` | db-design §2 mapping |
| T `TheKey_HasTheSpecifiedTextInEachLanguage` (uk, en) | key missing for uk / en | FR-006 |
| T `TheText_DiffersFromEveryOtherSignInRefusal` (uk, en) | key missing for uk / en | FR-006 |

Every failure compiles, runs in a valid host, and fails on the behaviour its
scenario names.

## 5. Tests that pass before implementation (explained)

Regression guards for behaviour FR-004 / FR-009 require to stay unchanged; they pass
now and must still pass after IMPLEMENTATION:

- R `ADeanAccountNotInAllowedAdmin_KeepsTheNotApprovedRefusal`,
  `ADeanAccount_WhenTheInstallationIsUnknown_KeepsTheUnknownInstallationRefusal`,
  `ADeanAccount_WhenTheControlPlaneDoesNotAnswer_KeepsTheUnavailableRefusal` (AC-003);
- R `AnApprovedAddressWithNoAccount_StillGetsAnAdminAccount`,
  `AnExistingAdminAccount_IsStillAdmitted`, `ADisabledAdminAccount_IsStillRefusedAsDisabled` (AC-004);
- R `AfterTheRefusal_TheDeanStillSignsInWithThePassword_ThroughTheForcedChange` (AC-006) —
  passes today because the Google path does not touch the password state; it would catch an
  implementation that counted the refusal as a failed attempt or locked the account (I-6);
- G `AnAdminCreatedInTheMeantime_IsStillSignedIn` (FR-005 must keep the existing race outcome);
- P `AnUnknownCategory_IsStillRejected` ×2 (the list must stay closed after the migration).

None of the AC-001/AC-002/AC-005/AC-007/AC-008/AC-009 core scenarios passes vacuously.

## 6. Unexpected failures

None.

## 7. Untested Acceptance Criteria

None — AC-001…AC-009 are each mapped in `docs/tests/US-051-ac-test-matrix.md`.

## 8. Open Decisions

None. Note for IMPLEMENTATION (spec review M-4): the log test requires a new event
name, at `Warning`, carrying `RequestId`; the id is free to choose (next free 2205).

## 9. Overall result

PASS — red phase verified; existing suite green.
