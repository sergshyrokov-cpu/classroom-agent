---
artifact_type: specification
story: US-005
version: 1
status: APPROVED
created_at: 2026-09-17T13:23:07Z
updated_at: 2026-09-17T13:29:44Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-005-installation-legitimacy-check.md
    version: null
  - path: trebovaniya.md
    version: 73
  - path: docs/decisions/US-005-open-decisions.md
    version: 1
supersedes: null
---

# US-005 Specification — Installation legitimacy check and grace period

## 1. Overview

The first Story on the school's side, and the first that uses the service channel
between the two planes:

- the installation skeleton — `ClassroomAgent.Web` host, `Domain`, `Application`,
  `Infrastructure`, `Contracts`, the installation database and its first migration,
  and the configuration this Story needs;
- the legitimacy check contract in `ClassroomAgent.Contracts`;
- in the Control Plane: the check endpoint, the compatibility decision,
  `InstanceLicenseCheck`, and the last check on the school's detail page;
- in the installation: the background check on its schedule, `LegitimacyState`,
  the read-only determination with its reason, liveness and readiness on the
  private port;
- log lines for the check on both sides.

Sources: `trebovaniya.md` v73 §2 (read-only mode and its closed list of service
writes), §3 (`InstanceLicenseCheck`, `LegitimacyState`), §4 (Epic 8), §5
(mandatory installation settings, audit list, v69, v73), §8 (observability,
version compatibility, antiforgery exemptions), §9 ("Доступ Владельца к учебным
данным", "Сетевая изоляция", parameters table, "Проверка легитимности", v73);
`architecture.md` AD-1 … AD-6, AD-8, AD-9; `package-map.md`;
`security-conventions.md` SC-2, SC-4, SC-9, SC-10, SC-12, SC-13;
`deployment-conventions.md` DC-3, DC-4, DC-6, DC-7, DC-10, DC-11, DC-12;
`persistence-conventions.md` PC-1, PC-2, PC-5, PC-6; `testing-conventions.md`
TC-2, TC-5, TC-8; BR-024, BR-025, BR-026, BR-027, BR-081; NFR-013 … NFR-017,
NFR-061, NFR-073. The US-001 Control Plane host baseline and the US-002 detail page
are reused, not re-specified.

**Open Decisions:** OD-001 (NuGet packages for the installation projects) and
OD-002 (compile-only skeleton at TEST_WRITING) — process decisions of a greenfield
project, section 11. Behaviour not literally fixed by the Story or
`trebovaniya.md` is stated as interpretations I-1 … I-14.

## 2. Business Goal

The legitimacy check is how the Owner's decisions reach a school without anyone
going to its server (`trebovaniya.md` §9): it carries the `Installation` status —
the lever that decides whether the school works at all — plus the domain and
client ID later Stories check the connection against (US-009, US-010). It is the
guarantee; the push (US-006) only makes it faster (BR-024, NFR-014). The grace
period keeps a short Control Plane outage from stopping a school (NFR-013), and a
school without the Owner's record never works (BR-081).

## 3. Business Flow

### 3.1 A new school

1. The Owner registers the `Installation` (US-002), copies its identifier into the
   installation's configuration with the Control Plane address, and starts the
   installation (FR-001).
2. Until its first successful check the installation is in read-only mode, reason
   "legitimacy not yet confirmed" (FR-008).
3. The first check runs right after startup (FR-003). The Control Plane knows the
   identifier, answers "active", `supported`, domain and client ID, and records the
   call (FR-004, FR-005, FR-006).
4. The installation stores the answer in `LegitimacyState` and leaves read-only
   mode (FR-007, FR-008, FR-010). The school's detail page in the Control Plane now
   shows the last check (FR-011).

### 3.2 Normal operation

The installation checks again 6 hours after each successful check (FR-003).

### 3.3 The Control Plane is unreachable

1. A check fails; the last successful time stays; the next attempt comes 15
   minutes later, and so on until one succeeds (FR-003, FR-007).
2. Readiness reports `Degraded` while the grace period runs (FR-013).
3. If more than 7 days pass since the last success, the installation enters
   read-only mode, reason "grace period expired" (FR-008, FR-010).
4. The first successful check ends the mode.

### 3.4 Suspension

1. The Owner suspends the `Installation` (US-004).
2. At its next check the installation receives status "suspended" — a successful
   check — and enters read-only mode, reason "suspended by the Owner" (FR-007,
   FR-008). Resuming works the same way in reverse.

### 3.5 An outdated installation

1. The Control Plane answers `upgrade_required` (FR-005).
2. The check counts as unsuccessful; the answer's status, compatibility, domain and
   client ID are recorded, the last successful time is not; after 7 days the school
   is in read-only mode (FR-007, FR-008).

## 4. Functional Requirements

### FR-001 Installation configuration

The installation reads its settings from configuration (DC-3, AD-10). This Story
requires *(interpretations I-1, I-2)*:

| Setting | Rule | Source |
|---|---|---|
| Installation id | a UUID | `trebovaniya.md` §5 v69 |
| Control Plane address | an absolute `https://` URI, no user info, no query or fragment | `trebovaniya.md` §5 v73, DC-3, DC-6 |
| Private port | an integer 1–65535, different from every public endpoint port | DC-6 |
| Database connection string | non-empty | PC-1, DC-3 |

- If any is missing or invalid, the installation does not start: startup fails
  before the host accepts requests, and the log states **which** setting is wrong
  and why (missing / invalid format), never its value (AC-001, SC-10).
- With all valid, the installation starts.
- The school time zone, retention period N and default UI language are not read
  by this Story (I-1).
- Nothing in the database holds these settings.

### FR-002 Wire contract

`ClassroomAgent.Contracts` gains the legitimacy check request and response
(`package-map.md`; exact shape and transport details: `openapi-designer`):

- **Request:** installation id; installation application version; contract
  version. Nothing else.
- **Response for a known installation:** `Installation` status (`active` /
  `suspended`); compatibility state (`supported` / `upgrade_recommended` /
  `upgrade_required`); domain; client ID. Nothing else.
- **Unknown installation:** an answer that says so and carries no status,
  compatibility, domain or client ID *(interpretation I-9)*.
- The contract has no teaching-data type and references no other project (SC-12,
  SC-13). It evolves additively; both sides ignore unknown fields (DC-12).
- The contract's own version is a positive integer declared in `Contracts`; this
  Story introduces version `1` *(interpretation I-7)*.

### FR-003 Check schedule (installation)

A background service in `ClassroomAgent.Web` (`LegitimacyCheckBackgroundService`,
AD-5) runs the check use case in `Application`:

- the first check starts right after the host has started, not after a delay;
- the next check is due **6 hours** after a successful check completed, or **15
  minutes** after an unsuccessful check completed — fixed intervals, no growth
  *(interpretation I-5)*;
- at most one check runs at a time;
- nothing a check does — an exception included — stops the background service or
  the host; a failure is an unsuccessful check (FR-007) and the schedule goes on;
- time comes from an injectable clock, and the waiting is driven by it, so tests
  advance time without waiting (Story Notes);
- on host shutdown the service stops without starting a new check.

### FR-004 Check endpoint (Control Plane)

- One endpoint in `ControlPlane.Controllers`, `POST` only, receiving FR-002's
  request and returning FR-002's response; rules in `ControlPlane.Services` (AD-3).
- Anonymous and exempt from antiforgery — the "Legitimacy check" entries of both
  SC-4 closed lists; no other endpoint becomes anonymous or exempt.
- Other HTTP methods on its path do not reach it (`405` or `404`, API design).
- Steps, each only if the previous passed:
  1. **Validation** of the body (VR-002). Invalid, missing or malformed → `400`;
     nothing recorded; the rejected body is not logged (SC-10).
  2. **Lookup** of the `Installation` by id. None → the FR-002 "unknown" answer
     (not `500`); nothing recorded; logged at `Warning` with the received id
     *(interpretation I-9)*.
  3. **Compatibility** per FR-005.
  4. **Record** per FR-006.
  5. **Answer** with the stored status, compatibility, domain and client ID.
- A suspended `Installation` is answered the same way, with status `suspended`,
  and its compatibility is computed too *(interpretation I-11)*.
- No audit row (`trebovaniya.md` §5, §9 v73).

### FR-005 Compatibility decision (Control Plane)

Control Plane configuration may set *(interpretation I-8)*:

- **minimum supported installation version** — optional, `MAJOR.MINOR.PATCH`;
- **recommended installation version** — optional, `MAJOR.MINOR.PATCH`.

For a known installation, in this order:

1. contract version not in the set the Control Plane supports → `upgrade_required`
   (the supported set is declared in code; this Story: `{1}`, I-7);
2. minimum set and application version below it → `upgrade_required`;
3. recommended set and application version below it → `upgrade_recommended`;
4. otherwise → `supported`.

- Versions compare numerically by major, then minor, then patch (I-7).
- A setting that is set but not a valid version stops the Control Plane at startup,
  naming the setting, not its value (I-8).

### FR-006 InstanceLicenseCheck (Control Plane)

- One record per `Installation` at most, holding the **last** check: the UTC time
  the Control Plane answered, the application version and contract version
  reported, the status answered and the compatibility state answered
  (`trebovaniya.md` §3, §9 v73).
- Every call of a known installation replaces it — whatever the compatibility
  state, `upgrade_required` included *(interpretation I-10)*.
- The check changes no other Control Plane data. Concurrent calls for one installation leave exactly one
  record with one of the calls' values and never answer `500`
  *(interpretation I-14)*.
- No record for an unknown id or an invalid body.
- Not audited; never shown to an installation.
- Kept while the `Installation` exists (there is no deletion of `Installation` in
  v1). Exact schema: `db-designer`.

### FR-007 Check use case (installation)

The use case in `Application` calls the Control Plane through `IControlPlaneClient`
(`Application/Ports`, implemented in `Infrastructure/ControlPlane`, AD-4) with the
configured installation id, the installation's application version (FR-001, I-7)
and the contract version.

**Successful** — all of: an answer within **30 seconds** *(interpretation I-6)*;
the answer parses; the installation is known; compatibility is not
`upgrade_required`. Then `LegitimacyState` becomes: last successful check = the
completion time of this check; status, compatibility state, domain, client ID =
the answer's (AC-007). An answer with status `suspended` is successful.
`upgrade_recommended` is logged at `Warning` (DC-12).

**Unsuccessful** — each with its category (AC-008):

| Category | Condition | `LegitimacyState` |
|---|---|---|
| unreachable | connection or TLS failure | unchanged |
| timeout | no complete answer within 30 seconds | unchanged |
| error answer | any answer other than the known or unknown outcome | unchanged |
| unparseable answer | the answer does not parse into the contract, or a required field is missing or out of its allowed values | unchanged |
| unknown installation | the Control Plane answers "unknown" | unchanged |
| upgrade required | compatibility is `upgrade_required` | status, compatibility state, domain, client ID from the answer; **last successful check unchanged** |

- Each unsuccessful check is logged at `Error` with its category only — no response
  body, domain, client ID or exception message from the remote side (SC-10).
- The Control Plane certificate is validated normally; no validation bypass exists
  in any environment (DC-6, SC-9).
- If `LegitimacyState` cannot be saved (database unavailable), the check counts as
  unsuccessful for scheduling and readiness, is logged at `Error`, and the stored
  state is left as it was.
- The use case never calls Google (SC-13).
- Writing `LegitimacyState` is on the BR-026 closed list: it succeeds whatever the
  current mode (AC-011).

### FR-008 Read-only determination (installation)

An `Application` query answers "is the installation in read-only mode, and why",
from `LegitimacyState` and the clock, in this order:

| # | Condition | Read-only | Reason |
|---|---|---|---|
| 1 | no successful check was ever recorded | yes | legitimacy not yet confirmed |
| 2 | last known status is `suspended` | yes | suspended by the Owner |
| 3 | now − last successful check > 7 days | yes | grace period expired (with the last successful check time) |
| 4 | otherwise | no | — |

- "More than 7 days" is strict: exactly 7 days is not yet read-only
  *(interpretation I-4)*.
- If both 2 and 3 hold, the reason is "suspended by the Owner" (the order above).
- The query is in `Application`, independent of any UI (AD-6), and is what US-007
  and later screens use. This Story does not refuse any write or Google call
  (US-007).
- Row 1 applies to an installation whose `LegitimacyState` holds only an
  `upgrade_required` answer.

### FR-009 LegitimacyState persistence

- Exactly one `LegitimacyState` record per installation database, holding: last
  successful check time (UTC, may be absent), last known status, last
  compatibility state, domain, client ID (`trebovaniya.md` §3 v54, v73). No other
  field *(interpretation I-3)*.
- Created by the first check that writes it; updated in place afterwards; never
  deleted by this Story.
- Survives a restart (AC-011). Exact schema, the single-row guarantee and the
  first migration of the installation database: `db-designer` (PC-2).
- Times are UTC (PC-6).

### FR-010 Mode and result logging (installation)

The installation keeps in process memory the previous check result and the
previous read-only determination *(interpretation I-3)*:

- entering read-only mode → one `Warning` with the reason; leaving it → one
  `Information`. The determination is re-evaluated after every check and at
  startup; at startup a read-only determination is logged as entering;
- a check result that differs from the previous one — successful vs unsuccessful,
  another category, another status or compatibility state — one `Information`
  (DC-10);
- the same result and mode repeated write only the per-check lines of FR-007.

### FR-011 Last check on the school's page (Control Plane)

The `Installation` detail page (US-002 FR-005) gains a "last check" section:

- time of the last check — date and time to the minute in UTC, marked "UTC"
  (US-002 OD-003) *(interpretation I-12)*;
- application version and contract version as reported, HTML-encoded;
- status answered and compatibility state answered, as translated labels;
- no record → the translated text "not called yet";
- everything else on the page, and the installations list, unchanged;
- the page stays Owner-only (US-002); no new endpoint.

### FR-012 Logging

**Installation** (DC-10): `Information` — start and stop, check result changes and
leaving read-only mode (FR-010); `Warning` — entering read-only mode,
`upgrade_recommended`; `Error` — every unsuccessful check with its category,
unhandled exceptions. JSON lines to a rolling daily file kept 30 days with a size
cap; request id on lines written inside a request.

**Control Plane** (`trebovaniya.md` §8 v73): each check of a known installation —
`Information` with the `Installation` id, the versions and the answered status and
compatibility; an unknown id — `Warning` with the received id.

Both: never a domain, client ID, email, request or response body (SC-10, AC-015).

### FR-013 Liveness and readiness (installation)

Served on the private port only (DC-6, DC-11):

- one private route group; its filter compares `HttpContext.Connection.LocalPort`
  with the configured private port and answers `404` on any other port; `Host` and
  `X-Forwarded-Host` play no part;
- both anonymous — the SC-4 "Liveness and readiness" entry; GET only;
- **liveness** — healthy while the process runs; touches no dependency;
- **readiness**:
  - `Unhealthy` — the installation database is unreachable;
  - `Degraded` (HTTP 200) — FR-008 says read-only, or the last check since startup
    was unsuccessful (grace period still running);
  - `Healthy` — otherwise;
  - before the first check since startup completes, readiness uses the stored
    state only;
- responses carry the state only — no reason, time, version or exception;
- the private port speaks HTTP; HTTPS redirection and HSTS are not applied to it
  (DC-6);
- the "synchronization service not running" state is added by US-013.

### FR-014 Installation host (skeleton)

- Projects and references exactly as `package-map.md`; the Web project references
  `Contracts`, `Application`, `Domain`, `Infrastructure`; `Application` does not
  reference `Infrastructure`; `ControlPlane` references none of the new projects
  except `Contracts` (AD-1, AD-3).
- `Nullable` enabled and warnings as errors in every new project.
- **Public port:** this Story maps nothing on it. Every request to it answers
  `404` *(interpretation I-13)*. Sign-in, the deny-by-default fallback policy,
  antiforgery, the translated error page, localization, HTTPS redirection and HSTS
  on the public port arrive with the first installation page (US-008).
- The installation database schema is created only by migrations applied by
  deployment (PC-2, DC-4); no `EnsureCreated()`.
- DI wiring in `Web/Configuration`; `IControlPlaneClient` registered with a typed
  `HttpClient` whose base address is the configured Control Plane address.

## 5. Acceptance Criteria

Carried from the Story with the same ids; the Story is the authority for wording.

| AC | Title | Specified by |
|---|---|---|
| AC-001 | The installation starts only with its mandatory configuration | FR-001 |
| AC-002 | The check runs at startup and on schedule | FR-003 |
| AC-003 | The Control Plane answers a known installation | FR-002, FR-004 |
| AC-004 | The Control Plane decides compatibility | FR-005 |
| AC-005 | The Control Plane records the last check | FR-006, FR-012 |
| AC-006 | The Control Plane refuses an unknown or malformed call | FR-004, VR-002 |
| AC-007 | A successful check updates LegitimacyState | FR-007 |
| AC-008 | An unsuccessful check keeps the last success | FR-007 |
| AC-009 | The installation knows whether it is in read-only mode, and why | FR-008 |
| AC-010 | Mode changes are logged | FR-010 |
| AC-011 | LegitimacyState survives a restart | FR-007, FR-008, FR-009 |
| AC-012 | The Owner sees the last check on the school's page | FR-011 |
| AC-013 | The check endpoint is the service channel, not the Owner UI | FR-004 |
| AC-014 | Liveness and readiness on the private port | FR-013, FR-014 |
| AC-015 | Logs carry identifiers only | FR-012 |

## 6. Validation Rules

### VR-001 Installation configuration (installation startup)

| Setting | Required | Valid | Invalid examples |
|---|---|---|---|
| Installation id | yes | UUID in canonical text form (case-insensitive) | empty, `abc`, a UUID with braces |
| Control Plane address | yes | absolute URI, scheme `https`, host present, no user info, no query, no fragment | empty, relative, `http://…`, `https://user@host`, `https://host/?x=1` |
| Private port | yes | integer 1–65535, not equal to a public endpoint port | `0`, `70000`, `abc`, same as public port |
| Database connection string | yes | non-empty | empty |

Failure: the host does not start; the log names the setting and the rule broken,
never the value.

### VR-002 Check request (Control Plane)

| Field | Required | Valid | Invalid → |
|---|---|---|---|
| installation id | yes | UUID | `400` |
| application version | yes | `MAJOR.MINOR.PATCH`, each part 0–999999 in decimal digits without leading zeros (except `0`), at most 20 characters | `400` |
| contract version | yes | integer 1–999999 | `400` |
| body | yes | parses into the contract; unknown extra fields ignored (DC-12) | missing or malformed → `400` |

- A valid UUID that no `Installation` has is not a validation failure: it is the
  "unknown" outcome (FR-004 step 2).
- `400` records nothing; the body is never logged or echoed. Body shape: API
  design (the channel is not the public REST API, API-7).
- A contract version outside the supported set is valid input with the
  `upgrade_required` answer, not `400` (FR-005).

### VR-003 Check response (installation)

- Status ∈ {`active`, `suspended`}; compatibility ∈ {`supported`,
  `upgrade_recommended`, `upgrade_required`}; domain and client ID non-empty and
  within the lengths `trebovaniya.md` §3 v69 fixes (domain 3–253, client ID 10–32
  digits). Anything else is an "unparseable answer" (FR-007) — data returned by an
  external system is validated before it reaches business logic (AGENTS.md
  Security Policy).

### VR-004 Control Plane version settings

- Each optional; when set, `MAJOR.MINOR.PATCH` as VR-002; invalid → the Control
  Plane does not start, naming the setting.

## 7. Security Requirements

| # | Requirement | Source |
|---|---|---|
| S-01 | The check endpoint is the only endpoint added to the Control Plane anonymous and antiforgery-exemption lists, as their existing "Legitimacy check" entries; `POST` only; the US-001 enumeration tests account for it | SC-4, TC-5 |
| S-02 | Liveness and readiness are anonymous only as the SC-4 entry, bound to the private port by the local-port filter; on the public port they answer `404`, also with a forged `Host` / `X-Forwarded-Host` | SC-4, DC-6, TC-5 |
| S-03 | The contract carries only installation id, versions, status, compatibility, domain and client ID; no teaching data; the Control Plane references no installation project | SC-12, AD-1 |
| S-04 | The installation sends data only to the configured Control Plane address; no other outbound destination; no Google call | SC-13 |
| S-05 | The Control Plane address is HTTPS and its certificate is validated; no bypass | DC-6, SC-9 |
| S-06 | An unknown id or invalid body reveals no status, domain or client ID and records nothing | SC-12, FR-004 |
| S-07 | The check response is validated before use (VR-003) | AGENTS.md Security Policy |
| S-08 | Logs carry identifiers, versions, categories and states only — never domain, client ID, email, request or response body, exception text from the remote side; rejected bodies never logged | SC-10, DC-10 |
| S-09 | Readiness and liveness answer state only | DC-11, SC-6 |
| S-10 | Configuration errors name the setting, never the value | SC-10 |
| S-11 | Controllers see DTOs only; no `DbContext` in `Web` or in Control Plane `Controllers`; no Google SDK or HTTP type in `Application` | AD-3, AD-4, AD-8 |
| S-12 | Version numbers and texts shown on the detail page are HTML-encoded | SC-10 (output safety) |
| S-13 | No secret is added to configuration or source by this Story; the connection string comes from configuration or environment only | SC-7, PC-1 |
| S-14 | Checks write no audit row on either side | `trebovaniya.md` §5, §9 v73 |

## 8. Error Handling

### Control Plane

| Situation | Response | Recorded | Log |
|---|---|---|---|
| Valid call, known installation | success answer (FR-002) | `InstanceLicenseCheck` replaced | `Information` |
| Valid call, unknown id | "unknown" answer, no data | nothing | `Warning` |
| Invalid / missing / malformed body | `400` | nothing | none with the body |
| Method other than POST | not handled (`405`/`404`, API design) | nothing | — |
| Concurrent calls, same installation | each answered | one record | per call |
| Unhandled exception | `500`, no detail | nothing | `Error` |
| Detail page, no check yet | "not called yet" | — | — |

### Installation

| Situation | Effect | Log |
|---|---|---|
| Invalid configuration | host does not start | names the setting |
| Successful check | `LegitimacyState` updated; next in 6 h | `Information` on result change; `Warning` for `upgrade_recommended` |
| Unsuccessful check (any category) | per FR-007 table; next in 15 min | `Error` with category |
| `LegitimacyState` save fails | unsuccessful for schedule/readiness; state as before | `Error` |
| Exception inside the check | unsuccessful; service keeps running | `Error` without remote detail |
| Enters / leaves read-only | — | `Warning` / `Information`, once |
| Database unreachable | readiness `Unhealthy` | — |
| Request to public port | `404` | — |

Expected outcomes (unknown installation, unsuccessful check, invalid input) are
results, not exceptions (AD-9).

## 9. Non-Functional Requirements

- **NFR-013** — 7 days without a successful check before read-only (FR-008).
- **NFR-014** — check every 6 hours; the push is US-006 (FR-003).
- **NFR-015** — structured logs; liveness and readiness on the private network
  (FR-012, FR-013).
- **NFR-017** — versions reported on every check; `upgrade_required` consumes the
  grace period (FR-005, FR-007).
- **NFR-061** — semantic versioning; the installation reports its release version
  (I-7); versioned migrations.
- **NFR-062** — .NET 10; nullable enabled; warnings as errors.
- **NFR-073** — the detail page additions in Ukrainian and English
  (`ControlPlane.Localization`), Ukrainian by default (FR-011, TC-8). The
  installation shows no user-visible text in this Story.
- **Testability** — the clock and `IControlPlaneClient` are substitutable; schedule
  and grace-period tests advance a fake clock; installation tests never call a real
  Control Plane; the `IControlPlaneClient` implementation is tested against a local
  test HTTP server (timeouts, error answers, unparseable answers); both databases
  run on PostgreSQL via Testcontainers (TC-2).

## 10. Out of Scope

- Refusing writes and Google calls in read-only mode (US-007).
- The status-change push and its receiver (US-006).
- Any Admin or Dean screen, including the legitimacy status of the §2 matrix and
  the reason of an `upgrade_required` answer (after US-008 / US-012).
- The Admin login check (US-008).
- The installation host baseline on the public port — sign-in, deny-by-default,
  antiforgery, error page, localization, HTTPS redirection, HSTS (US-008).
- School time zone, retention period N, default UI language settings (I-1).
- The "synchronization service not running" readiness state (US-013).
- A history of checks; a check list page in the Control Plane.
- Anything in Google.

## 11. Open Decisions

Full text: `docs/decisions/US-005-open-decisions.md`.

| Id | Subject | Status | Impact |
|---|---|---|---|
| OD-001 | NuGet packages for the installation projects | RESOLVED (2026-09-17): option 1 | without it `Infrastructure` and `Web` cannot reference EF Core, Npgsql or Serilog (FR-009, FR-012, FR-014) |
| OD-002 | Compile-only installation skeleton created at TEST_WRITING | RESOLVED (2026-09-17): option 1 | without it TEST_WRITING cannot produce compiling red tests |

Neither changes a business rule; both only let the Story be built and tested.

### Interpretations for human review

A rejected interpretation becomes an Open Decision.

- **I-1** Only the settings this Story uses are required now: installation id,
  Control Plane address, private port, database connection string. The time zone,
  retention period N and default language — required by `trebovaniya.md` §5 and
  listed as required in DC-3 — are added by the Stories that first use them, so
  each gets a test (Story Notes). DC-3 is not changed.
- **I-2** The private port is a required setting: liveness and readiness exist
  only on it (DC-6), so without it they would be unreachable.
- **I-3** `LegitimacyState` stores exactly the fields of `trebovaniya.md` §3. The
  previous check result and the previous read-only determination, used for
  readiness (FR-013) and log-on-change (FR-010), live in process memory: persisting
  them would add a write to the BR-026 closed list. After a restart the check runs
  at once, so nothing useful is lost.
- **I-4** "More than 7 days" is strict and measured with the installation's own UTC
  clock: exactly 7 days after the last success the school still works.
- **I-5** Intervals count from the completion of the previous check and are fixed
  (6 h / 15 min); no exponential backoff for the check (the push has its own
  retries, NFR-014).
- **I-6** The 30 seconds cover the whole call — connecting, TLS and reading the
  answer.
- **I-7** The installation's application version is its release version
  `MAJOR.MINOR.PATCH` (NFR-061), numeric comparison, no pre-release suffix. The
  contract version is a positive integer declared in `Contracts`; the Control Plane
  declares the set it supports in code — `{1}` in this Story. A breaking contract
  change adds a version and keeps the old one supported for a window (DC-12).
- **I-8** Minimum supported and recommended versions are optional Control Plane
  settings; unset means no limit; set but invalid stops the Control Plane at
  startup. A recommended version below the minimum is accepted and simply never
  applies.
- **I-9** An unknown installation id gets a distinct "unknown" answer (not `400`,
  not `500`) carrying no data, and the Control Plane logs the received id at
  `Warning` — a UUID, not personal data. The HTTP status for it is API design.
- **I-10** `InstanceLicenseCheck` is written for every call of a known
  installation, `upgrade_required` included: it is exactly the case the Owner needs
  to see on the school's page.
- **I-11** A suspended installation still gets its compatibility computed and
  recorded; suspension does not short-circuit the answer.
- **I-12** The last check time on the detail page follows the creation date
  display of US-002 (UTC, to the minute, marked "UTC").
- **I-13** The installation's public port maps nothing in this Story and answers
  `404` to every request without the translated error page, which arrives with the
  host baseline in US-008. Nothing on the public port reads or writes data.
- **I-14** Concurrent checks for one installation in the Control Plane: last write
  wins, exactly one record, no `500`.

## 12. Traceability

| AC | Functional requirements | Validation rules | Security | Open Decisions |
|---|---|---|---|---|
| AC-001 | FR-001, FR-014 | VR-001 | S-10, S-13 | OD-001, OD-002 |
| AC-002 | FR-003 | — | — | OD-002 |
| AC-003 | FR-002, FR-004 | VR-002 | S-01, S-03 | — |
| AC-004 | FR-005 | VR-002, VR-004 | — | — |
| AC-005 | FR-006, FR-012 | — | S-08, S-14 | — |
| AC-006 | FR-004 | VR-002 | S-06, S-08 | — |
| AC-007 | FR-007, FR-009 | VR-003 | S-05, S-07 | — |
| AC-008 | FR-007 | VR-003 | S-07, S-08 | — |
| AC-009 | FR-008 | — | — | — |
| AC-010 | FR-010, FR-012 | — | S-08 | — |
| AC-011 | FR-007, FR-008, FR-009 | — | — | OD-001 |
| AC-012 | FR-011 | — | S-12 | — |
| AC-013 | FR-004 | — | S-01 | — |
| AC-014 | FR-013, FR-014 | — | S-02, S-09 | — |
| AC-015 | FR-012 | — | S-08 | — |

Requirements with no single AC but required by conventions the Story cites:
FR-014 skeleton rules (AD-1 … AD-3, PC-2), S-04 (SC-13), S-11 (AD-3, AD-4, AD-8).
