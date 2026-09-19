---
id: US-007
epic: EPIC-8
title: Read-only mode enforcement
slug: read-only-mode
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v77.
---

# User Story

As the **Owner** of the service

I want an installation that is suspended, past its grace period, or has never
confirmed its legitimacy to refuse every write and every call to Google, while
viewing and exporting keep working

So that suspending a school actually stops it from working instead of only
saying so, and a school that has lost contact with me keeps its data readable
until the contact is restored.

---

# Business Value

US-005 taught the installation **whether** it is in read-only mode; nothing yet
acts on the answer. Until something does, the Owner's suspension lever — the one
`trebovaniya.md` §9 describes as deciding whether the school works at all — has
no effect on the school's server, and the grace period is a number nobody
applies.

This Story is also the point where the rule becomes unbreakable rather than
remembered: every later installation Story adds writes and Google calls
(US-008 … US-039), and each must land on an enforcement point that already
exists and refuses by default. Adding the check per use case later means one
forgotten use case is a Critical security finding (SC-5).

---

# Scope

**In scope:**

- a single enforcement point in `Application` that every write use case passes
  through, refusing while the installation is in read-only mode (AD-6, SC-5);
- the closed list of permitted service writes from BR-026, expressed in code as
  an explicit allow-list — not as exemptions scattered across use cases;
- the refusal itself: its type, the reason it carries (never confirmed /
  suspended by the Owner / grace period expired, with the time of the last
  successful check), and what is logged;
- the rule that no port calling Google is invoked in read-only mode, and the
  enforcement point that makes it so;
- proof that the enforcement cannot be bypassed: a structural test that fails
  when a write path exists which does not pass through the enforcement point;
- the permitted case proven end to end on the one write path that exists today —
  the `LegitimacyState` write of US-005, which must keep working in read-only
  mode or the installation could never leave it.

**Out of scope:** the determination of read-only mode and its logging (US-005,
AC-009 and AC-010); the `AuditEvent` table and any audit row — a refused write is
not an audited action (SC-11), and the table arrives with the first audited
action (US-008); showing the mode or its reason to a user, and any banner,
disabled button or hidden menu — nobody can sign in to an installation yet, and
hiding UI is not enforcement (AD-6); the write use cases themselves —
synchronization, Meet code linking, Dean accounts, connection settings, "check
access", report templates — each arrives with its own Story and proves its own
refusal there (TC-5); the Google ports and any Google SDK code (US-009, US-011,
US-013); the retention purge (EPIC-10); the startup access self-check that is
skipped in read-only mode (US-011).

---

# Acceptance Criteria

## AC-001 A write refuses while the installation is in read-only mode

**Given** the installation is in read-only mode for any of the three reasons —
no check has ever succeeded, the `Installation` is suspended, the grace period
has expired (BR-025)

**When** a write use case that is not on the BR-026 closed list runs

**Then**:

- it refuses; nothing is written to the database and no change is committed;
- the refusal is the same for all three reasons — this is one mode, not three
  behaviours;
- the refusal happens in `Application`, before the use case reaches a repository
  or a port (AD-6).

## AC-002 The refusal carries the reason

**Given** a write refused in read-only mode

**When** the refusal is produced

**Then**:

- it names which of the three reasons applies (BR-025);
- for an expired grace period it also carries the time of the last successful
  check; for "never confirmed" there is no such time;
- the reason is carried as data — a caller can render it in Ukrainian or English
  later (NFR-073) without parsing a message string;
- the refusal is signalled as a failure the way `architecture.md` AD-9
  prescribes, not as a status value an unaware caller can ignore.

## AC-003 The closed list of permitted service writes still runs

**Given** the installation is in read-only mode

**When** a write on the BR-026 closed list runs

**Then** it succeeds. The list is exactly:

- an `AuditEvent` row;
- sign-in bookkeeping: Identity failed-attempt counting and lockout, recording
  the time of the last successful sign-in, creating the `AppUser` of an approved
  Admin at their first sign-in, a Dean changing their own password, and a user
  choosing their UI language;
- the legitimacy-check state — last successful check time, last known status,
  last compatibility state, and the `Installation`'s domain and client ID;
- the retention purge and its audit event.

Only the `LegitimacyState` write exists today (US-005); the rest are asserted
against the list, and each Story that introduces one proves it then.

## AC-004 Anything not on the list is refused by default

**Given** a write use case that neither declares itself a permitted service
write nor is on the BR-026 list

**When** it runs in read-only mode

**Then**:

- it is refused, with no further declaration needed — the default is refusal,
  not permission;
- permitting a new service write requires changing the list in one place, and
  that place carries a comment binding it to BR-026 and `trebovaniya.md` §2 (a
  new service write is permitted only by extending that list).

## AC-005 Reading and exporting are never blocked

**Given** the installation is in read-only mode

**When** a read use case, or an export of already-synced data, runs

**Then**:

- it succeeds unchanged — the enforcement point applies to writes only;
- a read is not made to depend on the legitimacy state.

## AC-006 No call to Google is made in read-only mode

**Given** the installation is in read-only mode

**When** any use case that would call a Google port runs

**Then**:

- no Google port is invoked at all — not synchronization, not the Meet pull, not
  "check access", not the startup access self-check (SC-5, SC-8,
  `trebovaniya.md` §2, v54);
- the refusal happens before the port would be called, so nothing leaves the
  process;
- no Google port exists yet, so this Story fixes the enforcement point and the
  rule binding such ports to it; the first Story adding one proves its own
  refusal there (US-009, US-011, US-013).

## AC-007 The enforcement cannot be bypassed

**Given** the solution as it stands after this Story

**When** the test suite runs

**Then**:

- a structural test enumerates the write paths and fails when one exists that
  neither passes through the enforcement point nor is declared a permitted
  service write;
- its failure message states what the author must do, so a later Story is told
  how to comply rather than tempted to delete the test;
- the test fails when the enforcement point is removed from an existing write
  path — it is proven to detect the violation, not merely to pass today.

## AC-008 The mode is evaluated at the moment of the write

**Given** a long-running process, or a use case that starts while the
installation is not in read-only mode

**When** the enforcement point is reached

**Then**:

- the mode is determined from the current `LegitimacyState` and the current
  clock, never from a value cached at startup or held for the process lifetime;
- a check that enters read-only mode takes effect on the next write, without a
  restart;
- time comes from the injectable clock, so the boundary is tested without
  waiting (US-005 Notes).

## AC-009 Leaving read-only mode restores writes

**Given** an installation in read-only mode because its grace period expired, or
because it was suspended

**When** a later check succeeds with status active and inside the grace period
(US-005 AC-007, US-006)

**Then**:

- the next write succeeds, without a restart and without any manual step;
- the installation could reach this state at all — the `LegitimacyState` write
  carrying it was permitted throughout (AC-003, BR-026).

## AC-010 A refused write is logged, not audited

**Given** a write refused in read-only mode

**When** the refusal is recorded

**Then**:

- one log line at `Warning` names the refused operation and the reason category;
- it carries no personal data, no request body and no rejected payload (SC-10,
  DC-10); inside a request it carries the request id;
- no audit row is written — a refused write is not on the SC-11 audited list,
  and this Story creates no `AuditEvent` table;
- entering and leaving read-only mode keep being logged once each, by US-005
  AC-010 — a refused write does not re-log the mode change.

## AC-011 Enforcement lives in Application only

**Given** the solution after this Story

**When** the layering is checked

**Then**:

- the enforcement point is in `ClassroomAgent.Application`; `Web` holds no copy
  of the rule and no second decision about it (AD-3, AD-6);
- `Domain` still depends on nothing, and `Application` still holds no
  `Infrastructure` reference (AD-3);
- the architecture tests of US-005 keep passing unchanged.

---

# Open Decisions

## OD-001 Where the HTTP `409` mapping and the user-visible message belong

API-5 and API-6 fix what a caller sees when read-only mode refuses an action:
HTTP `409`, with a message naming the reason. The installation has no controller,
no `/api/v1`, no error page and no localization baseline today — all of them
arrive with US-008 (US-005 Notes).

Options:

1. This Story delivers the refusal as far as `Application` only (the typed
   failure of AC-002); the first Story adding an installation endpoint maps it to
   `409` with the translated message and tests it (US-008). It must then be
   recorded that API-5 is not yet satisfied end to end after US-007.
2. This Story also delivers the mapping, which means bringing forward a slice of
   the installation host baseline (error body, error page, localization) that
   US-005 deliberately left to US-008.

**Resolution:** option 1, decided by the Owner on 2026-09-19. US-007 delivers the
refusal as far as `Application` — the typed failure of AC-002, carrying the
reason as data. The mapping to HTTP `409` with the translated message arrives
with US-008, together with the installation host baseline it needs. API-5 is
therefore **not** satisfied end to end after this Story, and the Specification
must record that openly rather than leave it implied.

---

# Notes

- The Specification must decide **how** the enforcement point is reached: a guard
  every write use case calls, a decorator around them, a check inside the unit of
  work, or a combination — and whether a backstop in the persistence path is
  added as defence in depth. AD-6 fixes only that the decision is made in
  `Application` and cannot be bypassed by calling an endpoint directly. Whatever
  it decides must make AC-004 (default refusal) and AC-007 (structural proof)
  real rather than aspirational.
- BR-026's closed list is the only source for AC-003; `architecture.md` AD-6
  deliberately does not restate it. Encoding it in code creates a second
  restatement — the Specification must say how it stays bound to BR-026: one
  place, a comment naming the rule, and a test that fails when they diverge.
- Most of the list cannot be exercised yet: there is no Identity, no `AppUser`,
  no audit table and no purge. The Story therefore proves the mechanism plus the
  one live case, and every later Story proves its own (TC-5). This is a mechanism
  Story: it may end with no new endpoint and no new screen.
- `GetLegitimacyModeQuery` and `LegitimacyMode` already exist from US-005 and are
  the input here; nothing about the determination is re-decided.
- The two minor findings of the US-006 security review (F-1 Control Plane
  `System.Net.Http` log level, F-2 push size limit counted in characters) are
  Control Plane files and are **not** in this Story's scope — they belong to the
  next Story touching `ControlPlane/Program.cs` and the push receiver.
