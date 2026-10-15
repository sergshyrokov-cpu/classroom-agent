---
artifact_type: implementation_verification
story: US-048
version: 1
status: APPROVED
created_at: 2026-10-15T21:00:00Z
updated_at: 2026-10-15T21:00:00Z
produced_by: implementation-verifier
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
  - path: docs/evidence/US-048-test-generation-report.md
    version: 1
  - path: docs/evidence/US-048-implementation-report.md
    version: 1
supersedes: null
verdict: PASS
---

# US-048 Implementation Verification

## 1. Summary

**PASS.** 0 Critical, 0 Major, 2 Minor. The code does what spec v1 says: the count returns to zero in the save that
starts a lockout (FR-001); an `AppUser` concurrency conflict is translated in `UnitOfWork` to
`AppUserConflictException` after the stale account entry and staged audit rows are detached (FR-005); the Dean
sign-in re-runs on a fresh read and clock at most three times (FR-003, FR-004); a re-run reuses the password
verification on an unchanged hash (FR-006). The tests prove the Acceptance Criteria against the real concurrency
token with a controlled interleaving. Spec-review M-3 is confirmed: other `AppUser` writers still end on the error
page.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-048-dean-lockout-counter-reset.md` | — |
| Specification | `docs/specifications/US-048-spec.md` | 1 (APPROVED) |
| Open Decisions | `docs/decisions/US-048-open-decisions.md` | 1 |
| API design | `docs/designs/api/US-048-api-design.md` | 1 (NOT_APPLICABLE; no openapi) |
| DB design | `docs/designs/database/US-048-db-design.md` | 1 (NOT_APPLICABLE; no entity model) |
| Test strategy / AC matrix / test generation report | `docs/tests/US-048-*.md`, `docs/evidence/US-048-test-generation-report.md` | 1 / 1 / 1 |
| Implementation report | `docs/evidence/US-048-implementation-report.md` | 1 |

The implementation report's inputs name the current versions; nothing is stale.

## 3. AC Verification

| AC | Tests | Verdict |
|---|---|---|
| AC-001 | `DeanLockoutCounterResetTests.TheFifthFailure_LocksAndReturnsTheCountToZero`, `DuringTheLockout_TheCorrectPasswordIsRefused_AndTheCountStaysZero`, `RecordFailedSignIn_AtTheMaximum_…` | Proven: count 0 and lockout end asserted; would fail without the new line (red phase recorded). |
| AC-002 | `AfterTheLockout_OneWrongPassword_DoesNotLockAgain` | Proven: category, count 1, not locked, then correct password signs in. |
| AC-003 | `AfterTheLockout_FourFailures_DoNotLock`, `AfterTheLockout_FiveMoreFailures_LockAgain_AndTheLockLiftsByItself` | Proven, including expiry by moving the clock. |
| AC-004 | US-012 regression classes inside the full suite (impl report: 5936/5936) | Accepted on the reported run; `security-reviewer` reruns the full suite. |
| AC-005 | `DeanSignInConcurrencyTests.TwoWrongPasswordsAtOnce_…`, `InReadOnlyMode_TheReRunStillSaves` | Proven at the use-case level: no exception, both `WrongPassword` (the page maps it to the common message, US-012), 2 reads, 2 rows, count 2. Real PostgreSQL, real stamp. |
| AC-006 | `AConflictAtTheFifthFailure_EndsInTheLockoutRefusal` | Proven: competing row `wrong_password`, re-run row `locked_out`, count 0, locked. |
| AC-007 | `TheReRun_SeesWhatTheOtherWriterSaved` | Proven with the real `SetDeanAccountStateUseCase` as the other writer; one sign-in row. |
| AC-008 | `ThreeConflictsInARow_AreNotHidden`, `TwoConflicts_TheThirdRunSaves` | Proven: 3 reads, `AppUserConflictException`, no row, count untouched; two conflicts absorbed with one row. |
| AC-009 | `AReRunOnAnUnchangedHash_DoesNotVerifyAgain`, `AReRunOnAChangedHash_VerifiesAgain` | Proven: 1 vs 2 verifications via `CountingPasswordHasher`; the changed hash comes from the real `ResetDeanPasswordUseCase`. |

No test asserts on a stub instead of the system: the interleaving wrapper only delays the return of the real
repository's read; saves go through the real `UnitOfWork` to PostgreSQL.

## 4. Specification Coverage

| Requirement | Where | Verdict |
|---|---|---|
| FR-001 | `AppUser.RecordFailedSignIn` | implemented; policy numbers stay in `SignInDeanUseCase` |
| FR-002 | sequence order untouched; diff only extracts `RunAsync` and swaps the verify calls | implemented |
| FR-003 | `SignInDeanUseCase.ExecuteAsync` loop; `RunAsync` reads the clock and the account each run | implemented |
| FR-004 | `MaximumRuns = 3`, filter `run < MaximumRuns` | implemented |
| FR-005 | `UnitOfWork` `AppUser` clause + `DetachFailedAppUserEntries`; `AppUserConflictException` in `Application` | implemented (see Minor N-2) |
| FR-006 | private `PasswordCheck` keyed on the hash string | implemented; step-1 timing verification unaffected |
| FR-007 | filter `!cancellationToken.IsCancellationRequested` | implemented (see Minor N-1) |
| VR-001, VR-002 | guard kept; count ≤ 4 asserted across three lockouts | implemented |
| §7 read-only | every commit still under `PermittedServiceWrite.SignInBookkeeping` | implemented, tested in read-only mode |

No behaviour outside the artifacts was found.

## 5. Design Conformance

API and DB designs are NOT_APPLICABLE and hold: no endpoint, route, page, schema or migration changed.
AD-3/AD-4: `Application` sees only `AppUserConflictException`; no EF Core or Npgsql type crosses. AD-6: no read-only
guard involved (sign-in bookkeeping, BR-026). AD-8: no controller or view model touched.

## 6. Deviations

None from the artifacts. Spec-review **M-3 checked**: the new clause catches every `AppUser` concurrency conflict.
A search of `src/` finds no Data Plane code catching `DbUpdateConcurrencyException` or `AppUserConflictException`
other than the sign-in loop (the Control Plane's `OwnerSignInService` uses its own `DbContext`, not this
`UnitOfWork`), and the `catch (Exception)` sites (synchronization, retention purge, legitimacy, readiness) do not
write `AppUser`. So the other writers — Admin reset, disable/re-enable, password change, language choice — still end
on the host's error page on a conflict, as before; only the exception type and the prior detach differ. Report §7
states this correctly.

## 7. Report Accuracy

The change set in report §4 equals the working tree (`git status`; untracked files checked by listing, not
`git grep`): 4 production files, 3 test classes, 2 fixtures, 4 workflow artifacts. `.claude/settings.local.json` is
modified but correctly declared out of scope (not to be committed). Every AC-to-test location in report §3 exists.
No `TODO`, `TBD`, `FIXME`, `NotImplementedException` or skipped test in the changed files; the skeleton exception is
complete.

## 8. Story Test Run

Delegated to `cheap-worker` (Sonnet; no file touched, facts reported verbatim):
`dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~DeanLockoutCounterResetTests|FullyQualifiedName~DeanSignInConcurrencyTests|FullyQualifiedName~AppUserConflictTranslationTests"`
→ total 19, passed 19, failed 0, skipped 0; no build warnings in the output. The full suite was not rerun here.

## 9. Findings

- **N-1 (Minor)** — `SignInDeanUseCase.ExecuteAsync`, filter `!cancellationToken.IsCancellationRequested`
  (spec FR-007). `ACancelledRequest_IsNotRunAgain` cancels before the save, so `SaveChangesAsync` throws
  `OperationCanceledException` before reaching the database; the filter itself is never exercised (the test would
  pass without it). The matrix already records the test as a regression guard (green in the red phase). The path the
  filter covers — cancellation landing after the conflict was raised — is narrow and only changes whether a third read
  is made. Advisory; no loop-back.
- **N-2 (Minor)** — `UnitOfWork.DetachFailedAppUserEntries` detaches every `Added` `AuditEvent` in the scope, not
  only "the run's" (FR-005 wording). In the sign-in scope the only staged row is the run's, so the effect is the same;
  for other writers the exception propagates to the error page anyway. Same pattern as US-032 security INFO (broad
  audit detach). Advisory; no loop-back.

## 10. Verdict

**PASS** — next stage SECURITY_REVIEW.
