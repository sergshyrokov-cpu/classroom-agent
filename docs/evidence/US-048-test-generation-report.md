---
artifact_type: test_generation_report
story: US-048
version: 1
status: DRAFT
created_at: 2026-10-15T20:00:00Z
updated_at: 2026-10-15T20:00:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-048-dean-lockout-counter-reset.md
    version: null
  - path: docs/specifications/US-048-spec.md
    version: 1
  - path: docs/decisions/US-048-open-decisions.md
    version: 1
  - path: docs/designs/api/US-048-api-design.md
    version: 1
  - path: docs/designs/database/US-048-db-design.md
    version: 1
  - path: docs/tests/US-048-test-strategy.md
    version: 1
  - path: docs/tests/US-048-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-048 Test Generation Report

## 1. Files

**Created (tests):**
- `tests/ClassroomAgent.Tests/Application/UseCases/DeanLockoutCounterResetTests.cs` — 8 unit tests
- `tests/ClassroomAgent.Tests/Application/UseCases/DeanSignInConcurrencyTests.cs` — 9 integration tests (PostgreSQL)
- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/AppUserConflictTranslationTests.cs` — 2 integration tests
- `tests/ClassroomAgent.Tests/TestInfrastructure/InterleavingAppUserRepository.cs` — read hook + read counter
- `tests/ClassroomAgent.Tests/TestInfrastructure/CountingPasswordHasher.cs` — verification counter

**Modified:** none.

**Compile-only skeleton:**

| Declaration | File |
|---|---|
| `public sealed class AppUserConflictException : Exception` with constructors `()` and `(Exception innerException)`, both throwing `NotImplementedException` | `src/ClassroomAgent.Application/Exceptions/AppUserConflictException.cs` |

Spec FR-005 requires "an exception declared in `Application`" but gives it no name. The name follows the US-032
precedent (`MeetingCodeLinkConflictException`), which the spec cites. This is a naming choice, not a behaviour
decision. Nothing is registered in DI and no existing behaviour changed.

## 2. Commands

Delegated to a `cheap-worker` subagent (Sonnet), which made no code changes:

1. `dotnet build ClassroomAgent.sln` → succeeded, **0 warnings, 0 errors**.
2. `dotnet test ClassroomAgent.sln --no-build --filter "FullyQualifiedName~DeanLockoutCounterResetTests|FullyQualifiedName~DeanSignInConcurrencyTests|FullyQualifiedName~AppUserConflictTranslationTests"`
   → 19 run: **17 failed, 2 passed**, 0 skipped. Docker/Testcontainers available.
3. `dotnet test ClassroomAgent.sln --no-build --filter "…DeanLockoutTests|…DeanSignInSequenceTests|…DeanAccountAdministrationTests|…DeanSignInPageTests|…DeanPasswordChangeTests|…PermittedServiceWriteTests"`
   → 62 run: **62 passed**, 0 failed, 0 skipped.

Test design, the skeleton, the test code and the red-phase verdict were done inline. Only the build and test runs were delegated.

## 3. Red-phase classification

**Expected failures caused by missing production behaviour (17):**

- `DeanLockoutCounterResetTests`, 7 tests. All fail with `Assert.Equal`/`Assert.InRange`. The count after the
  lockout is 5 or 6 where 0, 1 or 4 is expected (for example, `TheFifthFailure_…` expected 0, actual 5, and
  `AfterTheLockout_OneWrongPassword_…` expected 1, actual 6). This is defect F-1 itself: `RecordFailedSignIn` does
  not zero the count.
- `AppUserConflictTranslationTests`, 2 tests. Expected `AppUserConflictException`, actual raw
  `DbUpdateConcurrencyException` ("expected to affect 1 row(s), but actually affected 0"). The `UnitOfWork`
  translation (FR-005) is missing.
- `DeanSignInConcurrencyTests`, 8 tests. Each fails because a raw `DbUpdateConcurrencyException` leaves the use
  case on the first conflict. There is no re-run (FR-003) and no translation (FR-005). This is audit finding I-1,
  the `500`.

Each failure message matches its scenario. None is caused by compilation, fixture or host setup: the hosts
started, seeded and reached the save.

**Passing before implementation (2), explained:**
- `RecordFailedSignIn_WithAMaximumBelowOne_IsRejected` (VR-001): the existing guard is kept unchanged.
- `ACancelledRequest_IsNotRunAgain` (FR-007): today a cancelled request also throws without re-running. It is a
  regression guard for the new retry loop, as the test strategy §6 says.

**Unexpected failures:** none.

## 4. Coverage

All of AC-001…AC-009 are mapped in the `ac_test_matrix`. AC-004's evidence is the existing US-012 suite, which
stays green (62/62). No Acceptance Criterion is untested.

## 5. Open Decisions

None raised. OD-001 (Story) was resolved (a) by the Owner. Spec-review finding M-3 (other `AppUser` writers end on
the error page with the new exception type) is left to IMPLEMENTATION_VERIFICATION, as recorded at
HUMAN_SPEC_APPROVAL.

## 6. Result

**PASS.** Tests compile, the setup is valid, the regression suite is green, and the new behaviour tests fail only
for the missing implementation.
