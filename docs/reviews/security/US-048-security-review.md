---
artifact_type: security_review
story: US-048
version: 1
status: APPROVED
created_at: 2026-10-15T19:43:00Z
updated_at: 2026-10-15T19:43:00Z
produced_by: security-reviewer
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
  - path: docs/evidence/US-048-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-048-implementation-verification.md
    version: 1
  - path: trebovaniya.md
    version: 96
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-048 Security Review — Dean failed-attempt count reset and sign-in conflict re-run

## 1. Executive Summary

**Result: PASS.** 0 Critical, 0 Major, 0 Minor, 3 Informational.

The Story changes the Dean password sign-in (an anonymous, internet-facing page)
in three places: `AppUser.RecordFailedSignIn` zeroes the failed-attempt count
when a lockout begins (SC-2 "5 consecutive failures"); `UnitOfWork` translates a
concurrency-stamp conflict on an `AppUser` row into `AppUserConflictException`
after detaching the failed account entry and staged audit rows; and
`SignInDeanUseCase` re-runs the six-step SC-2 sequence on a fresh read and clock,
at most three runs, reusing the password verification while the stored hash is
unchanged. The SC-2 step order (unknown → lockout → wrong password → disabled →
temporary → success) is preserved in every run; no endpoint, policy, schema,
package or configuration changes. Independent build (0 warnings) and full suite
(5936/5936) are green; story tests 19/19.

Next action: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

See front matter `inputs`. API and DB designs are `NOT_APPLICABLE` records (no
endpoint, schema or migration change). Changed code:

- `src/ClassroomAgent.Domain/Entities/AppUser.cs` (modified)
- `src/ClassroomAgent.Application/UseCases/SignInDeanUseCase.cs` (modified)
- `src/ClassroomAgent.Infrastructure/Persistence/UnitOfWork.cs` (modified)
- `src/ClassroomAgent.Application/Exceptions/AppUserConflictException.cs` (new)
- tests: `DeanLockoutCounterResetTests`, `DeanSignInConcurrencyTests`,
  `AppUserConflictTranslationTests`, `TestInfrastructure/CountingPasswordHasher`,
  `TestInfrastructure/InterleavingAppUserRepository` (new)

## 3. Security-Relevant Scope

- **Host:** installation (Data Plane). Dean sign-in page — anonymous by SC-4
  (closed list), protected by the SC-2 lockout. No Control Plane change.
- **Assets:** Dean password hash, lockout state (`AccessFailedCount`,
  `LockoutEnd`), audit rows of sign-in outcomes.
- **Trust boundaries:** browser → installation (public HTTPS); use case →
  `IUnitOfWork` port → PostgreSQL.
- **Components:** brute-force protection (lockout), timing equalisation of
  unknown logins, sign-in audit, error handling of concurrency conflicts.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 (Testcontainers available).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 5936 total, 5936 passed, 0 failed, 0 skipped (7 m 33 s).
- Story filter (3 classes) — 19/19 passed.
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` — no vulnerable packages in any of the 7 projects (nuget.org reachable).
- `git diff --stat` over `*.csproj`/`*.props`/`*.targets` — empty.
- Delegated to a `cheap-worker` (facts only): the build, the full and filtered
  test runs, the vulnerability scan and the git status / project-file diff.
  Code reading and every judgement here were done by the reviewer.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed changed. |
| SC-2 Authentication | PASS | Lockout at 5 consecutive failures for 15 min, count restarts at lockout (`AppUser.RecordFailedSignIn`, `AccessFailedCount = 0` with `LockoutEnd`); no permanent lockout; sequence order unchanged in `RunAsync` — lockout checked before the password, disabled only after a correct password without moving the counter, before the temporary-password branch; not built on `PasswordSignInAsync`/`CheckPasswordSignInAsync`. Timing equalisation of unknown logins kept (`PasswordCheck.VerifyDiscarding`); a re-run on an unchanged hash does not verify again, so a known login under contention costs one verification like an unknown one. Tests: `DeanLockoutCounterResetTests` (8), `DeanSignInConcurrencyTests` (9). |
| SC-4 Authorization | PASS | No endpoint, page, policy or antiforgery change (`git diff` limited to the four files above). |
| SC-5 Read-only mode | PASS | Sign-in bookkeeping is the BR-026 `SignInBookkeeping` write, declared around every commit of every run; `InReadOnlyMode_TheReRunStillSaves` covers the re-run. No Google port on this path. |
| SC-6 No DB UI | NOT_APPLICABLE | No diagnostic surface added. |
| SC-7 Key | NOT_APPLICABLE | Not touched. |
| SC-8 Google | NOT_APPLICABLE | Not touched. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | The third conflict propagates to the existing `/error/500` page with no detail; `InstallationExceptionHandler` logs the exception, whose message is static ("The account was changed concurrently.") wrapping EF's row-count message; `EnableSensitiveDataLogging` is not used anywhere in `src`. No new log line. |
| SC-11 Audit | PASS | One sign-in audit row per completed run; a conflicting run's staged row is detached before the re-run (`UnitOfWork.DetachFailedAppUserEntries`), so the request leaves exactly one outcome row; rows carry account id, category, time and request id — no login text, no personal data. No update/delete path added. |
| SC-12 Owner | NOT_APPLICABLE | No `Contracts` / Control Plane change. |
| SC-13 Outbound | NOT_APPLICABLE | No outbound flow. |

## 6. Authentication and Authorization

- **Requirement:** `trebovaniya.md` §9 / §2 (v62, v64, v65), SC-2.
- **Before:** after the first lockout the count stayed at 5, so one wrong password
  after expiry re-locked the account — stricter than policy and a denial-of-service
  lever against a known Dean login (one guess per 15 minutes kept the Dean out).
  The fix removes that lever while still requiring 5 consecutive failures per
  lockout, as SC-2 states.
- **Concurrency:** previously a lost race returned `500` and the losing attempt was
  not counted. Now the loser re-runs on a fresh read, so concurrent wrong guesses
  are all counted (`TwoWrongPasswordsAtOnce_BothGetTheCommonRefusal_AndTheCountIsTwo`)
  and a conflict at the fifth failure ends in the lockout refusal
  (`AConflictAtTheFifthFailure_EndsInTheLockoutRefusal`). Brute-force resistance
  is improved, not weakened.
- Anonymous access unchanged; no role check changed.

## 7. Credentials, Key and Google Access

The plaintext password is held only for the request inside `PasswordCheck`
(as before in the method parameter); it is not logged, stored or put in an
exception. Only `IPasswordHasher.Verify` sees it. A changed hash between runs
(Admin reset, password change) forces a fresh verification
(`AReRunOnAChangedHash_VerifiesAgain`), so a stale "matches" result cannot sign
a Dean in with a password that was just replaced. No key or Google access.

## 8. Sensitive Data Exposure

No response, view, DTO, export or telemetry change. Logs: see SC-10. Audit: see
SC-11. Refusal messages unchanged (one common message; "account disabled" only
with the correct password).

## 9. Input Validation

No new input. Email and password reach the use case through the unchanged
US-012 page model.

## 10. API Security

No endpoint added or changed (API design `NOT_APPLICABLE` v1). The only
observable change is that a lost race returns the specified refusal page
instead of `500`.

## 11. Persistence and Configuration

No migration, column or constraint. The optimistic concurrency token on
`app_user` is the existing `concurrency_stamp`; the translation is code in
`UnitOfWork` (US-032 precedent). No configuration file changed.

## 12. Logging, Audit and Telemetry

No new log statement. Audit as SC-11. `docs/hooks/tool-usage.jsonl` remains
git-ignored (`.gitignore` lines 62–63).

## 13. Dependencies

No package or project reference changed. The vulnerability scan reported no
vulnerable packages (direct and transitive) in any project.

## 14. Security Test Coverage

| Property | Test(s) |
|---|---|
| Count restarts at lockout; 5 new failures needed after expiry | `DeanLockoutCounterResetTests` |
| Lockout checked before password; counter unchanged during lockout and for disabled | `DeanLockoutCounterResetTests` (VR-001 guard) + US-012 regression |
| Concurrent failures all counted; no `500` | `DeanSignInConcurrencyTests.TwoWrongPasswordsAtOnce_…`, `AConflictAtTheFifthFailure_…` |
| Re-run reads fresh state | `TheReRun_SeesWhatTheOtherWriterSaved` |
| Bounded re-runs; third conflict not hidden | `ThreeConflictsInARow_AreNotHidden`, `TwoConflicts_TheThirdRunSaves` |
| One verification on unchanged hash; re-verify on changed hash | `AReRunOnAnUnchangedHash_DoesNotVerifyAgain`, `AReRunOnAChangedHash_VerifiesAgain` |
| Read-only mode does not block bookkeeping | `InReadOnlyMode_TheReRunStillSaves` |
| Provider type never reaches Application; detach | `AppUserConflictTranslationTests` (2) |

Concurrency tests run on PostgreSQL via Testcontainers with a controlled
interleaving (`InterleavingAppUserRepository`), not timing; fixtures are
synthetic. No live Google or Control Plane call.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Lock out a known Dean indefinitely with one guess per 15 min | Each lockout needs 5 consecutive failures | `AppUser.RecordFailedSignIn`; counter tests | PASS |
| Parallel guesses to escape counting | Every completed attempt counted; lost race re-runs | concurrency tests | PASS |
| Parallel guesses to learn the password from a `500` | No oracle: a third conflict ends in the same `500` for right and wrong passwords, with no session and no outcome | `ThreeConflictsInARow_AreNotHidden`; code review | PASS (INFO-1) |
| Amplify hashing cost by forcing conflicts | Verification reused on unchanged hash; at most 3 runs | `AReRunOnAnUnchangedHash_…` | PASS |
| Timing difference known vs unknown login under contention | One verification either way | `PasswordCheck` | PASS (INFO-2) |
| Sign in with a password replaced between runs | Re-verify on changed hash | `AReRunOnAChangedHash_VerifiesAgain` | PASS |

## 16. Repository Hygiene

Ignored as required: `classroom_cache.db`, the generated `.xlsx` exports and both
live credential files (not opened). No secret-like file in the change set.
`.claude/settings.local.json` is modified in the working tree — IDE-local config,
outside the Story (INFO-3).

## 17. Deviations

None from the Specification or SC-2. The broad audit-row detach (verification
N-2) is a deliberate US-032-pattern choice; in the sign-in request only the
run's own row is staged, so no other audit row can be lost.

## 18. Findings

### INFO-1 — An attempt whose three runs all conflict is not counted
- Severity: Informational · Category: AUTHENTICATION · SC-2
- File: `src/ClassroomAgent.Application/UseCases/SignInDeanUseCase.cs` (`ExecuteAsync`)
- Evidence: after `MaximumRuns` conflicts the exception propagates; nothing is saved
  for that attempt (spec FR-004, I-4).
- Risk: negligible — the attacker gets the error page with no outcome, so the
  uncounted attempt reveals nothing about the password; it needs three
  consecutive interleavings on one row. Same as the Owner sign-in.
- Correction: none required. Loop-back: none.

### INFO-2 — A re-run that finds the account gone does a second (dummy) verification
- Severity: Informational · Category: AUTHENTICATION · SC-2
- File: `SignInDeanUseCase.RunAsync` step 1 after a conflict
- Evidence: if the account is deleted or demoted between runs, the next run takes
  the unknown-login branch and calls `VerifyDiscarding` once more.
- Risk: none practical — requires an Admin deleting the account mid-request; the
  response is the common refusal.
- Correction: none required.

### INFO-3 — `.claude/settings.local.json` is modified in the working tree
- Severity: Informational · Category: REPOSITORY_HYGIENE
- Evidence: `git status` shows ` M .claude/settings.local.json`; not part of the Story.
- Correction: leave it out of the Story commit (Definition of Done item 8).

## 19. Positive Controls

- SC-2 six-step order preserved in each run; lockout checked before the password.
- Count restart at lockout, no permanent lockout.
- Timing equalisation kept and not amplified by re-runs.
- Provider exception never crosses into Application (AD-4); one public type per file.
- BR-026 write declared on every commit of every run.
- Exactly one audit outcome row per request; no personal data.
- Generic error page on unrecoverable conflict; no sensitive EF logging.
- Build clean, full suite green, no vulnerable packages, no dependency change.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 is resolved (a).

## 21. Review Limitations

- Code, configuration and test review plus local test runs; no penetration test.
- Wall-clock timing of the sign-in under contention was not measured; the
  equal-cost claim rests on counting verifications (`CountingPasswordHasher`).

## 22. Verdict Rationale

The implementation report records a green build and tests, confirmed by an
independent run (5936/5936, 0 warnings). Every touched SC item is PASS with
evidence; no Critical, Major or Minor finding; no blocking Open Decision.
Verdict **PASS**, next stage `HUMAN_PR_APPROVAL`.
