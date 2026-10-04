---
artifact_type: test_strategy
story: US-019
version: 1
status: DRAFT
created_at: 2026-10-04T07:00:00Z
updated_at: 2026-10-04T07:00:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-019-trigger-sync-from-ui.md
    version: null
  - path: docs/specifications/US-019-spec.md
    version: 1
  - path: docs/designs/api/US-019-openapi.yaml
    version: 1
  - path: docs/designs/api/US-019-api-design.md
    version: 1
  - path: docs/designs/database/US-019-db-design.md
    version: 1
  - path: docs/designs/database/US-019-entity-model.md
    version: 1
  - path: docs/decisions/US-019-open-decisions.md
    version: 1
supersedes: null
---

# US-019 Test Strategy — Trigger a synchronization from the UI

## 1. Scope

The manual "Synchronize" request: one form `POST`, its use case, the
synchronization-request port and its adapter over `SyncRunCoordinator`, the
audit row, the buttons on two existing pages, the translations and the log
lines. The run the request starts is **not** retested: US-013 … US-017 own it,
and the coordinator's one-at-a-time behaviour is covered by
`SyncRunCoordinationTests` (US-013, US-037).

Test-by-test mapping: `docs/tests/US-019-ac-test-matrix.md`.

## 2. Levels

| Level | What | Classes |
|---|---|---|
| Integration over HTTP (`InstallationTestHost`, real PostgreSQL via Testcontainers, TC-2) | the endpoint, the use case with the port substituted, audit rows, pages, logs | `SynchronizationRequestTests`, `SynchronizationRequestRefusalTests`, `SynchronizationRequestAuditTests`, `SynchronizationRequestAuthorizationTests`, `SynchronizeButtonTests`, `SynchronizationRequestTranslationTests`, `SynchronizationRequestLoggingTests` |
| Integration (database) | the new action value, the shape constraint, the factories' rows stored | `SynchronizationRequestAuditSchemaTests` |
| Unit | the adapter over a real `SyncRunCoordinator`; the factories' guards | `CoordinatorSynchronizationRequestsTests`, part of `SynchronizationRequestAuditSchemaTests` |

Read-only mode is proven through the `Application` use case with the port
substituted (`FakeSynchronizationRequests` records zero calls), for all three
BR-025 causes (TC-5) — the pattern of US-011.

## 3. Scenarios

- **Positive:** Admin press from the connection page; Dean press from the home
  page; both get `302` to their own page and a one-time message; the port is
  called once; one `succeeded` audit row with the right actor and role.
- **During other work:** the port answers `AfterCurrentWork` → the "after the
  current synchronization" message. Adapter: idle / during a run / during a
  purge / several requests during a run → exactly one further run.
- **Negative — state:** read-only (3 causes × 2 roles) → `409`, reason named,
  nothing enqueued, refusal audited; read-only and unconfigured → read-only
  reason first; no connection / connection for another domain → `409` with the
  role's message, nothing enqueued, refusal audited (OD-009 a).
- **Security (TC-5):** anonymous → `/sign-in`; Dean with a temporary password →
  forced change; missing antiforgery token → `400`; a `GET` requests nothing; a
  submitted `returnUrl` is ignored; both granted roles allowed. The global
  endpoint-enumeration and antiforgery tests of US-008 pick the new endpoint up
  without change.
- **Pages:** the button on the Admin connection page and the Dean home page, in
  read-only mode too; no button on the Admin home page (I-6); the Dean sees
  nothing about any run (S-08).
- **Persistence:** both factory rows stored; the shape constraint rejects a row
  with a target and a row with a foreign refusal category; the refused factory
  rejects other categories; the role is recorded as given.
- **Localization (AC-007):** every new key in uk and en, differing; an English
  user sees the English message.
- **Logging (FR-010):** one `Information` line per accepted press without the
  email; one `Warning` line per refused press.

No validation boundaries exist: the request carries no field (VR-001); the
"field" scenario is the ignored `returnUrl`. No date or time-zone logic (TC-8
not applicable).

## 4. Fixtures

- `FakeSynchronizationRequests` — the port, recording calls and answering a
  scripted `SynchronizationRequestTiming`.
- `SynchronizationRequestHostExtensions` — starts a host in a legitimacy state
  with a seeded connection and the fake port, signs in an Admin, a Dean, a Dean
  with a temporary password or no one; presses the button; reads this Story's
  audit rows.
- `SynchronizationRequestTestData` — the path, return pages, text keys, audit
  codes, log event names and markup ids fixed for this Story.
- Reused: `ReadOnlyModeHost`, `SeededConnection`, `AccessCheckHostExtensions.SeedConnectionAsync`,
  `DeanAccountHostExtensions`, `InstallationTestHost`.

## 5. Excluded scenarios

- A real synchronization run after the press — US-013 … US-017 and the existing
  coordinator tests; here the port is substituted (TC-4).
- The audit-commit failure after an enqueue (db-design §5) — accepted behaviour,
  a `500` through the existing handler; no fault-injection seam exists for the
  unit of work and none is added.
- Rate limiting — none (OD-004).
- The `Down` migration — not tested in any Story.

## 6. Known limitations

- Fixed names (text keys, log event names, markup ids, TempData) are fixed by
  this stage in `SynchronizationRequestTestData`, as US-017 fixed its
  `LastSync.*` keys; IMPLEMENTATION uses them.
- A red test whose expectation already holds (the Admin home page has no
  button; the restricted Dean session already redirects) is explained in the
  test-generation report rather than counted as evidence of the new behaviour.

## 7. Open Decisions affecting testing

OD-010 (compile-only skeleton) raised here and resolved by the Owner as (a).
No other.
