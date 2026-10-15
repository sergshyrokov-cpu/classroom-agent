---
artifact_type: specification
story: US-048
version: 1
status: APPROVED
created_at: 2026-10-15T18:25:22Z
updated_at: 2026-10-15T19:00:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-048-dean-lockout-counter-reset.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/decisions/US-048-open-decisions.md
    version: 1
supersedes: null
---

# US-048 Specification — A Dean's failed-attempt count starts again when a lockout begins

## 1. Overview

The Dean's password sign-in (US-012 FR-012, FR-013) is brought to the policy it
already has to follow (`trebovaniya.md` §9 "Модель ролей", v62: "после 5
неудачных попыток подряд вход блокируется на 15 минут, успешный вход сбрасывает
счётчик; бессрочной блокировки нет"; §2, v62, v64: the same policy holds for the
Dean; SC-2). Two defects found by the pre-pilot security audit
(`docs/reviews/audits/2026-10-pre-pilot-security-audit.md`) are corrected:

1. **F-1.** `AppUser.RecordFailedSignIn` sets the lockout at the fifth
   consecutive failure but leaves the counter at 5. After the lockout expires the
   next single wrong password makes it 6 and locks sign-in again, until a
   successful sign-in or an Admin reset. A lockout must again need five
   consecutive failures.
2. **I-1 / Story OD-001 (resolved a).** Two wrong passwords for the same Dean at
   the same moment make the second save fail on the account's concurrency token;
   nothing catches it and the caller sees the error page (`500`). Both callers
   must get the common refusal message.

The requirement does not change; the code is brought to it. No endpoint, page,
message, audit category, schema or migration changes.

**Requirements version.** The Story says "aligned with v93". Versions 94–96 were
checked: v94 (Data Protection keys at rest), v95 (browser security headers) and
v96 (Google sign-in admits only an Admin account) do not touch the Dean's
password sign-in sequence, its counter or its lockout. This Specification is
written against v96.

**Open questions.** `trebovaniya.md` §7 (v96) has open items 10, 14, 26, 27 and
28; none concerns sign-in, the counter or the lockout.

## 2. Business Goal

A Dean's sign-in page is reachable from the internet and the login is a
guessable work email (§2, v62). The lockout exists to slow password guessing
without letting a stranger lock a Dean out for good. Today, after the first
lockout, one request per fifteen minutes is enough to keep a Dean locked out,
and a Dean who mistypes once after a lockout is locked again. After this Story a
lockout always needs five consecutive wrong passwords — the same as the Owner's
sign-in in the Control Plane (US-001 FR-009, I-4).

## 3. Business Flow

The six-step sequence of SC-2 v66 / US-012 FR-012 is unchanged in order and
outcome. Only two things change:

- **Step 3 (wrong password).** The counter rises by one. If it reaches five, the
  lockout starts (`LockoutEnd = now + 15 min`) **and the counter returns to
  zero in the same save** (FR-001).
- **Any save of the sequence that loses a race with another write to the same
  account** is not reported as an error: the sequence is run again from step 1
  on a fresh read of the account, at most three runs in total (FR-003…FR-006).

```
attempt 1..3:
  read account (fresh)            ── step 1 unknown login → common refusal
  lockout in force?               ── step 2 → common refusal, counter unchanged
  password wrong?                 ── step 3 → counter +1; at 5: lockout, counter := 0
  account disabled?               ── step 4 → "account disabled" message
  success                         ── steps 5–6 → counter := 0, lockout cleared
  save (state change + audit row together)
    concurrency conflict → discard this run's staged changes, run again
```

## 4. Functional Requirements

### FR-001 The count starts again when a lockout begins

When a wrong password (step 3) brings the failed-attempt count to five, the
lockout is set to the time of that attempt plus fifteen minutes and the count is
set to **zero**, both in the same save as the attempt's audit row. The policy
numbers (5, 15 minutes) stay in `Application` (`SignInDeanUseCase`); the entity
still knows no policy (US-012 FR-013).

Consequences, all already required and now true:

- while the lockout is in force step 2 refuses every attempt and does not move
  the count, which stays at zero (US-012 FR-012 step 2, SC-2 v65, v66);
- when the lockout expires, sign-in needs five more consecutive wrong passwords
  to lock again (AC-002, AC-003);
- the lockout still expires by itself and is never permanent (SC-2 v62).

*(interpretation I-1 — "consecutive" is counted from the start of the last
lockout; the same reading as the Owner's US-001 I-4.)*

### FR-002 Nothing else in the sequence changes

The order of the six steps, the outcomes, the common refusal message, the
"account disabled" message, the audit categories and their texts, the timing
equalisation on an unknown login, the forced change of a temporary password, an
Admin reset clearing a lockout and zeroing the count, a reset never re-enabling a
disabled account (BR-014 v64), a successful sign-in zeroing the count and
clearing the lockout, and read-only mode treating all of this as sign-in
bookkeeping (BR-026, US-012 FR-015) stay exactly as US-012 specifies.

### FR-003 A concurrency conflict runs the sequence again

When saving any step of the sequence fails because the Dean's account row was
changed by another write since it was read (the account's concurrency token no
longer matches), the attempt is **not** reported as a failure. The sequence is
run again from step 1: the account is read again from the database, the clock is
read again, and the steps are evaluated on the current state. The same holds for
a conflict with any other writer of the account — another sign-in, an Admin
reset, disable or re-enable, a password change, a language choice
*(interpretation I-2)*.

The new run may reach a different step than the first one. In particular, if the
parallel attempt was the fifth failure and started a lockout, the retried attempt
is refused at step 2 with category "locked out" and does not move the count
*(interpretation I-3)*.

### FR-004 At most three runs

The sequence runs at most **three** times for one request — the first run and up
to two re-runs, as the Owner's sign-in does (`OwnerSignInService`,
`MaxConcurrencyRetries = 3`). If the third run also ends in a concurrency
conflict, that conflict is not caught by the sequence and reaches the host's
existing unhandled-error handling, as any other unexpected failure does today
*(interpretation I-4)*.

### FR-005 What a conflicted run leaves behind

A run that ended in a conflict leaves nothing behind: its audit row was staged in
the same save as the account change and is discarded together with it, and the
stale account it read is no longer the one the next run sees. So each request
writes **exactly one** audit row — the one of the run that saved — and the count
changes by exactly the number of wrong-password outcomes that were saved.

Layering (AD-3, AD-4, AD-7): `Application` never sees an EF Core or Npgsql type.
The persistence layer translates a concurrency conflict on an `AppUser` row into
an exception declared in `Application` and, before throwing, removes the failed
account entry and the run's staged audit rows from the unit of work, so that the
next read returns the current row. This follows the precedent of US-032
(`UnitOfWork` → `MeetingCodeLinkConflictException`, which detaches the failed
entries). The translation is limited to `AppUser` conflicts; every other conflict
or failure keeps its current handling.

### FR-006 A re-run does not verify the same password twice

A re-run verifies the password again only if the account's stored password hash
changed since the previous run (an Admin reset or a password change landed in
between). Otherwise it reuses the result of the previous verification. Reason:
SC-2 requires that the response never reveals whether the login exists; an
unknown login never conflicts and costs one verification, so a known login must
not cost two or three under contention *(interpretation I-5)*.

### FR-007 Cancellation

Each run honours the request's `CancellationToken`. A cancelled request is not
re-run.

## 5. Acceptance Criteria

Story criteria AC-001…AC-005 are kept with their ids; AC-006…AC-009 are derived
from the interpretations so that each is tested.

### AC-001 Five failures in a row start a lockout, and the count starts again
*(Story)* **Given** a Dean account with no failed attempts, **when** five wrong
passwords are submitted in a row, **then** sign-in is locked for 15 minutes
(step 2 refuses even the correct password, with the common message), and the
account's failed-attempt count is zero. — FR-001

### AC-002 After the lockout ends, one wrong password does not lock again
*(Story)* **Given** a Dean whose lockout has just ended (clock moved past it),
**when** one wrong password is submitted, **then** the common refusal message is
shown, the audit row has category "wrong password", the count is 1, and sign-in
is **not** locked — the correct password on the next attempt signs the Dean in.
— FR-001

### AC-003 After the lockout ends, five more failures lock again
*(Story)* **Given** a Dean whose lockout has just ended, **when** five wrong
passwords are submitted in a row, **then** sign-in is locked for 15 minutes
again — the lockout still expires by itself and is never permanent. — FR-001

### AC-004 Everything else in the sequence is unchanged
*(Story)* **Given** the existing US-012 tests (lockout before password, disabled
account only with the correct password and no lockout, temporary password, Admin
reset clearing a lockout), **when** the suite runs after the change, **then** it
stays green. — FR-002

### AC-005 Simultaneous wrong passwords get the common refusal (OD-001)
*(Story)* **Given** a Dean account with no failed attempts, **when** two wrong
passwords for that Dean are submitted at the same moment, **then** both callers
get the common refusal message — never the error page (`500`) — each attempt
writes its "wrong password" audit row, and the count is 2. — FR-003, FR-005

### AC-006 A conflict at the fifth failure ends in the lockout refusal
*(derived, I-3)* **Given** a Dean with four failed attempts, **when** two wrong
passwords are submitted at the same moment, **then** neither caller sees the
error page; one attempt is audited "wrong password" and starts the lockout, the
other is audited "locked out"; the count is zero and sign-in is locked. — FR-001,
FR-003

### AC-007 The re-run sees what the other writer saved
*(derived, I-2)* **Given** a sign-in attempt that read the account before an
Admin disabled it, **when** its save conflicts with the disable, **then** the
attempt is re-run on the current account and its outcome is the one the current
state gives (for the correct password: "account disabled"), and exactly one
audit row is written for it. — FR-003, FR-005

### AC-008 Three conflicts in a row are not hidden
*(derived, I-4)* **Given** a unit of work whose save conflicts on every call,
**when** a Dean signs in, **then** the sequence runs three times and the third
conflict propagates out of the use case; no audit row and no account change is
saved. — FR-004, FR-005

### AC-009 A re-run does not verify the same hash again
*(derived, I-5)* **Given** a wrong-password attempt whose first save conflicts
and whose account's password hash is unchanged on the re-read, **when** the
sequence is re-run, **then** the password hasher's verification is called once
for the request; **and given** the hash changed in between (an Admin reset),
**then** it is called again for the new hash. — FR-006

## 6. Validation Rules

This Story adds no input, field or format. The sign-in form's validation (US-012:
email and password required, lengths, normalisation by `AppUser.Normalize`)
is unchanged.

- **VR-001** The policy constants are not configurable: five attempts, fifteen
  minutes, three runs. `AppUser.RecordFailedSignIn` keeps rejecting a maximum
  below 1 (`ArgumentOutOfRangeException`).
- **VR-002** The stored count is never negative (`ck_app_user` check, existing)
  and after this Story never exceeds four while no lockout is in force: it
  reaches five only inside the save that returns it to zero.

## 7. Security Requirements

- **SC-2 policy unchanged.** Five consecutive failures, fifteen minutes, no
  permanent lockout, one common message for every refusal, step 2 before the
  password check, step 4 only with the correct password and no lockout. This
  Story removes an easier-than-allowed way to keep a Dean locked out (§2 v62).
- **No login oracle.** A concurrency conflict must not give a known login a
  response an unknown login cannot get: no error page (FR-003), no second
  password verification on the same hash (FR-006). A re-run adds one database
  read; this residual timing difference exists only under concurrent requests
  for the same account and is far smaller than the `500` it replaces.
- **Audit (SC-11).** One row per request, the category of the run that saved,
  no personal data — the typed login is still never recorded on step 1. A
  discarded run writes no row.
- **Logging (SC-10).** No new log entry is required. Nothing this Story adds
  writes the typed email or password to a log.
- **Read-only mode (BR-026, AD-6).** Re-runs are the same sign-in bookkeeping
  and are declared as `PermittedServiceWrite.SignInBookkeeping` like the first
  run.
- **Layers (AD-3, AD-4).** No provider type in `Application`; FR-005.

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| Concurrency conflict on the Dean's `AppUser` row, run 1 or 2 | discarded silently; sequence re-run (FR-003, FR-005) |
| Concurrency conflict on run 3 | propagates; existing unhandled-error handling (FR-004) |
| Any other save failure | unchanged: propagates as today |
| Request cancelled | `OperationCanceledException` as today; no re-run (FR-007) |

## 9. Non-Functional Requirements

- No extra database round trip or hashing work on the uncontended path.
- Time comes only from the injected `TimeProvider` (US-012 FR-013, TC-1), so
  AC-001…AC-003 are proved by moving the clock, not by waiting.
- AC-005…AC-007 depend on the real concurrency token and are proved against real
  PostgreSQL (TC-2), with an interleaving the test controls rather than a timing
  race; the EF Core InMemory provider is not used.

## 10. Out of Scope

- The order of the six steps, the messages, the audit categories and their texts
  (SC-2 v65, v66).
- The Owner's sign-in (already correct; already retries).
- An Admin reset clearing a lockout, and a reset never re-enabling a disabled
  account (BR-014 v64).
- Rate limiting across accounts — not a requirement.
- Concurrency handling of any other `AppUser` writer (Admin reset, disable,
  password change, Google sign-in of an Admin): they keep their current
  behaviour; only the Dean sign-in sequence re-runs.
- **API_DESIGN:** this Story does not change public API behaviour — no endpoint,
  route, status code, request or response body changes.
- **DB_DESIGN:** this Story does not change persistence behaviour in the sense of
  schema — no table, column, index, constraint or migration; the same
  `access_failed_count` and `lockout_end` columns hold a different value. The
  only persistence-layer code is the conflict translation fully specified in
  FR-005.

## 11. Open Decisions

The Story's only Open Decision, **OD-001**, is resolved (a) by the Owner on
2026-10-15 and is carried into `docs/decisions/US-048-open-decisions.md`. Its
effect is FR-003…FR-006 and AC-005. No new Open Decision was raised.

## 12. Interpretations

| Id | Interpretation | Why | Lives in |
|---|---|---|---|
| I-1 | "5 подряд" is counted from the start of the last lockout: the count returns to zero when the lockout starts | The rule says the lockout needs five consecutive failures; the Owner's sign-in already reads it so (US-001 I-4) | FR-001 |
| I-2 | A conflict with **any** writer of the account re-runs the sequence, not only a parallel sign-in | The token cannot tell writers apart; the re-run gives the outcome of the current state | FR-003, AC-007 |
| I-3 | A re-run that meets a lockout started by the parallel attempt is refused at step 2 ("locked out") and does not count | Step 2 precedes step 3 (SC-2 v66); counting it would break "the count does not move during a lockout" | FR-003, AC-006 |
| I-4 | At most three runs, then the conflict propagates (error page) | Bounded, identical to the Owner (`MaxConcurrencyRetries = 3`); three consecutive conflicts are not a realistic sign-in | FR-004, AC-008 |
| I-5 | A re-run does not verify the password again against an unchanged hash | SC-2 "never reveals whether the login exists": a known login must not cost two or three hash checks while an unknown one costs one | FR-006, AC-009 |

## 13. Traceability

| AC | Story | FR / VR | trebovaniya.md / conventions |
|---|---|---|---|
| AC-001 | AC-001 | FR-001, VR-002 | §9 v62, §2 v62 v64, SC-2 |
| AC-002 | AC-002 | FR-001 | §9 v62, SC-2 |
| AC-003 | AC-003 | FR-001 | §9 v62 ("бессрочной блокировки нет") |
| AC-004 | AC-004 | FR-002 | §2 v64 v65, SC-2 v65 v66, BR-014, BR-026 |
| AC-005 | AC-005, OD-001 | FR-003, FR-005 | §9 v62 (one common message), SC-11 |
| AC-006 | OD-001 | FR-001, FR-003 (I-3) | SC-2 v66 |
| AC-007 | OD-001 | FR-003, FR-005 (I-2) | SC-11 |
| AC-008 | OD-001 | FR-004, FR-005 (I-4) | — |
| AC-009 | OD-001 | FR-006 (I-5) | §9 v62 (login not revealed), SC-2 |

## 14. Delegation

All lookups for this Specification (requirements §2, §7, §9; the US-001 and
US-012 specifications; the audit report; `SignInDeanUseCase`, `AppUser`,
`OwnerSignInService`, `UnitOfWork`; existing tests asserting the count) were made
directly with read-only searches; nothing was delegated.
