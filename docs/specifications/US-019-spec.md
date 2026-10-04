---
artifact_type: specification
story: US-019
version: 1
status: APPROVED
created_at: 2026-10-04T06:28:44Z
updated_at: 2026-10-04T06:31:38Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-019-trigger-sync-from-ui.md
    version: null
  - path: trebovaniya.md
    version: 81
  - path: docs/decisions/US-019-open-decisions.md
    version: 1
supersedes: null
---

# US-019 Specification — Trigger a synchronization from the UI

## 1. Overview

Until now a synchronization run starts only on the schedule (US-013). This Story
adds the "Synchronize" button the permission matrix gives to both Admin and Dean
(`trebovaniya.md` §2 "Запуск синхронизации" ✔ ✔, BR-004): the Admin on the
connection page, to check right after connecting that the setup works (§2
explanation); the Dean on the home page, to refresh the data without waiting
(§4 Epic 1, "Декан может вручную инициировать синхронизацию (кнопка)").

Four properties shape everything below.

- **The button only asks.** A press enqueues a request through the
  out-of-schedule entry point US-013 built for this Story
  (`SyncRunCoordinator.Request`, US-013 OD-007) and answers at once. It never
  waits for the run, never reports its result and never calls Google itself
  (§8 "Кнопка "Синхронизировать" — только триггерит серверный эндпоинт",
  BR-040, AD-5).
- **One run at a time is already guaranteed.** A press during a run or a
  retention purge is remembered, once however many arrive, and the run starts
  when the current work ends (US-013 OD-007, US-037 API design §3). This Story
  adds no second mechanism.
- **Read-only mode refuses in `Application`.** Synchronization is on the
  BR-026 blocked list; the press is refused by the read-only guard before
  anything is enqueued, and the button is not hidden to enforce it (AD-6,
  OD-006).
- **A manual start is audited.** §5 lists "ручной запуск синхронизации";
  every press, accepted or refused, writes one audit row with no personal data
  (SC-11, OD-005). A scheduled run still writes none (US-013 FR-017).

## 2. Business Goal

The Admin's first question after connecting a school is "does it work?" Today
the answer comes up to an hour later, when the scheduled run reaches the
"Last synchronization" block US-017 added. With the button the Admin sees the
result in minutes, while still setting up.

A Dean who knows a teacher has just graded a batch of work can bring the data
up to date before preparing a report, instead of waiting for the interval.

## 3. Business Flow

### 3.1 The Admin, right after connecting

The Admin has saved the connection (US-009) and run "Check access" (US-011).
On the connection page they press "Synchronize". The page answers at once:
"synchronization requested". The run starts in the background. The Admin
reloads the page later; the "Last synchronization" block shows the run as
running, then completed or failed with its diagnosis (US-017).

### 3.2 A Dean on the home page

The Dean presses "Synchronize" on the home page and sees "synchronization
requested". Nothing else changes on the page: the Dean sees no status, time or
diagnosis of any run (OD-002; the state screen is US-024).

### 3.3 A run or a purge is already in progress

The press is accepted; the message says the synchronization will start after
the current one. When the current work ends, exactly one further run starts,
however many presses were made meanwhile (OD-003, OD-004).

### 3.4 Read-only mode

The installation is suspended, past its grace period, or never legitimated.
The button is visible; a press is refused with the read-only reason, as every
blocked action is (`409`, API-5). Nothing is enqueued; no Google call follows.
One audit row records the refused press.

### 3.5 No usable connection

No connection is saved, or the saved one no longer matches the `Installation`
domain (US-009 OD-002). A run requested now would be skipped by the background
service (US-013, `SynchronizationRunOutcome.SkippedConnection`) without any
trace a user can see. What a press answers in this state is **OD-009**, resolved as
option (a): the press is
refused with a message saying the connection must be set up first — for the
Admin "save the connection", for the Dean "ask the Admin" — and the refusal is
audited.

## 4. Functional Requirements

### FR-001 The request-synchronization use case

`RequestSynchronizationUseCase` (name indicative; API_DESIGN and implementation
may refine it) in `Application/UseCases`, in this order:

1. **Read-only guard first** — `IReadOnlyModeGuard.EnsureAllowedAsync` with a
   constant operation name, before anything else is read or enqueued (AD-6,
   US-007 FR-002). On `ReadOnlyModeException` the refusal row of FR-005 is
   written and the exception is rethrown, exactly as `RunAccessCheckUseCase`
   does (US-011 FR-006 step 1); the host maps it to `409` (US-008 FR-014).
2. **Connection state** (OD-009 a): read the state through
   the existing US-009 query; a state that must not be used (`NotConfigured`,
   `DomainMismatch`, `DomainUnknown`) refuses with an outcome naming it, writes
   the refusal row of FR-005, and enqueues nothing. The use case does not
   re-derive the rule (US-011 I-11).
3. **Enqueue** through the port of FR-002 and learn whether other work was in
   progress at that moment (FR-003).
4. **Write the audit row** of FR-005.
5. **Return the outcome**: `Requested` or `RequestedAfterCurrentWork`, or the
   refusal of step 2. Every outcome other than the read-only refusal is a value,
   never an exception (AD-9).

It makes no Google call, opens no Google port and reads no secret: the request
is the whole of its work. The background run keeps its own read-only and
connection checks (US-013 FR-007), so a mode that changes between the press and
the start still stops the run.

### FR-002 The synchronization-request port

`Application` must not reference `ClassroomAgent.Web` (AD-3), where
`SyncRunCoordinator` lives. A port in `Application/Ports`
(`ISynchronizationRequests`, name indicative) offers one operation: request a
run, returning whether a synchronization run or a retention purge was in
progress at the moment of the request. Its implementation is a thin adapter over
the existing `SyncRunCoordinator.Request`, `IsRunning` and `IsPurging`; where the
adapter lives (the host project, next to the coordinator) is fixed by
API_DESIGN within AD-3/AD-4. The coordinator keeps its contract: a request is
always accepted (US-037 API design §3).

### FR-003 The answer during other work

The message distinguishes the two accepted outcomes (OD-003):

- `Requested` — nothing was in progress; the run starts at once.
- `RequestedAfterCurrentWork` — a run or a purge was in progress; the request is
  remembered and the run starts when that work ends.

Which one is shown is read at the moment of the request (I-1). Several presses
collapse into one remembered request (OD-004); each press is still answered and
audited on its own.

### FR-004 The buttons and the endpoint

- **Admin** — the button is on the connection page (`WorkspaceConnection`),
  next to the "Last synchronization" block of US-017 (OD-001).
- **Dean** — the button is on the home page (OD-001). The Admin's home page
  gains no button.
- Both buttons post to **one** state-changing endpoint (`POST`, with the
  antiforgery token under the global rule, API-7). A `GET` never requests a run.
- After the press the user is back on the page they pressed it on, with the
  message of FR-003 or the refusal of §8. The page to return to is derived
  **from the user's role on the server**, never from a value in the request
  (VR-002). Whether the answer is a redirect with a one-time message or a
  re-rendered page, and the status code of a successful press, are fixed by
  API_DESIGN within API-4/API-5 (I-4).
- The button is **visible in read-only mode** and not disabled; enforcement is
  FR-001 step 1 (OD-006, AD-6). The pages already state the read-only reason
  (US-008 FR-019 for the home page; US-009 for the connection page).
- No JSON endpoint is added (OD-007, I-4).
- The "Last synchronization" block does not refresh itself (OD-008).

### FR-005 Audit

`AuditAction` gains one member for a manual start (`SynchronizationRequested`,
name for DB_DESIGN to confirm) — `trebovaniya.md` §5 "ручной запуск
синхронизации", SC-11.

| Row | When | Actor | Outcome | Target |
|---|---|---|---|---|
| request accepted | FR-001 step 4 — both accepted outcomes | the user's `AppUser` id and role (Admin or Dean) | `Succeeded` | none (I-3) |
| refused: read-only | FR-001 step 1 | as above | `Refused`, category `ReadOnlyMode` (existing) | none |
| refused: no usable connection (OD-009 a) | FR-001 step 2 | as above | `Refused`, category `ConnectionNotUsable` (existing) | none |

- `Succeeded` means the **request was accepted**, not that a run happened or
  succeeded (I-2).
- The read-only refusal row is written as US-009 FR-008 / US-011 FR-008
  established: after the guard has thrown, around that commit alone, declaring
  `PermittedServiceWrite.AuditEvent` — audit rows are on the BR-026 list.
  `PermittedServiceWrite` gains no member.
- **No email, no domain, no Google data in any row** (SC-11, PC-9). The row
  carries the request id (SC-11).
- `AuditTargetType` and `AuditRefusalCategory` gain nothing.
- A scheduled run, and the run that a request later starts, write no audit row
  (US-013 FR-017).

### FR-006 Read-only mode

Requesting a run is blocked in read-only mode (BR-026 names synchronization).
In that mode:

- the endpoint answers `409` with the read-only reason through the existing
  handler (US-008 FR-014, API-5, SC-5); this Story adds no second mapping;
- **nothing is enqueued**: the port of FR-002 is not called. Proven in
  `Application` with the port substituted, for all three BR-025 causes (TC-5);
- the pages that carry the button stay viewable.

### FR-007 Authorization

`InstallationPolicies` gains a policy for the matrix row "Запуск синхронизации"
— ✔ Admin, ✔ Dean (`trebovaniya.md` §2, BR-004) — `StartSynchronization`
(name indicative). It is **its own policy** (I-5), declared by the endpoint of
FR-004.

- An anonymous visitor is sent to sign in (SC-4); the SC-4 anonymous closed list
  gains nothing, and the endpoint enumeration test of US-008 passes with the new
  endpoint classified as protected.
- A Dean held on the forced password change (US-012) is refused as on every
  other page until the password is changed.
- No other matrix cell changes: the connection page stays Admin-only
  (`ConfigureWorkspaceConnection`); the Dean's diagnosis stays hidden (US-017
  AC-007).

### FR-008 Localization

Every string this Story adds exists in both `SharedResource.uk.resx` and
`SharedResource.en.resx` (NFR-073): the button label, the two accepted messages
of FR-003, and the refusal messages of §8 (with Admin and Dean wording where
OD-009 (a) makes them differ). Ukrainian is the default. A test fails on a key
present in one file and missing from the other (existing rule).

### FR-009 Persistence

- **No new table, no new column.**
- `AuditAction` gains one member. The audit table constrains that column
  (`ck_audit_event_action`), so the constraint is amended by an EF Core migration
  in this Story (PC-2), as US-011 did for `AccessCheckRun`; DB_DESIGN names it.
- `DbContext` appears in neither `Application` nor `Web` (AD-3).

### FR-010 Logging

- An accepted request logs one line at `Information` with the actor's account
  id, the request id and which accepted outcome it was.
- A refused request logs one line at `Warning` with the refusal category and the
  request id.
- No log line carries an email, a domain, or anything from Google (SC-10,
  DC-10).

## 5. Acceptance Criteria

The Story's eight criteria, unchanged in meaning; ids are the Story's.

| Id | Criterion | Specified by |
|---|---|---|
| AC-001 | The Admin requests a run from the connection page | FR-001, FR-004, FR-005 |
| AC-002 | The Dean requests a run from the home page | FR-001, FR-004, FR-005 |
| AC-003 | A press during a run or a purge is remembered | FR-002, FR-003 |
| AC-004 | Repeated presses collapse into one run | FR-002, FR-003 |
| AC-005 | Read-only mode refuses the press | FR-001 step 1, FR-006 |
| AC-006 | Only Admin and Dean may request a run | FR-004, FR-007 |
| AC-007 | The UI is bilingual | FR-008 |
| AC-008 | Tests never reach Google | FR-001, FR-002, TC-4 |

One criterion is derived from OD-009 (a):

| Id | Criterion | Specified by |
|---|---|---|
| AC-009 | Without a usable connection the press is refused (OD-009 a): nothing is enqueued, the message says what to do first, one `Refused` / `ConnectionNotUsable` audit row is written | FR-001 step 2, FR-005 |

## 6. Validation Rules

### VR-001 The request carries no input

The `POST` carries the antiforgery token and nothing else. A value submitted
under any field name is ignored — the request model has no property to bind it
to. A missing or invalid antiforgery token is `400` under the global rule
(API-7).

### VR-002 No return address from the request

The page the user is returned to is chosen on the server from the user's role
(Admin → connection page, Dean → home page). No `returnUrl`, referrer or other
request value decides it, so the endpoint cannot become an open redirect.

### VR-003 The actor

The actor of the audit row and of the log line is the signed-in account from the
session, never a value from the request.

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | The endpoint declares its own policy granted to Admin and Dean; anonymous access is impossible and the SC-4 closed list gains nothing. | SC-4, §2, BR-004 |
| S-02 | Allowed-role cases (Admin, Dean) and a forbidden case (anonymous; a Dean on the forced password change) are tested. | SC-1, TC-5 |
| S-03 | In read-only mode nothing is enqueued, proven in `Application` with the port substituted, for all three BR-025 causes. | SC-5, BR-026, AD-6 |
| S-04 | The press makes no Google call and reads no secret; the run it requests keeps every rule of US-013 … US-017. | AD-5, SC-7, SC-8 |
| S-05 | The action is a `POST` with the antiforgery token; a `GET` requests nothing. | SC-4, API-7 |
| S-06 | The return page is derived from the role, never from the request. | VR-002 |
| S-07 | Every press — accepted or refused — writes one audit row with no personal data. | SC-11, §5 |
| S-08 | A Dean is shown no status, time or diagnosis of any run. | US-017 AC-007, OD-002 |
| S-09 | No new outbound destination. | SC-13 (Hard Stop) |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous request | Redirected to sign-in (SC-4). |
| Missing or invalid antiforgery token | `400` under the global rule (API-7). |
| Read-only mode | `409` with the read-only reason (US-008 FR-014); audit row `Refused` / `ReadOnlyMode`; nothing enqueued. |
| No usable connection (OD-009 a) | Refused as a state conflict — the answer shape (`409` with the page and the message, as US-011 answers an unusable connection) is fixed by API_DESIGN within API-5; audit row `Refused` / `ConnectionNotUsable`; nothing enqueued. |
| A run or purge in progress | **Not an error**: accepted, `RequestedAfterCurrentWork`. |
| The run itself later fails | Not this endpoint's concern; recorded in `SyncState` and shown to the Admin by US-017. |
| An unexpected failure | The single exception handler answers the error page with no detail (AD-9, API-10, SC-10). |

There is no rate-limit refusal (OD-004).

## 9. Non-Functional Requirements

- **BR-040 / AD-5** — the request returns at once; the web request never waits
  for a run.
- **NFR-073** — every message translated (FR-008).
- **NFR-062** — .NET 10, C#, nullable enabled, warnings as errors.
- **NFR-070** — the button and message are usable on a phone.

## 10. Out of Scope

- A JSON endpoint `POST /api/v1/sync` (OD-007, I-4).
- Anything a Dean sees about runs — US-024.
- Automatic refresh of "Last synchronization" (OD-008).
- A rate limit or cooldown (OD-004).
- A separate Meet pull — EPIC-4.
- Any change to how a run works: schedule, retries, diagnosis (US-013, US-017),
  incremental behaviour (US-018).
- Anything in the Control Plane, the service channel or
  `ClassroomAgent.Contracts`.

## 11. Open Decisions

Full text, options and resolutions in `docs/decisions/US-019-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 Where the button lives | **RESOLVED** 2026-10-04 (a) | none — FR-004 |
| OD-002 What the Dean sees after a press | **RESOLVED** 2026-10-04 (a) | none — FR-004, S-08 |
| OD-003 A press while a run or purge is in progress | **RESOLVED** 2026-10-04 (a) | none — FR-003 |
| OD-004 Many presses in a row | **RESOLVED** 2026-10-04 (a) | none — FR-003 |
| OD-005 What is audited | **RESOLVED** 2026-10-04 (a) | none — FR-005 |
| OD-006 The button in read-only mode | **RESOLVED** 2026-10-04 (a) | none — FR-004, FR-006 |
| OD-007 A JSON API endpoint | **RESOLVED** 2026-10-04 (a) | none — FR-004, I-4 |
| OD-008 Automatic refresh | **RESOLVED** 2026-10-04 (a) | none — FR-004 |
| OD-009 A press with no usable connection | **RESOLVED** 2026-10-04 (a) — raised by SPECIFICATION | none — FR-001 step 2, FR-005, §8, AC-009 |

No item of `trebovaniya.md` §7 is touched; item 27 (re-import of a purged
leaver) concerns what a run imports, not how it is started.

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally; each
can be corrected at `HUMAN_SPEC_APPROVAL`.

- **I-1 The message reflects the moment of the press.** Whether other work is in
  progress is read together with the request. If the current run ends a moment
  later, the message "after the current synchronization" was still true when
  given; no guarantee beyond that is made.
- **I-2 What `Succeeded` in the audit means.** §5 audits the *start* — the press.
  The row records that the request was accepted, not what the run did; the run's
  result lives in `SyncState` (BR-044).
- **I-3 No audit target.** A remembered request has no `SyncState` row of its own,
  and the single `SyncState` row may not exist before the first run; the row
  therefore has no target, as the retention purge row has none. DB_DESIGN may
  decide otherwise only by adding a target type, which this Specification does
  not require.
- **I-4 API-4's `POST /api/v1/sync` row.** `api-conventions.md` API-4 lists
  `POST /api/v1/sync` → `202 Accepted` "with the `SyncState` id". OD-007 adds no
  JSON endpoint, and a remembered request has no `SyncState` id to return. This
  Story's endpoint is the server-rendered form `POST` §8 refers to; API-4's row
  stays a convention for a future JSON endpoint and is not implemented here.
  Whether API-4 should be reworded is a non-blocking finding for the Owner.
- **I-5 Its own policy.** As US-010 I-7 and US-011 I-9: one policy per matrix
  row. "Запуск синхронизации" is the first row granted to both roles that writes
  something, so neither `AuthenticatedUser` nor `ViewLegitimacyStatus` stands in
  for it.
- **I-6 The Admin's home page has no button.** OD-001 places the Admin's button
  on the connection page, where the result appears; one button per role keeps
  the pages simple.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-001, FR-004, FR-005 | VR-001, VR-002, VR-003 | S-01, S-05, S-06, S-07 |
| AC-002 | FR-001, FR-004, FR-005 | VR-001, VR-002, VR-003 | S-01, S-07, S-08 |
| AC-003 | FR-002, FR-003 | — | S-04 |
| AC-004 | FR-002, FR-003, FR-005 | — | S-07 |
| AC-005 | FR-001, FR-006 | — | S-03, S-07 |
| AC-006 | FR-004, FR-007 | VR-001 | S-01, S-02, S-05 |
| AC-007 | FR-008 | — | — |
| AC-008 | FR-001, FR-002 | — | S-04, S-09 |
| AC-009 (derived, OD-009 a) | FR-001, FR-005 | — | S-07 |
| — (audit enum, migration, logging) | FR-009, FR-010 | — | S-07 |

Requirement sources: `trebovaniya.md` v81 §2, §4 (Epic 1), §5, §8; BR-004,
BR-025, BR-026, BR-040, BR-044; NFR-062, NFR-070, NFR-073; AD-3, AD-4, AD-5,
AD-6, AD-8, AD-9; API-4, API-5, API-7, API-10; SC-1, SC-4, SC-5, SC-7, SC-8,
SC-10, SC-11, SC-13; PC-2, PC-9; DC-10; TC-4, TC-5.
