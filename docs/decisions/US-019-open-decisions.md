---
artifact_type: open_decisions
story: US-019
version: 1
status: DRAFT
created_at: 2026-10-04T06:28:44Z
updated_at: 2026-10-04T06:31:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-019-trigger-sync-from-ui.md
    version: null
  - path: trebovaniya.md
    version: 81
supersedes: null
---

# US-019 Open Decisions — Trigger a synchronization from the UI

Eight decisions (OD-001 … OD-008) were written into the Story by the author and
**resolved by the Owner on 2026-10-04, before activation**, each as the
recommended option (a). They are carried here with their ids and resolutions
unchanged.

Writing the Specification raised **one new Open Decision, OD-009**, resolved by
the Owner on 2026-10-04 as option (a). The other choices are
interpretations I-1 … I-6 in `docs/specifications/US-019-spec.md` §11.

---

## Raised by SPECIFICATION

### OD-009 A press with no usable connection

**Context.** The Story's Notes leave it to the Specification. When no
`WorkspaceConnection` is saved, or the saved one no longer matches the
`Installation` domain (US-009 OD-002), the background run is skipped with a log
line and no `SyncState` change (US-013; US-017 I-8). A press in that state
therefore produces nothing any user can see. "Check access" refuses in the same
state (US-011 FR-006 step 2) with an audit row `Refused` / `ConnectionNotUsable`.

**Options.**

- **(a) Refuse the press, like "Check access" (recommended).** Nothing is
  enqueued; the Admin sees "save the connection first", the Dean "the
  connection is not set up — ask the Admin"; one audit row `Refused` /
  `ConnectionNotUsable` (existing category). Answer shape as US-011: `409` with
  the page. Adds AC-009.
- **(b) Accept the press.** The request is enqueued and audited as accepted; the
  run is then skipped by the background service and only the log records why.
  The user is told "synchronization requested" although nothing will run.

**Impact.** Specification FR-001 step 2, FR-005 (third row), §8 (fourth row) and
derived AC-009 are written for (a); under (b) they are removed.

**Resolution:** (a) — resolved by the Owner on 2026-10-04, before HUMAN_SPEC_APPROVAL.

## Raised by TEST_WRITING

### OD-010 Compile-only skeleton created at TEST_WRITING

**Context.** The tests must call types and members that do not exist yet; the
test-writer may not modify production behaviour, and no artifact says who
creates them — the same gap every earlier Story resolved for itself.

**Options.**

- **(a) A compile-only skeleton (recommended)** — only what the tests reference,
  every behavioural member throwing `NotImplementedException`, nothing registered
  in dependency injection, no existing behaviour changed:
  `AuditAction.SynchronizationRequested`; `AuditEvent.SynchronizationRequested`
  and `SynchronizationRequestRefused` (throwing);
  `Application/Ports/ISynchronizationRequests.RequestAsync` returning
  `Application/Models/SynchronizationRequestTiming { StartsNow, AfterCurrentWork }`;
  `Application/Models/RequestSynchronizationOutcome` (a declaration);
  `Application/UseCases/RequestSynchronizationUseCase.ExecuteAsync(actorId,
  actorRole, requestId, ct)` (throwing);
  `Web/BackgroundServices/CoordinatorSynchronizationRequests` (throwing).
- **(b) No skeleton** — tests over HTTP and SQL only; the read-only refusal
  could not be proven with the port substituted (TC-5).

**Resolution:** (a) — resolved by the Owner on 2026-10-04.

---

## Resolved before activation

### OD-001 Where the button lives

Options: (a) Admin — on the connection page next to "Last synchronization";
Dean — on the home page; (b) both on the home page only; (c) the Dean's button
waits for US-024.

**Resolution:** (a).

### OD-002 What the Dean sees after a press

Options: (a) only the confirmation "synchronization requested"; (b) also the
time of the last successful run.

**Resolution:** (a) — the diagnosis is Admin-only (US-017 AC-007), and the
state screen is US-024.

### OD-003 A press while a run or a purge is in progress

Options: (a) accept and remember it; the run starts after the current work;
(b) answer "already running" and remember nothing.

**Resolution:** (a) — the behaviour the coordinator already has (US-013
OD-007, US-037 API design).

### OD-004 Many presses in a row

Options: (a) they collapse into one remembered request; no rate limit;
(b) a cooldown, e.g. once a minute per user.

**Resolution:** (a).

### OD-005 What is audited

Options: (a) every accepted press and every press refused in read-only mode;
(b) accepted presses only.

**Resolution:** (a) — as "Check access" (`AccessCheckRun`) records a run and a
refusal.

### OD-006 The button in read-only mode

Options: (a) visible; a press is refused with a message naming the reason;
(b) hidden (server-side refusal still required, AD-6).

**Resolution:** (a) — as "Check access".

### OD-007 A JSON API endpoint

Options: (a) none — a form `POST` like every other screen today; (b) a JSON
`POST /api/v1/sync` per API-4.

**Resolution:** (a). How this relates to API-4's row is Specification I-4.

### OD-008 Automatic refresh of "Last synchronization"

Options: (a) none — the Admin reloads the page; (b) the page polls the state.

**Resolution:** (a).
