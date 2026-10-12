---
artifact_type: api_design
story: US-024
version: 1
status: DRAFT
created_at: 2026-10-12T12:53:45Z
updated_at: 2026-10-12T12:53:45Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-024-spec.md
    version: 1
  - path: docs/decisions/US-024-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 90
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
  - path: docs/designs/api/US-019-openapi.yaml
    version: 1
supersedes: null
---

# US-024 API Design — Database statistics and last synchronization

Companion to `docs/designs/api/US-024-openapi.yaml`.

Delegated: nothing at this stage. The facts used were gathered read-only at
SPECIFICATION (`quick-look`: `SignInRoutes`, `InstallationPolicies`,
`SynchronizationRequestController`, `RequestSynchronizationUseCase`, the home
page) and re-read directly here: `US-019-openapi.yaml`, `US-019-api-design.md`,
`US-022-openapi.yaml` (header style), `GetLastSynchronizationQuery`,
`InstallationExceptionHandler`, `Views/Home/Index.cshtml`.

## 1. Scope of the contract

**Two new** operations on the installation's public port, and one existing page
changed additively:

| Operation | Purpose | Policy | Calls Google | Writes |
|---|---|---|---|---|
| `GET /database` (new) | the Database page | `ViewDatabaseStatistics` (new) | no | nothing |
| `POST /database/synchronization-requests` (new) | request a run, return to the page | `StartSynchronization` (US-019) | no | one audit row (US-019); one in-process request |
| `GET /` (US-008) | gains the "Database" section | `ViewLegitimacyStatus` (unchanged) | no | nothing |

`POST /synchronization/requests` (US-019) is untouched (OD-011 a). No `/api/v1`
path, no Control Plane change, no private-port change; `ContractVersion.Current`
stays **1**.

## 2. Decisions this stage made

### 2.1 Paths

- `/database` — a top-level path of its own. It is neither Google Workspace
  data (`/workspace/*`), nor reports (`/reports/*`), nor the Admin's settings
  (`/settings/*`, Admin-only by convention since US-019 §2.1) — both roles open
  it. New route constant `SignInRoutes.Database`.
- `/database/synchronization-requests` — the page's own action, a noun for what
  is created (as US-019 §2.1), nested under the page it belongs to. New route
  constant `SignInRoutes.DatabaseSynchronizationRequests`.

### 2.2 No query parameters

The `GET` binds nothing. Status, paging or filters do not exist on this page, and
the one-time message travels in TempData, so the address never carries data
(spec S-09). The language switcher's return path for this page is the fixed
`/database`, never the raw query (US-039 INFO-1; US-022 security INFO-2).

### 2.3 The second endpoint (OD-011 a)

- Calls `RequestSynchronizationUseCase.ExecuteAsync` exactly as the US-019
  controller does — actor id and role from the session, the trace identifier —
  so the guard, the connection check, the enqueue and the audit row are
  identical by construction (AC-008). No new use case, audit action, refusal
  category or message code.
- Post-Redirect-Get on acceptance, with the US-019 TempData key
  `SynchronizationMessage` and the US-019 `SynchronizationMessageKey` codes.
  Reusing the key means a message is shown by whichever page the user lands on;
  this endpoint always lands on `/database`.
- The return page is a constant of the endpoint, not a value from the request
  (spec VR-002, S-07): the endpoint cannot become an open redirect.
- Read-only refusal is rethrown to the host-wide handler (`409`, error page with
  the reason), as US-019 does; the connection refusal re-renders the Database
  page with `409` and the role's message, as US-019 re-renders the role's page.
- Logging mirrors US-019 (`SynchronizationRequestLog.Requested` / `.Refused`).
  Whether the US-019 controller and this one share a helper is an
  implementation choice; the observable behaviour is fixed here.

### 2.4 The query

`GetDatabaseStatisticsQuery` in `Application/UseCases` returns
`DatabaseStatisticsPageModel`. Input: the session's role (for
`failureGuidance`). It reads through read ports only (DB_DESIGN names them,
spec FR-011), converts the three times to the school's zone with
`SchoolTimeZone` as `GetLastSynchronizationQuery` does, and calls no
`IReadOnlyModeGuard`. The host adds the one-time message key from TempData; the
`409` re-render passes the refusal key instead.

### 2.5 The new policy

`ViewDatabaseStatistics` — its own policy for its own §2 matrix row (spec FR-010),
granted to Admin and Dean, declared in `InstallationPolicies` and registered in
`InstallationSecurityServices` beside the others. `StartSynchronization` is reused
for the press because its matrix row is "Запуск синхронизации", whatever page
carries the button.

### 2.6 What the page carries about a failed run

`FailureGuidance` is a closed enum chosen in `Application` from the role:
`AskAdmin` (Dean: a translated hint, no link), `ConnectionPageLink` (Admin: a
link to `/settings/workspace-connection`), `None` otherwise. The DTO has no
field that could carry `LastError` or `FailedStep`, so the diagnosis cannot leak
by a view mistake (spec S-08, AC-013).

### 2.7 Times

`date-time` values without offset — wall-clock in the school's zone, converted in
`Application` (AD-3). The view formats them as the course list and the
connection page do: the culture's short date pattern plus `HH:mm`.

### 2.8 Counter types

`int32`, non-negative. A single school's row counts stay far below `int32.MaxValue`
over the retention period (NFR-003); DB_DESIGN may use `long` internally and
narrow, or `CountAsync` directly.

## 3. Operation notes

### `GET /database`

- Order: authentication (`302 /sign-in`) → restricted session (`302
  /sign-in/change-password`) → authorization (`ViewDatabaseStatistics`) → query →
  `200`.
- Any query string is ignored and echoed nowhere (AC-019).
- Read-only mode: `200` with every counter and the block (AC-009); no write, no
  Google port (proven in `Application`, TC-5).
- Before the first run: counters `0`, state `NotYet`, three times null (AC-006).
- The button is always rendered (read-only included; enforcement in
  `Application`, AD-6, OD-007).
- Logs one `Information` line with the actor id (spec FR-013).

### `POST /database/synchronization-requests`

- Order, as US-019 §3: authentication → restricted session → authorization
  (`StartSynchronization`) → antiforgery (`400`) → read-only guard (`409`, error
  page, audit `Refused`/`ReadOnlyMode`) → connection state (`409`, Database page,
  audit `Refused`/`ConnectionNotUsable`) → enqueue → audit `Succeeded` → `302
  /database` with the message.
- No field is bound from the body.
- Returns at once (NFR-001).

### `GET /`

- Markup only: `<nav id="database-section">` with a heading and one link to
  `/database`, between `report-templates-section` and `admin-settings`, for both
  roles, in read-only mode too (AC-001). View model unchanged.

## 4. Authentication and authorization model

| Operation | Anonymous | Dean (temporary password) | Dean | Admin | Antiforgery |
|---|---|---|---|---|---|
| `GET /database` | `302 /sign-in` | `302 /sign-in/change-password` | `200` | `200` | — |
| `POST /database/synchronization-requests` | `302 /sign-in` | `302 /sign-in/change-password` | `302 /database` / `409` | `302 /database` / `409` | required |
| `GET /` | unchanged | unchanged | unchanged | unchanged | — |

There is no forbidden *signed-in* role in v1; the forbidden-role tests are the
anonymous visitor and the restricted session (TC-5, spec S-02). The SC-4
anonymous list and the antiforgery exemption list are unchanged; the endpoint
enumeration test classifies both new operations as protected.

## 5. Error model

All responses are HTML; there is no API-6 JSON body in this Story.

| Status | Operation | When | Body |
|---|---|---|---|
| `302` | both | not signed in; restricted session; (POST) accepted | redirect |
| `400` | POST | missing/invalid antiforgery token | translated error page |
| `409` | POST | read-only mode | translated error page naming the BR-025 reason |
| `409` | POST | no usable connection | the Database page with `ConnectionNotUsableAdmin`/`Dean` |
| `500` | both | unexpected failure (incl. an unknown stored sync status) | the single error page, no detail |

No `404`, no `400` for the `GET` (nothing is validated — nothing is bound), no
`403` for a signed-in user.

## 6. Acceptance Criterion → operation map

| AC | Operation(s) | Evidence in the contract |
|---|---|---|
| AC-001 | `GET /` | `database-section` placement |
| AC-002 | `GET /database` | `CourseCounts` |
| AC-003 | `GET /database` | `teachers`, `students` |
| AC-004 | `GET /database` | `courseWorkItems`, `submissions`, `meetMeetings`, `meetCodesAwaitingDecision` |
| AC-005 | `GET /database` | `SynchronizationBlock`; no processed counter in the DTO |
| AC-006 | `GET /database` | `NotYet`, null times, zero counters |
| AC-007 | `GET /database` | `Failed` + `FailureGuidance` by role |
| AC-008 | `POST /database/synchronization-requests` | `302 /database`, `x-reuses-use-case`, `x-writes` |
| AC-009 | both | `GET` `200`, `x-writes: nothing`; `POST` `409` case 1 |
| AC-010 | both | policies; `302 /sign-in`; restricted session; `400` |
| AC-011 | `GET /database`, `GET /` | every label by translation key (spec FR-012) |
| AC-012 | both | `x-calls-google: false` |
| AC-013 | `GET /database` | the DTO has no diagnosis or step field |
| AC-014 | `GET /database` | times converted in `Application` (§2.7) |
| AC-015 | `GET /database` | distinct-participant counters |
| AC-016 | `POST /database/synchronization-requests` | `409` case 2 re-renders `/database` |
| AC-017 | `GET /database` | (DB_DESIGN: fixed number of counting queries) |
| AC-018 | `GET /database` | `meetCodesAwaitingDecision` = Unassigned rule |
| AC-019 | `GET /database` | no parameter bound; nothing echoed |

## 7. Compatibility

- Additive only: two new paths, one new policy, two new route constants, markup
  added to the home page.
- `POST /synchronization/requests` and its return pages, `GET
  /settings/workspace-connection` and the Dean's home-page button are unchanged.
- The US-008 endpoint enumeration test must classify both new endpoints as
  protected and not anonymous.

## 8. Open questions for later stages

None blocking. Notes:

- **DB_DESIGN** names the read port(s) for the counters (spec FR-011), decides
  whether `IMeetCodesReadSource.GetCountsAsync` is reused for
  `meetCodesAwaitingDecision` or the rule is repeated in one statement with the
  other counts, checks indexes, and bounds the number of round trips (AC-017).
  No schema change is expected.
- **TEST_WRITING** asserts: the US-019 evaluation order on the new `POST`,
  including a read-only *and* unconfigured school answering with the read-only
  reason and zero port calls; that a submitted `returnUrl` is ignored and the
  `Location` is `/database`; that one audit row is written per press with the
  US-019 action; that `GET /database?x=<marker>` shows no `<marker>`; and that
  `POST /synchronization/requests` still returns to the role's page.
