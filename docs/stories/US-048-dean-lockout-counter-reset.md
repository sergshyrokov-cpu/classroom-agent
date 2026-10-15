---
id: US-048
epic: EPIC-6
title: A Dean's failed-attempt count starts again when a lockout begins
slug: dean-lockout-counter-reset
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from the pre-pilot security audit, finding F-1
# (docs/reviews/audits/2026-10-pre-pilot-security-audit.md); to be reviewed and
# edited by the Owner before activation. Aligned with trebovaniya.md v93.
---

# User Story

As a **Dean**

I want a sign-in lockout to need five wrong passwords in a row every time

So that one typo after an earlier lockout does not lock me out for another
fifteen minutes, and a stranger who knows my work email cannot keep me locked
out more easily than the rule allows.

---

# Business Value

`trebovaniya.md` §9 (v62) sets the policy the Dean inherits (§2, v62, v64):
"после 5 неудачных попыток подряд вход блокируется на 15 минут, успешный вход
сбрасывает счётчик; бессрочной блокировки нет". §2 adds why this matters for the
Dean in particular: the sign-in is reachable from the internet and the login is
a guessable work email, so locking a Dean out must not be easy.

The pre-pilot security audit (finding F-1) found that the Dean's counter is
**not** reset when a lockout begins. It stays at 5 after the lockout ends, so
the next single wrong password makes it 6 and locks sign-in again for 15
minutes — and so on, until a successful sign-in or an Admin reset. The Owner's
sign-in in the Control Plane already starts the count again when a lockout
begins (US-001 I-4) and has a test for it; the Dean's does not.

No data is exposed. The defect is availability: a Dean is locked out more often
than the rule says, and an outsider needs one request per 15 minutes instead of
five to keep a Dean's sign-in closed. The code is brought to the requirement;
the requirement does not change.

---

# Scope

**In scope:**

- the Dean sign-in sequence (US-012 FR-012, FR-013): when the fifth consecutive
  failure starts a lockout, the failed-attempt count starts again from zero;
- after the lockout ends, a lockout again needs five consecutive failures;
- tests for the count after a lockout ends, with the injected clock;
- two wrong passwords for the same Dean at the same moment get the common
  refusal message instead of the error page (OD-001).

**Out of scope:**

- the order of the six-step sequence, the messages, the audit categories and
  their texts (SC-2 v65, v66) — unchanged;
- the Owner's sign-in (already correct);
- an Admin reset clearing a lockout, and a reset never re-enabling a disabled
  account (BR-014 v64) — unchanged;
- rate limiting across accounts — not a requirement.

---

# Acceptance Criteria

## AC-001 Five failures in a row start a lockout, and the count starts again

**Given** a Dean account with no failed attempts

**When** five wrong passwords are submitted in a row

**Then** sign-in is locked for 15 minutes (step 2 refuses even the correct
password, with the common message), and the account's failed-attempt count is
zero.

## AC-002 After the lockout ends, one wrong password does not lock again

**Given** a Dean whose lockout has just ended (clock moved past it)

**When** one wrong password is submitted

**Then** the common refusal message is shown, the audit row has category
"wrong password", the count is 1, and sign-in is **not** locked — the correct
password on the next attempt signs the Dean in.

## AC-003 After the lockout ends, five more failures lock again

**Given** a Dean whose lockout has just ended

**When** five wrong passwords are submitted in a row

**Then** sign-in is locked for 15 minutes again — the lockout still expires by
itself and is never permanent.

## AC-004 Everything else in the sequence is unchanged

**Given** the existing US-012 tests (lockout before password, disabled account
only with the correct password and no lockout, temporary password, Admin reset
clearing a lockout)

**When** the suite runs after the change

**Then** it stays green.

## AC-005 Simultaneous wrong passwords get the common refusal (OD-001)

**Given** a Dean account with no failed attempts

**When** two wrong passwords for that Dean are submitted at the same moment

**Then** both callers get the common refusal message — never the error page
(`500`) — each attempt writes its "wrong password" audit row, and the count is
2.

---

# Open Decisions

## OD-001 Two wrong passwords at the same moment

**Question.** The audit also found (I-1) that two wrong passwords for the same
Dean arriving at the same moment make the second one fail with the error page
(`500`) instead of the common refusal: the account's concurrency token rejects
the second save and nothing catches it. No data leaks and the count stays
correct. The Owner's sign-in handles the same case by re-reading the account
and running the sequence again (up to 3 times). Fix it in this Story?

- **(a) Yes, in this Story (recommended)** — the same code path and the same
  tests; the Dean sees the normal refusal message, like the Owner does.
- (b) No — leave it as an informational note.

**Resolution (the Owner, 2026-10-15):** (a) — fixed in this Story. Two wrong
passwords for the same Dean at the same moment both get the common refusal
message, never the error page, and the count still rises by exactly the number
of failures, as the Owner's sign-in does (see AC-005).

---

# Notes

- Origin: pre-pilot security audit 2026-10-15, finding F-1 (and I-1 for OD-001).
- Code today: `AppUser.RecordFailedSignIn`
  (`src/ClassroomAgent.Domain/Entities/AppUser.cs`); the Owner's equivalent is
  `OwnerSignInService` (`src/ClassroomAgent.ControlPlane/Services/OwnerSignInService.cs`).
- Expected stages: API_DESIGN NOT_APPLICABLE (no endpoint changes); DB_DESIGN
  NOT_APPLICABLE (no schema change — the same columns, a different value).
- No new NuGet package, no new requirements version.
