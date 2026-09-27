---
artifact_type: specification
story: US-013
version: 1
status: APPROVED
created_at: 2026-09-27T08:50:44Z
updated_at: 2026-09-27T11:52:20Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-013-background-sync-service.md
    version: null
  - path: trebovaniya.md
    version: 79
  - path: docs/decisions/US-013-open-decisions.md
    version: 1
supersedes: null
---

# US-013 Specification — Background synchronization service

## 1. Overview

US-009 recorded which domain and which technical account the installation reads
the school's data as; US-011 proved those reads work and made the first real call
to Google. Neither brought a single row of teaching data into the installation's
database, and nothing in the program yet runs on its own initiative.

This Story builds the thing that will: a background service that decides when a
synchronization run happens, runs exactly one at a time, and records in
`SyncState` what it did, when it last succeeded and what went wrong
(`trebovaniya.md` §5, §4 Epic 1, §3). It imports nothing — OD-001 scoped it to the
host, and the first pipeline step arrives with US-014. What a Dean sees therefore
changes only with that Story; what the Owner sees changes here, because readiness
starts telling the truth about whether a school's synchronization is running at
all (§8, DC-11).

The Story also retires a named defect of the prototype. Streamlit synchronizes
inside the request that asked for it; §5 requires "фоновый процесс (в .NET —
`IHostedService` / `BackgroundService`, а не поток в веб-запросе, как в
Streamlit)". After this Story no synchronization code is reachable from a web
request at all.

## 2. Business Goal

**For the school:** data refreshes without anyone pressing a button — the periodic
run §4 Epic 1 asks for, so a Dean opening the journal is not looking at whatever
the last person happened to synchronize.

**For the Owner:** an answer to "работает ли школа". §8 is explicit that with no
metrics and no centralized log collection in the first version, "готовность плюс
`SyncState`" is that answer. Readiness cannot give it while nothing checks the
synchronization service, and `SyncState` cannot give it while nothing writes it.

**For the program:** the host every later synchronization Story plugs into. US-014,
US-015, US-017, US-018 and US-019 all depend on the run lifecycle, the schedule,
the one-run-at-a-time guarantee and the state record established here.

## 3. Business Flow

### 3.1 A school that has just been configured

The Admin saves the connection (US-009) and confirms access (US-011). The
installation starts; the service waits for the first legitimacy determination
(FR-011), finds the installation legitimate and a connection saved, and starts a
run. The run executes an empty pipeline and completes; the `SyncState` row records
a completed run with a zero counter and the instant it succeeded. Nothing is
visible to a user yet — US-024 shows the row, US-014 gives it something to count.

### 3.2 A normal day

The interval elapses, a run starts, completes, and the interval starts again from
its completion. Two log lines per run, both carrying the run identifier.

### 3.3 A run fails

The run ends as failed. `SyncState` keeps a diagnosable message and leaves the
instant of the last **successful** run untouched, so the Owner sees both that the
school last succeeded on Tuesday and that it has been failing since. The service
does not stop, and the next run is scheduled on the same interval (OD-003).
Classifying the failure as transient or as a permission failure, and retrying it,
is US-017.

### 3.4 Read-only mode

The `Installation` is suspended, or the grace period has expired, or no legitimacy
check has ever succeeded (BR-025). The schedule comes due, the guard refuses, no
run starts, and **nothing is written** — a synchronization write is not on BR-026's
closed list. One log line says the run was skipped and why (OD-005). The service
keeps evaluating the schedule, so the school resumes synchronizing by itself when
the Owner resumes it.

### 3.5 No connection saved

A freshly deployed installation whose Admin has not configured the connection yet.
There is nothing to synchronize through: no run starts, no Google call, nothing
written, one log line — the shape US-011 gave its own missing-connection case.

### 3.6 Shutdown

The host stops. No new run starts; a run in progress is cancelled through its
`CancellationToken`, and that cancellation is not recorded as a failed run
(FR-010, I-3).

## 4. Functional Requirements

### FR-001 What a synchronization run is

- A **run** is the unit of work: it has an identifier, a start instant, an end
  instant, a status and one counter (`trebovaniya.md` §3 — "счётчик").
- The identifier is generated inside the process when the run starts and is a
  value no external input can influence (VR-003). It exists so one run can be read
  end to end in the log (DC-10).
- The run executes a **sequence of pipeline steps**. Under OD-001 that sequence is
  empty in this Story: the run starts, executes no step, and completes with the
  counter at zero. **A completed run with a zero counter is the expected outcome
  and is not a defect** (I-1). US-014 adds the first step without changing the
  lifecycle.
- The counter reports how many items the run processed. With an empty pipeline it
  is zero.

### FR-002 `SyncState`

- `SyncState` is a Domain entity and the **only** place a reader learns run
  progress, status, the last error and the last successful run from (BR-044,
  PC-10). No use case infers progress by counting rows in another table.
- Exactly **one row** exists, updated in place (OD-004). The row is created by the
  first run that starts; **its absence means the installation has never
  synchronized** (I-2), which needs neither a fourth status value nor a seeded row.
- The row carries: the status, the identifier of the current or last run, the start
  instant, the end instant of the last finished run, the counter, the last error,
  and the instant of the last **successful** run.
- The status vocabulary is exactly the three values `trebovaniya.md` §3 names:
  `Running`, `Completed`, `Failed`.
- The instant of the last successful run is written only by a run that completed;
  a failed run never touches it (FR-008).
- The entity holds no policy number: the interval is not a field of it and never
  reaches it (AD-3; the US-012 precedent, where the lockout numbers arrive as
  arguments).

### FR-003 The schedule

- A run becomes due one **interval** after the completion of the previous run, as
  the legitimacy check counts its own wait from completion (US-005 FR-003). A run
  that takes longer than the interval therefore never causes runs to queue up.
- The interval is a single value taken from configuration (FR-013). It is never a
  literal in more than that one place (AD-10).
- **The same interval is used after a successful run and after a failed one**
  (OD-003).
- The wait runs on the injected `TimeProvider`, so tests advance time instead of
  sleeping (TC-6, the US-005 precedent).
- The first run of a process happens after the first legitimacy determination
  (FR-011), not at the moment the host starts.

### FR-004 One run at a time

- A **coordinator**, a process-wide singleton, guarantees that at most one run is
  in progress. Its responsibilities: begin a scheduled run when one is due and
  none is in progress; accept an out-of-schedule request; report a run as
  finished. Member names are the design's; the guarantee is not.
- While a run is in progress, no second run starts, whatever asks — the schedule
  or a request (OD-007). A request arriving during a run is remembered or refused
  by the design's choice, never served by a parallel run.
- The out-of-schedule entry point exists but **nothing calls it in this Story**:
  US-019 adds the endpoint, its authorization policy and its audit row. The shape
  to follow is `PushCheckCoordinator`, which does the same job for US-006.

### FR-005 The run use case

- One Application use case owns a run. Indicative name
  `RunSynchronizationUseCase`; it returns an outcome with exactly three shapes,
  the shape `StartupSelfCheckOutcome` already has: **ran** (with the run's result),
  **skipped because read-only** (with the reason), **skipped because of the
  connection** (with the connection state).
- Order of steps, and it is binding:
  1. `IReadOnlyModeGuard.EnsureAllowedAsync` with this use case's own operation
     name. A `ReadOnlyModeException` becomes the *skipped — read-only* outcome, not
     an error (the `RunStartupSelfCheckUseCase` precedent).
  2. The saved `WorkspaceConnection`. Not usable → the *skipped — connection*
     outcome.
  3. Only then the run: `SyncState` moves to `Running`, the pipeline executes, and
     the run finishes (FR-006).
- The use case calls no Google API in this Story, because the pipeline is empty
  (OD-001). Whatever port a later step introduces is an `IGoogleDataPort` and is
  bound by its rule — the guard is called first, which step 1 already satisfies
  (US-007 FR-007, the US-007 F-5 finding).

### FR-006 Writing `SyncState`

- The write goes through a new port in `Application/Ports` (indicative name
  `ISyncStateRepository`) implemented in `Infrastructure`, committed through
  `IUnitOfWork` (AD-3, AD-4).
- Two writes per run: the status becomes `Running` when the run starts, and the
  terminal status, the counter, the end instant and — on success — the instant of
  the last successful run are written when it finishes.
- Each write commits **only** the `SyncState` row. Nothing else may be staged when
  it commits (the US-009 security-review finding F-2: a commit writes the whole
  `DbContext`, not one row).
- **`PermittedServiceWrites` does not grow.** The use case calls
  `IReadOnlyModeGuard` first, so it is a *protected* write path, not a service
  write permitted in read-only mode. `ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath`
  passes without a new declaration, and BR-026's closed list stays as it is
  (OD-005).

### FR-007 A skipped run writes nothing

- When the guard refuses, or no usable `WorkspaceConnection` is saved: no run
  starts, **no `SyncState` write happens at all**, and no call reaches Google —
  including the token request (SC-5, SC-8).
- Exactly one log line records the skip and its reason, at `Information`, carrying
  a category and no free text — the shape `AccessCheckLog.SelfCheckSkipped` uses
  (`ReadOnly:<reason>`, `Connection:<state>`) (OD-005, I-4).
- The service then waits the interval and evaluates the schedule again, so leaving
  read-only mode resumes synchronization without a restart.

### FR-008 A failed run

- The run's status becomes `Failed`; the end instant and the counter reached so far
  are written; the instant of the last successful run is **left untouched**.
- The stored error is a **category plus a short message**, never Google's raw error
  text, never an exception's stack trace, and never a name, email or grade (SC-10,
  VR-002).
- The failure is logged at `Error` (I-7) and the service continues: the next run is
  scheduled on the same interval (OD-003).
- An exception raised inside a run never reaches the host and never stops the
  service (the US-005 rule: nothing a check does stops the service or the host).
- Deciding whether a failure was transient (`429`, `5xx`) or a permission failure
  (`403 unauthorized_client`, `access_denied`, a missing scope), and retrying it
  with backoff, is **US-017** (AD-5, BR-034, BR-043). This Story records what it is
  given.

### FR-009 Read-only mode

- Enforced in the Application layer by the guard (AD-6, SC-5), never by leaving the
  service unregistered and never by hiding UI — there is no UI in this Story.
- The refusal is proven with the Google port substituted, asserting **zero** calls
  (TC-4, TC-5).
- The three read-only reasons of BR-025 — suspended, grace period expired, never
  successfully checked — are all refusals here; the service does not distinguish
  them beyond the reason it logs.

### FR-010 Shutdown

- When the host stops, no new run starts and a run in progress is cancelled through
  the `CancellationToken` the service was given.
- An `OperationCanceledException` raised because that token was cancelled is a
  **normal stop**: it is not logged as a failure and **not written as a failed
  run** (Story AC-008).
- A `SyncState` row left at `Running` by a stopped or crashed process is therefore
  possible. It is self-healing: the next run overwrites it, readiness reports
  `Unhealthy` while the service is not running (FR-014), and a reader may present a
  `Running` row whose start is old as interrupted (I-3). Nothing in this Story
  writes a "cancelled" status, because §3 defines only three.

### FR-011 The first run of a process

- The service waits, before its first run, until the first legitimacy
  determination of this process has completed — the signal is
  `LegitimacyCheckMemory.LastOutcome` becoming non-null, which the legitimacy-check
  service sets when its first check finishes (OD-006).
- Reason: an installation that has never completed a successful check **is**
  read-only (BR-025), so a run at the instant of start would always be skipped on a
  fresh installation.
- The wait is bounded by one interval. If no outcome has appeared by then the
  service starts a run anyway and lets the guard decide, so a stalled check can
  never stall synchronization permanently (I-5).
- The waiting itself runs on the injected `TimeProvider` (FR-003).

### FR-012 Logging

- Four events, each with its own `EventId` in a block no other service uses, each
  emitted by a source-generated `LoggerMessage` method on a `partial` class (the
  `LegitimacyCheckBackgroundService` pattern):
  - run started — `Information`;
  - run completed — `Information`, carrying the counter (DC-10: "начало и
    завершение синхронизации со счётчиками");
  - run failed — `Error` (I-7);
  - run skipped — `Information`, carrying the category (FR-007).
- **Every line written by a run carries that run's identifier** (DC-10,
  `trebovaniya.md` §8), so one run reads end to end.
- SC-10 binds every line without exception: internal identifiers, categories and
  states only — never a name, email or grade, never a key, connection string or
  token, never a raw Google error object.

### FR-013 Configuration

- One **new optional** installation setting: the run interval, key
  `Sync:IntervalMinutes`, in whole minutes, alongside the existing keys read by
  `InstallationSettingsReader` (OD-002).
- Absent or blank → the default, **60 minutes (1 hour)** (I-6).
- Present and invalid — not an integer, or outside the permitted range — stops the
  start with `InstallationSettingException`, the mechanism every other setting uses
  (VR-001, DC-3).
- It becomes a field of `InstallationSettings` and reaches the service as a
  `TimeSpan`; no other class reads the configuration key.
- `docs/architecture/deployment-conventions.md` DC-3 is updated in this Story to
  list the new optional setting, so the canonical deployment document stays
  complete. The `implementation_report` traces that edit.

### FR-014 Readiness

- `GetReadinessQuery` answers `Unhealthy` when the synchronization background
  service is not running, in addition to the existing database probe (DC-11,
  `trebovaniya.md` §8 — "БД недоступна или фоновый сервис синхронизации не
  запущен").
- The signal is a process-wide marker the service sets when its `ExecuteAsync`
  begins and clears when `ExecuteAsync` returns or throws, in a `finally` — the
  in-process-memory pattern `LegitimacyCheckMemory` establishes. Readiness reads
  the marker; it never inspects the host's service collection.
- Precedence is unchanged and explicit: `Unhealthy` (database unreadable **or**
  service not running) outranks `Degraded` (read-only mode, or the last check
  unsuccessful within the grace period), which outranks `Healthy`.
- **Read-only mode stays `Degraded`.** The service runs in read-only mode and
  deliberately skips runs; reporting that as `Unhealthy` would cut off viewing and
  exporting, which DC-11 and DC-7 forbid.
- During shutdown readiness may answer `Unhealthy` once the marker is cleared. That
  is correct — the installation is stopping.

### FR-015 Persistence

- One new table for `SyncState` and **one** migration (indicative name
  `AddSyncState`), shipped in this Story (PC-2). No `EnsureCreated()`, no schema
  change outside the migration.
- The installation's migration count goes from five to six and its table count from
  five to six. `AppUserMigrationTests` asserts both today and is **expected** to
  change here — traced, as US-011 and US-012 traced their own.
- No existing table is altered. The Control Plane schema is untouched.
- `PC-10` also describes idempotent upsert on Google ids, the incremental rule and
  the age rule for a not-yet-imported course. Those belong to US-014, US-015 and
  US-018; DB_DESIGN must not pull them into `SyncState`.

### FR-016 Surface and authorization

- The Story adds **no endpoint, no page, no authorization policy and no route**.
  SC-4's closed list of anonymous endpoints is unchanged, and the US-008 endpoint
  enumeration test gains no row.
- The readiness endpoint keeps its place: the private port only, behind the
  existing port filter, reachable from the Owner's network alone (SC-9, DC-6,
  DC-11).
- No synchronization code is reachable from any web request (NFR-001, AD-5).

### FR-017 Audit

- A **scheduled** run writes no audit row. `trebovaniya.md` §5 audits the *manual*
  start of a synchronization, which is US-019; SC-11's list is not extended here.
- Consequently `AuditAction`, `AuditTargetType` and `AuditRefusalCategory` gain no
  member, and no constraint-amending migration is needed for the audit table.

### FR-018 Packages, wiring and localization

- **No NuGet package is added.** Everything needed —`BackgroundService`,
  `TimeProvider`, EF Core, Serilog — is already referenced.
- Registered in `InstallationServices.AddInstallation`: the background service via
  `AddHostedService`, the coordinator and the readiness marker as singletons, the
  use case and the `SyncState` repository as scoped, each run resolving its scope
  from `IServiceScopeFactory` (the existing background-service pattern).
- **No user-visible string and therefore no translation key** — nothing in this
  Story reaches a screen (US-024 shows the state, US-019 triggers a run). If the
  design finds a message that does reach a user, NFR-073 applies in full and both
  language files gain the key together.

## 5. Acceptance Criteria

The Story's Acceptance Criteria are carried here unchanged; each is covered by the
requirements named.

| Id | Acceptance Criterion (Story) | Covered by |
|---|---|---|
| AC-001 | The service is hosted, and readiness knows whether it runs | FR-014, FR-016, FR-018 |
| AC-002 | A run starts on a schedule, and only one runs at a time | FR-003, FR-004 |
| AC-003 | `SyncState` records the run and nothing else does | FR-001, FR-002, FR-006, FR-015 |
| AC-004 | In read-only mode nothing runs, nothing is called, nothing is written | FR-005, FR-007, FR-009 |
| AC-005 | Without a saved connection there is nothing to synchronize | FR-005, FR-007 |
| AC-006 | A failed run is recorded and does not stop the service | FR-008, FR-002 |
| AC-007 | Every line of a run is traceable and carries no personal data | FR-012, FR-017 |
| AC-008 | Shutdown is clean | FR-010 |
| AC-009 | Tests never reach Google and never sleep | FR-003, FR-009, §9 |

## 6. Validation Rules

### VR-001 The interval setting

- `Sync:IntervalMinutes` is optional. Absent, or present and blank after trimming,
  counts as absent and yields the default of FR-013 — the rule US-011 VR-004
  established for the key reference.
- Present: a whole number of minutes, parsed with the invariant culture and no
  permitted sign or separator, **1 … 1440** inclusive. Anything else — text, a
  fraction, zero, a negative number, a value above a day — stops the start with
  `InstallationSettingException`.
- The rejected value is never written to a log (SC-10).

### VR-002 `SyncState` fields

- The status is one of the three values of FR-002; no other value is storable, and
  the database enforces it with a check constraint, as `legitimacy_state` does for
  its own status.
- Exactly one row exists. The database enforces it — the singleton pattern
  `ck_legitimacy_state_singleton` establishes, not application code alone.
- The counter is zero or greater.
- The stored error has a bounded length, enforced by a check constraint, and the
  end instant is never earlier than the start instant. DB_DESIGN fixes the bound.
- The instant of the last successful run is either absent or the end instant of
  some completed run — never a value a failed run wrote.

### VR-003 The run identifier

Generated inside the process when the run starts; never taken from a request,
header, configuration value or Google response. No external input reaches it,
because this Story has no external input (VR-005).

### VR-004 The operation name of the read-only refusal

The use case declares one constant operation name, as every guarded use case does
(US-007 VR-001), and passes exactly that. It is a stable internal identifier, not
a translated string.

### VR-005 External input

This Story exposes no endpoint, no page and no form, so it accepts no request
body, query or route parameter and no uploaded file (FR-016). Its only external
inputs are the configuration value of VR-001 and the stored state it reads;
`WorkspaceConnection` was validated when US-009 saved it. Nothing from Google is
read, because the pipeline is empty (OD-001) — when US-014 adds a step, SC-10's
rule that data returned by Google is validated before it reaches business logic
applies to that step.

### VR-006 Time

Every instant is stored in UTC and read from the injected `TimeProvider`, never
from `DateTime.Now` or `DateTimeOffset.UtcNow` (PC-6, BR-053, TC-8).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | In read-only mode the installation makes **no call to Google at all**, the token request included, and the refusal is enforced in `Application`, not the UI | SC-5, AD-6, BR-026 |
| S-02 | No write other than the two `SyncState` writes of a permitted run; a skipped run writes nothing, and BR-026's closed list is not extended | SC-5, BR-026, FR-007 |
| S-03 | The service-account key is untouched by this Story: not read, not referenced, not logged. US-011 owns how it is obtained | SC-7, DC-5 |
| S-04 | No new outbound destination. The only services this Story talks to remain PostgreSQL and — through the existing legitimacy service — the Control Plane | SC-13 |
| S-05 | No log line carries a name, email, grade, key, connection string, token or raw Google error object; categories, states and internal identifiers only | SC-10, DC-10 |
| S-06 | The stored `SyncState` error carries no personal data and no raw Google text, and is bounded in length | SC-10, VR-002 |
| S-07 | No endpoint, page or policy is added; SC-4's anonymous list is unchanged | SC-4, FR-016 |
| S-08 | Readiness stays reachable only from the Owner's private network and answers with a state and nothing else | SC-9, DC-6, DC-11 |
| S-09 | A scheduled run writes no audit row, and SC-11's audited-action list is not extended; US-019 adds the manual start | SC-11, FR-017 |
| S-10 | Each `SyncState` commit stages that row only — a commit writes the whole `DbContext`, so nothing else may be pending | US-009 review F-2, FR-006 |
| S-11 | No Google SDK type and no `DbContext` crosses into `Application` or `Domain`; the background service holds no business rule | AD-3, AD-4 |
| S-12 | The rejected configuration value is never logged | SC-10, VR-001 |

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| Read-only mode | The guard throws `ReadOnlyModeException`; the use case turns it into the *skipped — read-only* outcome. Not an error, not a failed run. |
| No usable `WorkspaceConnection` | The *skipped — connection* outcome. Not an error, not a failed run. |
| An exception inside a run | The run is recorded `Failed` with a category and a short message (FR-008); logged at `Error`; the service continues on the same interval. |
| Host shutdown during a run | `OperationCanceledException` with the stopping token cancelled is a normal stop: nothing is logged as a failure, nothing is written (FR-010). |
| The database is unreachable when a run tries to write | The run fails as any other failure does; readiness independently reports `Unhealthy` because the database probe fails (FR-014). |
| The `SyncState` row is `Running` from a previous process | The next run overwrites it; readiness reports the service's real state (FR-010, I-3). |
| `Sync:IntervalMinutes` invalid | `InstallationSettingException` at startup — the installation refuses to start, and the value is not logged (VR-001). |
| The first legitimacy determination never arrives | After one interval the service runs anyway and the guard decides (FR-011, I-5). |

## 9. Non-Functional Requirements

- **NFR-001 / BR-040**: synchronization never blocks a web request. It runs in a
  `BackgroundService`; after this Story no synchronization code is reachable from a
  request at all (FR-016).
- **Quota** (`trebovaniya.md` §6): the schools share one Cloud project's quota, and
  the requirements expect a *periodic* rather than on-demand synchronization to stay
  well inside it. The interval is configurable precisely so one school can be
  slowed without a rebuild (FR-013).
- **DC-10**: logging levels and the run identifier as FR-012 fixes them; 30 days of
  rolling files, unchanged by this Story.
- **DC-11**: readiness as FR-014 fixes it. No metric and no centralized log
  collection is added — §8 rules both out of the first version.
- **Testing** (TC-1 … TC-8): tests are written before the implementation; the
  `SyncState` table is tested against real PostgreSQL through Testcontainers, never
  the InMemory provider; no test calls a live Google API; read-only mode is proven
  in the Application layer; the schedule is driven by advancing the injected clock
  and log assertions wait for the event instead of reading the file immediately.

## 10. Out of Scope

- Importing any entity: courses and rosters **US-014**, coursework and submissions
  **US-015** (OD-001).
- Retry with exponential backoff and the separation of transient from permission
  failures: **US-017** (AD-5, BR-043).
- Incremental behaviour: **US-018** (BR-042, PC-10).
- Starting a run from the UI — the endpoint, its policy and its audit row:
  **US-019**. Only the coordinator seam is built here (OD-007).
- Showing `SyncState` on a screen: **US-024**.
- Pulling Meet events: **US-031**.
- The retention purge, a separate daily background service: **US-037** (PC-11).
- Any write to Google Workspace: a Hard Stop, not a scope choice.
- New OAuth scopes, key handling or impersonation: established by **US-011** and
  reused unchanged.
- New audit members and new translation keys: none (FR-017, FR-018).

## 11. Open Decisions

All seven are recorded in `docs/decisions/US-013-open-decisions.md` (version 1) and
were **resolved by the Owner on 2026-09-27 as option 1** before activation. Their
impact on this Specification:

| Id | Subject | Impact |
|---|---|---|
| OD-001 | Host only, no entity import | FR-001, FR-004, FR-005, §10, I-1 |
| OD-002 | The interval is an optional installation setting with a default | FR-003, FR-013, VR-001, §9 |
| OD-003 | One interval after success and after failure alike | FR-003, FR-008 |
| OD-004 | One `SyncState` row, updated in place | FR-002, FR-006, VR-002, I-2, I-3 |
| OD-005 | A skipped run is logged, never written | FR-007, FR-009, FR-012, S-02 |
| OD-006 | The first run waits for the first legitimacy determination | FR-003, FR-011, I-5 |
| OD-007 | The one-run-at-a-time coordinator is built here | FR-004, FR-010, §10 |

No new Open Decision was raised. `trebovaniya.md` §7 holds no item that blocks this
Story: item 10 (the technical account's minimum roles) and item 14 (submissions of
a removed student) are untouched because this Story makes no Google call, and item
26 (documentation) is unrelated.

### Interpretations

Where the requirements leave a detail to the implementer rather than to the Owner,
it is written here so it can be corrected at `HUMAN_SPEC_APPROVAL` instead of being
discovered in code.

- **I-1 A zero-counter run is success.** OD-001 leaves the pipeline empty, so every
  run in this Story completes having processed nothing. The Specification states it
  explicitly (FR-001) so neither a test nor a reviewer reads it as a failure.
- **I-2 No row means "never synchronized".** `trebovaniya.md` §3 gives three status
  values, none of which describes an installation that has not synchronized yet.
  Rather than invent a fourth or seed a row in the migration, the row is created by
  the first run; its absence is the "never" case (FR-002). US-024 renders that.
- **I-3 A stale `Running` row is tolerated, not prevented.** A crashed process
  cannot write anything, so any design must tolerate a row left at `Running`; a
  graceful stop is therefore treated the same way rather than writing a fourth
  status §3 does not define (FR-010). Readiness, not `SyncState`, is what tells the
  Owner the service is down.
- **I-4 A skip is logged at `Information`.** DC-10 assigns `Warning` to *entering*
  read-only mode — which the legitimacy service already logs — not to each run that
  is skipped while the mode lasts. A skip is routine and would otherwise fill the
  file with warnings for a suspended school (FR-007, OD-005).
- **I-5 The wait for the first legitimacy determination is bounded by one
  interval.** OD-006 does not say what happens if that determination never arrives.
  An unbounded wait would let one stalled check stop synchronization for the life of
  the process, so after one interval the service runs and the guard decides
  (FR-011).
- **I-6 The default interval is 1 hour**, decided by the Owner at the Specification
  gate on 2026-09-27. No document fixes a number, so the choice is between
  protecting the shared quota of §6 and how stale a Dean's data may be — and until
  US-019 adds the button there is no way to ask for a run by hand. An hour keeps the
  data of the same school day current; its cost is nothing while the pipeline is
  empty (OD-001), and by the time US-014 and US-015 fill it the Owner will have
  measured a real run on a real domain and can raise the value **by configuration,
  without a rebuild** — which is what the setting exists for (FR-013). Six hours,
  matching the legitimacy check's cadence (BR-024), was the alternative considered
  and rejected as too stale for a school day.
- **I-7 A failed run is logged at `Error`.** DC-10 reserves `Warning` for *retried*
  transient Google failures. This Story does not classify or retry anything
  (US-017), so every failure it can see is, as far as it knows, unhandled — `Error`
  is the honest level. When US-017 adds the classification it may lower the retried
  cases to `Warning` (FR-008, FR-012).
- **I-8 The counter is one number.** `trebovaniya.md` §3 says "счётчик", singular.
  A set of per-entity counters would be a schema shaped by data this Story does not
  import; US-014 and US-015 decide whether they need more, and a migration is how
  they would add it (FR-001).

## 12. Traceability

| Acceptance Criterion | Functional requirements | Validation / security |
|---|---|---|
| AC-001 The service is hosted, readiness knows | FR-014, FR-016, FR-018 | S-07, S-08 |
| AC-002 Schedule, one run at a time | FR-003, FR-004 | VR-001, VR-006 |
| AC-003 `SyncState` records the run | FR-001, FR-002, FR-006, FR-015 | VR-002, VR-003, S-10 |
| AC-004 Read-only: nothing runs, called, written | FR-005, FR-007, FR-009 | VR-004, S-01, S-02 |
| AC-005 No connection, nothing to synchronize | FR-005, FR-007 | S-02 |
| AC-006 A failed run is recorded, service survives | FR-002, FR-008 | VR-002, S-06 |
| AC-007 Traceable lines, no personal data | FR-012, FR-017 | S-05, S-09, S-12 |
| AC-008 Clean shutdown | FR-010 | — |
| AC-009 Tests never reach Google, never sleep | FR-003, FR-009 | S-01, §9 |

Requirement sources: `trebovaniya.md` §2 (read-only mode and the closed list of
service writes), §3 (`SyncState`), §4 Epic 1 (the periodic run and what is stored),
§5 (the background process, logging, audit), §6 (the shared quota), §8 (readiness
and the run identifier); BR-025, BR-026, BR-040, BR-043, BR-044; NFR-001; AD-3,
AD-4, AD-5, AD-6, AD-10; PC-2, PC-6, PC-10; SC-4, SC-5, SC-7, SC-9, SC-10, SC-11,
SC-13; DC-3, DC-6, DC-10, DC-11; TC-1 … TC-8.
