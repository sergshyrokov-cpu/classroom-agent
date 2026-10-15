---
artifact_type: test_strategy
story: US-048
version: 1
status: DRAFT
created_at: 2026-10-15T19:40:00Z
updated_at: 2026-10-15T19:40:00Z
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
  - path: docs/reviews/specifications/US-048-spec-review.md
    version: 1
supersedes: null
---

# US-048 Test Strategy — A Dean's failed-attempt count starts again when a lockout begins

## 1. Scope

The Dean password sign-in sequence (`SignInDeanUseCase`, `AppUser.RecordFailedSignIn`) and the unit of work's
translation of a concurrency conflict on an `app_user` row (spec FR-001…FR-007, VR-001, VR-002, AC-001…AC-009).
No endpoint, page, message, audit category, schema or migration changes (api-design v1 and db-design v1 are
NOT_APPLICABLE), so there are no new contract, HTTP-role, antiforgery, cookie or migration tests: the US-012 ones
stay in force and are AC-004's evidence.

## 2. Test levels

| Level | What it proves | Where |
|---|---|---|
| Unit (Application, ports in memory — TC-1) | FR-001, VR-001, VR-002, AC-001…AC-003: the count after a lockout, with the injected `ManualTimeProvider` | `Application/UseCases/DeanLockoutCounterResetTests` |
| Integration (real PostgreSQL via Testcontainers — TC-2) | FR-003…FR-007, AC-005…AC-009: re-runs on a real concurrency-token conflict | `Application/UseCases/DeanSignInConcurrencyTests` |
| Integration (persistence) | FR-005: the unit of work's `AppUser` conflict translation and clean-up | `Infrastructure/Persistence/AppUserConflictTranslationTests` |
| Regression | AC-004: the US-012 suite unchanged | existing classes, §7 |

**Controlled interleaving, not a timing race (spec §9).** The attempt under test is the production
`SignInDeanUseCase`, built from one request scope's own ports, with only two of them wrapped:
`InterleavingAppUserRepository` (a hook after each read by login; counts the reads) and `CountingPasswordHasher`
(counts verifications). The competing write runs to completion in its own scope inside the hook — another sign-in,
an Admin disable or reset, or a raw `UPDATE` of `concurrency_stamp` standing for "any other writer" (I-2). The
attempt's save then meets a stale token deterministically. The number of reads by login is the number of runs.

## 3. Scenarios

### Positive
- P-1 Five failures lock and zero the count (AC-001).
- P-2 After the lockout, one wrong password is an ordinary refusal; the correct password then signs in (AC-002).
- P-3 After the lockout, five more failures lock again, and that lockout lifts by itself (AC-003).
- P-4 Two wrong passwords at once: both refused with the common outcome, two rows, count 2 (AC-005).
- P-5 Two conflicts are absorbed; the third run saves one row (FR-004).

### Negative
- N-1 During a lockout the correct password and a wrong one are both refused "locked out"; count stays 0 (AC-001).
- N-2 A conflict at the fifth failure: the re-run is refused "locked out" and does not count (AC-006, I-3).
- N-3 A conflict with an Admin disable: the re-run with the correct password ends "account disabled", no success
  recorded (AC-007, I-2).
- N-4 Three conflicts in a row propagate as `AppUserConflictException`; no row, no account change (AC-008, I-4).
- N-5 A cancelled request is not run again (FR-007).

### Boundary
- B-1 After a lockout, four failures do not lock; the fifth does (AC-003).
- B-2 Across three consecutive lockouts the count is 1,2,3,4,0 each time and never above four without a lockout
  (VR-002).
- B-3 Entity with a caller-given maximum of 3: the third call zeroes the count and sets the lockout (FR-001 — the
  entity knows no policy).
- B-4 Exactly the third run is the last (P-5 vs N-4).

### Validation
- V-1 `RecordFailedSignIn` with a maximum below 1 still throws `ArgumentOutOfRangeException` (VR-001).
- V-2 The policy constants stay 5 / 15 min (existing `DeanLockoutTests.ThePolicyNumbers_AreTheOnesSc2Fixes`).

### Security
- S-1 No login oracle by hashing work: a re-run on an unchanged hash verifies once; on a changed hash (Admin reset
  in between) it verifies again (AC-009, I-5, SC-2).
- S-2 No error page for a known login under contention (AC-005…AC-007 assert an outcome, never an exception).
- S-3 One audit row per request — the run that saved; a discarded run leaves none (FR-005, SC-11): asserted in
  AC-005…AC-008 and the persistence test.
- S-4 Read-only mode: the re-run is sign-in bookkeeping (BR-026) and still saves while the installation is
  suspended.
- S-5 Layering: the use case sees `AppUserConflictException`, never `DbUpdateConcurrencyException` (AD-3, AD-4).

### Persistence
- D-1 A stale `AppUser` save is translated to `AppUserConflictException` (FR-005).
- D-2 After the conflict, the same scope's next read returns the current row (the failed entry was detached) and a
  later save carries no audit row of the discarded run (FR-005).

## 4. Fixtures

- `DeanAccountWorld` (existing, in-memory ports) for the unit tests.
- `ReadOnlyModeHost.StartAsync(…, Cause.NotReadOnly | Cause.Suspended)` for a writable or suspended installation over
  an isolated Testcontainers database per test; `InsertDeanAsync` / `InsertAppUserAsync` seed accounts.
- New: `TestInfrastructure/InterleavingAppUserRepository`, `TestInfrastructure/CountingPasswordHasher`.
- Synthetic data only; no Google port is reached (TC-4).

## 5. Excluded scenarios

- An HTTP test with two simultaneous POSTs to `/sign-in`: it would be a timing race (spec §9 asks for a controlled
  interleaving). The page's mapping of `WrongPassword` / `LockedOut` to the common message is US-012's and is covered
  by `DeanSignInPageTests` (AC-004).
- Conflicts with other `AppUser` writers outside the sign-in sequence (spec §10). Spec-review M-3 (those writers
  still end on the error page with the new exception type) is checked at IMPLEMENTATION_VERIFICATION, as the Owner
  decided at HUMAN_SPEC_APPROVAL.
- Timing measurement of the re-run's extra read (spec §7 residual note) — not reliably testable; the verification
  count (S-1) is the testable part.

## 6. Known limitations

- FR-007 (N-5) passes before implementation too: today a cancelled request also throws and does not re-run. It is a
  regression guard for the new loop, not a red-phase test.
- The unit tests' in-memory repository returns the same object on every read, so re-run behaviour is proved only at
  the integration level, where it is real.

## 7. Open Decisions affecting testing

None. Story OD-001 is resolved (a). The name of the exception the spec FR-005 asks for ("an exception declared in
`Application`") is not given by any design; the skeleton names it `AppUserConflictException` after the US-032
precedent `MeetingCodeLinkConflictException` — a naming choice, recorded in the test generation report.
