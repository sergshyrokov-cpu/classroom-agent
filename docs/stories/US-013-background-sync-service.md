---
id: US-013
epic: EPIC-1
title: Background synchronization service
slug: background-sync-service
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v79.
# OD-001 … OD-007 were all resolved by the Owner on 2026-09-27, before
# activation.
---

# User Story

As the **installation itself**

I want a background service that runs synchronization on its own schedule, one
run at a time, and records in `SyncState` what it is doing, when it last
succeeded and what went wrong

So that the school's data is refreshed without anyone pressing a button and
without a web request ever waiting for Google — and so that the Owner, reading
readiness and `SyncState`, can tell whether a school is working at all.

---

# Business Value

`trebovaniya.md` §5 asks for it as a performance requirement and names the defect
it replaces: "синхронизация не должна блокировать UI — фоновый процесс
(в .NET — `IHostedService` / `BackgroundService`, а не поток в веб-запросе, как в
Streamlit)". The prototype synchronizes inside the request that asked for it; the
.NET system must not.

§4 Epic 1 asks for the periodic run ("периодически (или по запросу)") and for
`SyncState` to carry "время последней синхронизации, статуса и ошибок". §8 makes
`SyncState` half of the Owner's answer to "работает ли школа": "готовность плюс
`SyncState` отвечают на вопрос «работает ли школа»", because there are no metrics
and no centralized log collection in the first version.

This Story is the **host every later synchronization Story plugs into**: US-014
(courses and rosters), US-015 (coursework and submissions), US-017 (retry,
backoff and permission errors), US-018 (incremental sync) and US-019 (a run
started from the UI) all depend on the run lifecycle, the schedule and the state
record this Story establishes. It is also the Story that makes readiness
truthful: DC-11 requires `Unhealthy` when "фоновый сервис синхронизации не
запущен", and until this service exists there is nothing for readiness to check.

---

# Scope

**In scope:**

- a synchronization `BackgroundService` hosted by `ClassroomAgent.Web`, beside
  the legitimacy-check service US-005 established (AD-5, NFR-001);
- the run lifecycle: a run starts, is the only one running, and finishes as
  completed or failed;
- the schedule on which a run starts, on the injected clock (OD-002, OD-003,
  OD-006);
- `SyncState` — the entity, its table, its migration, and the rule that it is the
  only place run progress, status, counters, last error and last successful run
  are read from (BR-044, PC-10, OD-004);
- read-only mode: no run starts, no call reaches Google, and nothing is written
  (BR-026, SC-5, AD-6, OD-005);
- no `WorkspaceConnection` saved: no run starts, for the same reason US-011
  refuses its check (AC-005);
- logging per DC-10: run start and finish at `Information` with counters, every
  line of a run carrying that run's identifier, no personal data and no raw
  Google error (SC-10);
- readiness reports `Unhealthy` when this service is not running (DC-11);
- a clean stop at host shutdown — no new run starts and the current one is
  cancelled through its `CancellationToken`.

**Out of scope:**

- importing any entity — courses and rosters are **US-014**, coursework and
  submissions **US-015**; what this Story runs is the pipeline, not its steps
  (OD-001);
- retry with exponential backoff and the separation of transient failures from
  permission failures — **US-017**; this Story records a failed run with the
  message it was given and schedules the next one, it does not classify Google
  errors (AD-5);
- incremental behaviour — **US-018**;
- starting a run from the UI, its endpoint and its audit row — **US-019**;
- showing `SyncState` on a screen — Epic 5, **US-024**;
- pulling Meet events — **US-031**;
- the retention purge, a separate daily background service — **US-037** (PC-11);
- any write to Google Workspace — a Hard Stop, not a scope choice;
- the six OAuth scopes, the service-account key and impersonation, all
  established by **US-011** and reused unchanged.

---

# Acceptance Criteria

## AC-001 The service is hosted, and readiness knows whether it runs

**Given** the installation starts

**When** the host comes up

**Then**:

- a synchronization `BackgroundService` is registered in `ClassroomAgent.Web` and
  started by the host (AD-5, NFR-001);
- readiness answers `Unhealthy` when that service is not running, in addition to
  the existing database probe (DC-11, `GetReadinessQuery`);
- read-only mode still answers `Degraded`, not `Unhealthy` — a school in
  read-only mode whose sync service deliberately runs no run is not broken
  (DC-11, DC-7);
- no synchronization work happens inside any web request (NFR-001).

## AC-002 A run starts on a schedule, and only one runs at a time

**Given** the service is running

**When** the schedule says a run is due

**Then**:

- exactly one run starts; while it runs, no second run starts, however the
  schedule and any later trigger interact (AD-5);
- the waiting is done on the injected `TimeProvider`, so tests advance time
  instead of sleeping (TC-*, the US-005 precedent);
- the interval is taken from the single place OD-002 fixes — never a literal
  scattered through the code (AD-10).

## AC-003 `SyncState` records the run and nothing else does

**Given** a run starts and finishes

**When** its state is read

**Then**:

- `SyncState` carries the status (running / completed / failed), the counters,
  the last error and the time of the last successful run
  (`trebovaniya.md` §3, BR-044);
- the status reflects a run that is in progress while it is in progress, and its
  outcome once it ends;
- progress is never inferred by counting rows in another table (PC-10);
- the entity ships with its EF Core migration in this Story (PC-2).

## AC-004 In read-only mode nothing runs, nothing is called, nothing is written

**Given** an installation in read-only mode — suspended, past its grace period,
or never legitimated (BR-025)

**When** the schedule would start a run

**Then**:

- no run starts;
- no call is made to Google, including the token request — proven in the
  Application layer with the Google port substituted (SC-5, TC-4, TC-5);
- **nothing is written**: `SyncState` is not touched, because a synchronization
  write is not on the BR-026 closed list of service writes;
- the refusal is enforced in the Application layer through `IReadOnlyModeGuard`,
  called before anything else, never by not registering the service (AD-6, SC-8,
  the US-007 F-5 finding);
- that the run was skipped and why is visible in the log only (OD-005);
- the service keeps running and evaluates the schedule again, so the school
  resumes synchronizing by itself once it leaves read-only mode.

## AC-005 Without a saved connection there is nothing to synchronize

**Given** no `WorkspaceConnection` has been saved yet

**When** the schedule would start a run

**Then** no run starts, no Google call is made, nothing is written, and one log
line says the run was skipped for that reason — the same shape US-011 gave its
own missing-connection case (AC-005 there, OD-005 here).

## AC-006 A failed run is recorded and does not stop the service

**Given** a run that fails

**When** it ends

**Then**:

- `SyncState` records the failure with a diagnosable message and keeps the time
  of the last **successful** run untouched (§4 Epic 1, BR-044);
- the message holds no personal data and no raw Google error text (SC-10, SC-8);
- the service does not stop, and the next run is scheduled per OD-003;
- an unhandled exception inside a run never takes the host down (the US-005
  precedent: "nothing a check does stops the service or the host");
- classifying the failure as transient or as a permission failure, and retrying
  it, belong to US-017 — this Story records what it is told.

## AC-007 Every line of a run is traceable and carries no personal data

**Given** a run

**When** it starts and finishes

**Then**:

- start and finish are logged at `Information`, the finish line carrying the
  counters (DC-10);
- every line written by a run carries that run's identifier, so one run can be
  read end to end (DC-10, `trebovaniya.md` §8);
- no line carries a name, an email, a grade, a key, a connection string or a raw
  Google error object (SC-10);
- a scheduled run writes **no audit row** — `trebovaniya.md` §5 audits the
  *manual* start of a synchronization, which is US-019.

## AC-008 Shutdown is clean

**Given** a run in progress

**When** the host is stopping

**Then** no new run starts, the running one is cancelled through its
`CancellationToken`, and the shutdown is not reported as a failed run.

## AC-009 Tests never reach Google and never sleep

**Given** the test suite

**When** it runs

**Then**:

- every Google call goes through the substituted port with synthetic answers
  (TC-4);
- the schedule is tested by advancing the injected clock, and log assertions wait
  for the event rather than reading the log immediately after a stub returns (the
  US-005 / US-006 helpers);
- both the allowed case and the refused cases (read-only, no connection) are
  covered in the Application layer (TC-5);
- `SyncState` is tested against real PostgreSQL through Testcontainers (TC-2).

---

# Open Decisions

## OD-001 Does this Story import anything, or only run the pipeline?

The catalog plans US-013 as the service and US-014 / US-015 as the data. A
service with no steps completes a run with zero counters, which is testable but
imports nothing a Dean can see.

Options:

1. **Host only (recommended).** This Story delivers the service, the schedule,
   the run lifecycle, `SyncState`, the read-only refusal, the logging and the
   readiness contribution. A run executes an empty pipeline and completes with
   zero counters; US-014 adds the first step. Keeps both Stories the size the
   catalog planned and keeps the first real Google data call in the Story that
   owns the entities.
2. Merge US-014 into US-013, so the first run imports courses and rosters. One
   large Story, and the catalog entry for US-014 is retired.

**Resolution:** option 1, decided by the Owner on 2026-09-27. This Story delivers
the host; US-014 adds the first pipeline step. A run completing with zero
counters is the expected outcome until then, and the Specification must not treat
it as a defect.

## OD-002 How often a scheduled run starts, and where the interval lives

`trebovaniya.md` says only "периодически"; no document fixes a number. §6 notes
the schools share one Cloud project's quota and that a periodic (rather than
on-demand) sync should not make that a bottleneck.

Options:

1. **An optional installation setting with a default (recommended).** The Owner
   can tune one school without a rebuild; DC-3 grows by one optional key, like
   the default UI language. The Specification fixes the default.
2. A constant in the code, as the legitimacy check's 6 hours and 15 minutes are
   (those come from BR-024, which fixes them; nothing fixes this one).
3. A required setting — an installation without it refuses to start. Consistent
   with the retention period and the time zone, but it is not a value the school
   agreed in writing, so a deployment would fail over a tuning parameter.

**Resolution:** option 1, decided by the Owner on 2026-09-27. DC-3 gains one
optional installation setting; the Specification fixes its name and its default,
and an installation without it starts and synchronizes on that default.

## OD-003 What the schedule does after a failed run

Options:

1. **The same interval as after a success (recommended).** Simplest, and the
   failures this Story can see are not the ones worth hurrying — US-017 owns
   retrying the call that actually failed.
2. A shorter interval after a failure, as the legitimacy check uses 15 minutes
   against 6 hours.
3. Exponential backoff between runs.

**Resolution:** option 1, decided by the Owner on 2026-09-27. One interval, used
after a success and after a failure alike.

## OD-004 One `SyncState` row, or a history of runs

`trebovaniya.md` §3 describes `SyncState` in the singular — status, counter, last
error, last sync time.

Options:

1. **One row, updated in place (recommended).** Exactly what §3 and Epic 5
   describe; no growth, no purge rule to write, and BR-044 stays literally true.
2. One row per run, with the current state read as the latest. Gives a history
   nobody has asked for, and it would need a retention rule of its own (PC-11
   says nothing about it).

**Resolution:** option 1, decided by the Owner on 2026-09-27. One row, updated in
place, as `trebovaniya.md` §3 describes it.

## OD-005 What the service records when it cannot run

A run skipped because of read-only mode or a missing connection is worth knowing
about — the Owner reading the log must not mistake silence for success (the same
reason US-011 OD-004 gave).

Options:

1. **A log line only (recommended).** BR-026 permits no synchronization write in
   read-only mode, and its closed list can be extended only by a new version of
   `trebovaniya.md` §2 — so writing the skip into `SyncState` is not available to
   this Story. Which level the line uses is the Specification's to fix within
   DC-10.
2. Nothing at all.

**Resolution:** option 1, decided by the Owner on 2026-09-27. A log line and
nothing else — no `SyncState` write, and BR-026's closed list is not touched.

## OD-006 Whether the first run happens at startup

The legitimacy-check service checks immediately at start. A synchronization run
at start would find the installation read-only, because an installation that has
never completed a successful check **is** read-only (BR-025) — so an unconditional
run at startup would always be skipped on a fresh installation.

Options:

1. **Wait for the first legitimacy determination, then run (recommended).** The
   first run happens as soon as the installation knows it is legitimate, which is
   seconds after start, and a fresh installation synchronizes without waiting a
   whole interval.
2. Run at startup unconditionally and let the read-only guard skip it, then
   follow the interval.
3. Do not run at startup; wait one interval.

**Resolution:** option 1, decided by the Owner on 2026-09-27. The service waits
for the first legitimacy determination after start and then runs, so a fresh
installation synchronizes within seconds of becoming legitimate instead of
waiting a whole interval.

## OD-007 Is the trigger seam built now or in US-019?

AD-5 says a request that starts a sync "enqueues work and returns immediately".
The endpoint and its audit row are US-019.

Options:

1. **Build the seam now (recommended).** A coordinator that guarantees one run
   at a time and accepts an out-of-schedule request, exactly as
   `PushCheckCoordinator` does for US-006. AC-002 needs the one-at-a-time
   guarantee anyway, so US-019 then adds only an endpoint, a policy and an audit
   row.
2. Leave it to US-019, and let this Story guarantee one-at-a-time against the
   schedule alone.

**Resolution:** option 1, decided by the Owner on 2026-09-27. The coordinator is
built here and guarantees one run at a time; US-019 adds only the endpoint, its
policy and its audit row.

---

# Notes

- **The US-005 service is the precedent to follow**, not a thing to copy blindly:
  `LegitimacyCheckBackgroundService` already establishes the shape — a scoped
  service factory per run, waiting on `TimeProvider`, one operation at a time
  through a coordinator, `OperationCanceledException` on shutdown treated as a
  normal stop, and log lines carrying categories and states only.
- **`IGoogleDataPort` is a marker with a rule** (US-007 spec FR-007): a use case
  holding one must also take `IReadOnlyModeGuard` and call it first. Whatever
  port a run eventually calls is bound by it, and AC-004 is where that is proven
  for this Story.
- **No new Google scope, no new key handling.** US-011 established the six scopes
  as a constant in `Domain/Rules`, the key through `ISecretStore` from the
  configured reference, and impersonation of the `WorkspaceConnection`'s
  technical account (BR-015, SC-7, SC-8). This Story adds nothing there and must
  not introduce a second copy of any of it.
- **No user-visible string.** Nothing in this Story reaches a screen — the
  `SyncState` display is Epic 5 (US-024) and the manual trigger is US-019 — so it
  adds no translation keys. If the Specification finds a message that does reach
  a user, NFR-073 applies in full and both language files grow together.
- **Audit stays untouched.** A scheduled run writes no audit row (AC-007), so
  `AuditAction`, `AuditTargetType` and their check constraints do not grow, and
  no constraint-amending migration is needed for them. US-019 adds the manual
  start.
- **PC-10 already describes synchronization behaviour** that this Story does not
  implement: idempotent upsert on Google ids, the incremental rule, and the
  not-yet-imported-course age rule. They belong to US-014, US-015 and US-018;
  DB_DESIGN should not pull them forward into `SyncState`.
- **Readiness is an existing query.** `GetReadinessQuery` returns `Unhealthy`
  only for a database failure today; AC-001 extends it. That is a change to code
  US-005 owns, and the `implementation_report` must trace it.
- Nothing in this Story touches the Control Plane, the service channel or
  `ClassroomAgent.Contracts`.
