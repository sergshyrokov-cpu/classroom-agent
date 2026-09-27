---
artifact_type: test_strategy
story: US-013
version: 1
status: DRAFT
created_at: 2026-09-27T12:29:57Z
updated_at: 2026-09-27T12:29:57Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-013-background-sync-service.md
    version: null
  - path: docs/specifications/US-013-spec.md
    version: 1
  - path: docs/designs/api/US-013-api-design.md
    version: 1
  - path: docs/designs/database/US-013-db-design.md
    version: 1
  - path: docs/designs/database/US-013-entity-model.md
    version: 1
  - path: docs/decisions/US-013-open-decisions.md
    version: 2
supersedes: null
---

# US-013 Test Strategy — Background synchronization service

## 1. Scope

What the tests must prove: a run happens on a schedule, one at a time, records
itself in `SyncState` and nowhere else, refuses in read-only mode and without a
usable connection **without writing anything**, survives its own failures, logs
four events carrying the run identifier and no personal data, stops cleanly with
the host, and makes readiness truthful about whether the service runs.

What the tests deliberately do **not** prove: that any data is imported. The
pipeline is empty in this Story (OD-001), so **a completed run with a zero counter
is the expected result** (spec I-1) and no test may read it as a defect.

There is no endpoint, page, DTO, policy or translation key in this Story
(api-design §1, spec FR-016, FR-018), so there are no contract tests, no
authorization tests and no translation tests. That absence is itself asserted
where an existing guard already does it (§7).

## 2. Test levels

| Level | Used for | Why |
|---|---|---|
| Unit (Application, ports in memory) | the run's evaluation order, the refusals, what is written and what is not, the entity's invariants | TC-1: the use case is the unit, ports are substituted. These are the decisive tests: read-only mode is proved here, not through the UI (TC-5) |
| Integration (real host + real PostgreSQL) | the schedule, the coordinator, shutdown, the log lines, readiness, the table and its constraints | TC-2: Testcontainers, never the InMemory provider — the six check constraints exist only in the database |
| Configuration | `Sync:IntervalMinutes` and its boundaries | the reader validates at startup and stops the host (spec VR-001) |
| Security | nothing written in read-only mode, no audit row, no personal data in the log, readiness on the private port only | TC-5, SC-10, SC-11, SC-9 |

## 3. Fixtures

- **`SyncWorld`** — the run use case with every port in memory: the `sync_state`
  row, the unit of work counting commits and the status at each one, the read-only
  guard recording the operations it was asked, the stored connection and the
  legitimacy state. Modelled on `DeanAccountWorld` (US-012).
- **`SyncTestData`** — the configuration key, the default and the bounds of the
  interval, the four log event names, the table and migration names, the six
  constraint names and the three status codes. No test spells any of these as a
  literal.
- **`SyncHostExtensions`** — a host seeded into a legitimacy state and a connection
  state and only then started, the `sync_state` rows as the database holds them, a
  wait for a finished run, the readiness state, and the coordinator and the
  readiness marker resolved from the running host.
- **`ManualTimeProvider`** (existing) — the schedule is driven by advancing the
  injected clock; `AdvanceWhenDueAsync` and `WaitForTimerAtAsync` remove the race
  between a timer being created and time moving.
- **`ReadOnlyModeHost`** (existing) — the three BR-025 causes as theory data, so
  each refusal is proved for every cause rather than for a representative one.
- **`HostLogs` / `LogEvent`** (existing) — log assertions wait for the event and
  then read, never read immediately after a stub returns.

## 4. Positive scenarios

- A first run creates the row and completes with the counter at zero.
- A completed run writes the instant of the last success.
- A second run updates the same row and a third does not add one.
- The status is committed as `Running` before the outcome — two commits per run.
- The interval is one hour by default, the configured value when set, counted from
  the completion of the previous run.
- The second run happens when the interval elapses.
- The first run waits for the first legitimacy determination and then happens.
- A request made while idle is accepted and startable (the US-019 seam).
- Readiness is not `Unhealthy` while the service runs.

## 5. Negative scenarios

- Read-only mode, for each of the three BR-025 causes: skipped, **nothing written**,
  no commit, no transaction, the guard asked first and nothing else read.
- No connection saved, and a connection for another domain: skipped, nothing
  written.
- A failed run: the status and message are recorded and the instant of the last
  success is **not** moved.
- A terminal row cannot be completed or failed again.
- The service not running: readiness is `Unhealthy` with 503, and that outranks
  read-only mode.
- Readiness is not reachable on the public port.
- The database rejects a second row, an unknown status, a negative counter, an end
  before the start, and each of the three disagreements
  `ck_sync_state_terminal_fields` forbids.

## 6. Boundary scenarios

- `Sync:IntervalMinutes` at `1` and `1440` — accepted; `0`, `-5`, `1441`, `abc`,
  `1.5` — the host refuses to start.
- Absent and blank-after-trimming both mean the default.
- An error message longer than 512 characters is **truncated**, not rejected — a
  failing run must not fail again at the commit because its diagnosis was verbose.
- A counter of zero is success; a negative counter is impossible.
- The interval boundary itself: one second before it, no run; at it, a run.

## 7. Security scenarios

| Requirement | Test |
|---|---|
| S-01, S-02 (BR-026) | read-only mode writes nothing at all — asserted on the in-memory row, the commit count and the `sync_state` table |
| S-05 (SC-10) | no log line carries the technical account, the school's domain or the connection string |
| S-06 | the stored error is bounded and carries a category, never a payload |
| S-09 (SC-11) | a scheduled run writes **no** audit row |
| S-08 (SC-9) | readiness answers the state only and is not reachable on the public port |
| BR-026 closed list | `PermittedServiceWriteTests.TheRegistry_DeclaresOnlyWritesOnTheClosedList` and `ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath` are **existing guards that must stay green**: the run use case calls the guard first, so the registry does not grow (spec FR-006). Their staying green is the assertion |

## 8. Persistence scenarios

Every one against real PostgreSQL through Testcontainers (TC-2): the columns with
their nullability and lengths, the primary key, the singleton unique index, the
six check constraints, the absence of any foreign key and of any other index, and
the migration count going from five to six with `_AddSyncState` last.

## 9. Excluded scenarios, with justification

| Excluded | Why |
|---|---|
| Contract tests | no endpoint exists (api-design §1) |
| Authorization tests | no endpoint and no policy; a run has no principal |
| Translation tests | no user-visible string (spec FR-018) |
| Audit-row content tests | no audit row is written; only its absence is asserted |
| Google port behaviour | the pipeline is empty; no Google call happens (OD-001) |
| Retry and backoff | US-017 |
| Incremental behaviour, upsert | US-014, US-015, US-018 |
| A run started over HTTP | US-019; only the coordinator seam is exercised |
| Showing `SyncState` on a screen | US-024 |

## 10. Known limitations

- **A stale `Running` row is not provable by a test that crashes a process.** Spec
  I-3 tolerates it; what is asserted instead is that a graceful stop records no
  failure and that the next run overwrites the row.
- **The "service not running" state is simulated** by marking the readiness marker
  stopped, not by killing the background service — a test cannot make a
  `BackgroundService` die without also tearing down the host it asserts against.
- Timing is proved on the injected clock, so the tests prove the *schedule*, not
  wall-clock punctuality.
- The counter is asserted as zero everywhere, because nothing increments it yet;
  US-014 is where a non-zero counter becomes assertable.

## 11. Open Decisions affecting testing

- **OD-008** (raised here, resolved by the Owner on 2026-09-27 as option 1): the
  compile-only skeleton, without which the test project cannot compile. Ten items,
  listed in the open-decisions artifact. IMPLEMENTATION owns them from now on and
  may reshape them together with the tests.
- OD-001 (host only) shapes every assertion about the counter; OD-003 the interval
  after a failure; OD-004 the single row; OD-005 that a skip is logged and never
  written; OD-006 the wait before the first run; OD-007 that the coordinator exists
  here.
