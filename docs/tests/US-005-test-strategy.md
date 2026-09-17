---
artifact_type: test_strategy
story: US-005
version: 1
status: DRAFT
created_at: 2026-09-17T14:25:00Z
updated_at: 2026-09-17T14:25:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-005-installation-legitimacy-check.md
    version: null
  - path: docs/specifications/US-005-spec.md
    version: 1
  - path: docs/decisions/US-005-open-decisions.md
    version: 1
  - path: docs/designs/api/US-005-api-design.md
    version: 1
  - path: docs/designs/api/US-005-openapi.yaml
    version: 1
  - path: docs/designs/database/US-005-db-design.md
    version: 1
  - path: docs/designs/database/US-005-entity-model.md
    version: 1
supersedes: null
---

# US-005 Test Strategy — Installation legitimacy check and grace period

## 1. Scope

Both hosts. **Control Plane:** the service-channel endpoint, compatibility, the
`instance_license_check` record, the last-check section of the detail page, its
translations, schema, migration and the SC-4 enumeration tests. **Installation:**
configuration at startup, the background check schedule, recording
`legitimacy_state`, the read-only determination, mode and failure logging, liveness
and readiness on the private port, the empty public port, the HTTP client's reply
classification, the first migration, and project references. One contract suite runs
the installation's real client against the in-process Control Plane.

Not tested here (out of scope): refusing writes in read-only mode (US-007), the push
(US-006), any installation page or the host baseline (US-008), time zone / N /
language settings (spec I-1), "synchronization service not running" readiness
(US-013).

## 2. Test levels

| Level | Why | Classes |
|---|---|---|
| Integration — Control Plane host + PostgreSQL (TC-2) | the endpoint, record, page and concurrency are observable through HTTP and the database | `LegitimacyCheckEndpointTests`, `LegitimacyCheckCompatibilityTests`, `InstanceLicenseCheckRecordTests`, `InstallationLastCheckTests` |
| Integration — installation host + PostgreSQL, substituted Control Plane client and clock (TC-2, TC-4) | schedule, recording, mode, logging and health are behaviour of the running host | `LegitimacyCheckScheduleTests`, `CheckLegitimacyRecordingTests`, `LegitimacyModeQueryTests`, `LegitimacyLoggingTests`, `InstallationConfigurationTests`, `HealthEndpointTests`, `InstallationEndpointTests` |
| Contract — both sides | the wire contract cannot drift: real `ControlPlaneClient` → in-process Control Plane | `LegitimacyChannelContractTests` |
| Unit — HTTP client with a scripted handler | every reply class, including a 30-second timeout on the manual clock, without a network | `ControlPlaneClientTests` |
| Persistence | columns, named constraints, single row, migration history, no startup migration, model drift (PC-2, PC-4) | `InstanceLicenseCheckSchemaTests`, `MigrationTests` (changed), `InstallationDatabaseTests` |
| Security (TC-5) | anonymous and antiforgery-exempt lists; private port binding; public port 404; no leaks in logs or bodies | `AnonymousEndpointTests`, `AntiforgeryTests` (both changed), `HealthEndpointTests`, `InstallationEndpointTests`, log assertions |
| Localization (TC-8) | every new key in `uk` and `en`; Ukrainian default | `LastCheckTranslationTests`, `InstallationLastCheckTests` |
| Architecture | project references and framework-free layers (AD-1, AD-3, AD-4) | `ProjectReferenceTests` |
| Seam guard | the manual clock and the fake client work, so a red schedule test means missing behaviour | `ManualTimeProviderTests` |

The US-001 host-wide suites (`TranslationCompletenessTests`, session, cookies, error
page) pick up the Control Plane changes without edits.

## 3. Seams and contracts the implementation must honour

These are fixed by the tests; renaming one changes both sides.

1. **Configuration keys** (`InstallationConfigurationKeys`, api-design §10):
   `Installation:Id`, `ControlPlane:Address`, `Hosting:PrivatePort`,
   `ConnectionStrings:Installation`; public endpoints from `urls`; log directory
   `LogFile:Directory` (the Control Plane's key, US-001 — not validated by US-005).
   Control Plane: `Compatibility:MinimumSupportedVersion`,
   `Compatibility:RecommendedVersion`. Invalid values make host start throw; the
   exception chain names the key and not the value.
2. **Installation entry point** `ClassroomAgent.Web.Program` — a class in a
   namespace, not top-level statements, so the test project can reference both hosts
   (`WebApplicationFactory<ClassroomAgent.Web.Program>`). Tests set settings through
   `UseSetting` and replace two services with `ConfigureTestServices`.
3. **Clock.** `TimeProvider` is resolved from DI. Every wait of the check schedule and
   the 30-second call limit use `TimeProvider` timers (`Task.Delay(…, TimeProvider, …)`,
   `CancellationTokenSource(TimeSpan, TimeProvider)` or `PeriodicTimer`) — the manual
   clock fires them. "Check finished" is observed as a pending timer due exactly at
   completion + 6 h (success) or + 15 min (failure).
4. **`IControlPlaneClient`** (Application port) is resolved from DI; tests substitute
   `FakeControlPlaneClient`. `ControlPlaneClient(HttpClient, TimeProvider)` in
   `Infrastructure.ControlPlane` is constructed directly by the client tests; its
   `HttpClient.BaseAddress` is the Control Plane address.
5. **Read-only query** `GetLegitimacyModeQuery.ExecuteAsync(CancellationToken)` →
   `LegitimacyMode(IsReadOnly, Reason, LastSuccessfulCheckAt)`, resolved from a DI scope.
6. **Log event names** — `EventId.Name` in the Serilog compact JSON line:

   | Host | Event name | Level | Property |
   |---|---|---|---|
   | installation | `LegitimacyCheckFailed` | Error | `Category` = `Unreachable` \| `Timeout` \| `ErrorAnswer` \| `UnparseableAnswer` \| `UnknownInstallation` \| `UpgradeRequired` \| `SaveFailed` |
   | installation | `LegitimacyUpgradeRecommended` | Warning | — |
   | installation | `LegitimacyCheckResultChanged` | Information | — |
   | installation | `ReadOnlyModeEntered` | Warning | `Reason` = `NotYetConfirmed` \| `SuspendedByOwner` \| `GracePeriodExpired` |
   | installation | `ReadOnlyModeLeft` | Information | — |
   | Control Plane | `LegitimacyCheckAnswered` | Information | versions in the line |
   | Control Plane | `LegitimacyCheckUnknownInstallation` | Warning | received id in the line |

7. **Private port** — requests are sent with `TestServer.SendAsync` and
   `Connection.LocalPort` = 8081 (private) or 443 (public) (DC-6, TC-5).
8. **Installation database** — `ClassroomAgentDbContext(DbContextOptions<…>)`
   migrated by the tests before start; the host never migrates.
9. **Test data** — synthetic domains `*.example.test`, client IDs of digits, fixed
   UTC start 2026-09-17 08:00 (TC-4).

## 4. Scenarios

### Positive
- Known active / suspended installation → `200` with exactly the four properties;
  unknown extra request properties ignored; upper-case UUID accepted (AC-003).
- Compatibility table: below minimum / unsupported contract → `upgrade_required`;
  below recommended → `upgrade_recommended`; otherwise `supported`; numeric compare
  (`1.10.0` > `1.4.0`); unset settings impose nothing (AC-004).
- First call creates the record; later calls replace it; `created_at` kept; each
  installation its own record (AC-005).
- First check right after start carrying the configured id, contract version 1 and a
  `MAJOR.MINOR.PATCH` version; next check 6 h after success, from completion (AC-002).
- Successful, suspended and `upgrade_recommended` answers write `legitimacy_state`
  (AC-007).
- Mode: active and recent → not read-only; first success ends "not yet confirmed"
  (AC-009).
- Last check shown with UTC minute time, versions and translated labels (AC-012).
- Liveness `Healthy`; readiness `Healthy` after a success (AC-014).
- Real client ↔ Control Plane: answer, `upgrade_required` answer (AC-003).

### Negative
- Unknown id → `404 {"outcome":"unknown_installation"}`, nothing recorded, `Warning`
  (AC-006).
- 23 malformed requests (body, content type, JSON shape, each field's type and format)
  → `400 {"outcome":"invalid_request"}`, nothing recorded, body not logged (AC-006).
- GET/PUT/PATCH/DELETE on the check path → `404`/`405`, nothing recorded (AC-013).
- Before Owner setup → `302 /setup` (api-design §3); the real client classifies it as
  `ErrorAnswer`.
- Each failure category and `upgrade_required` → next check in 15 min, last success
  unchanged, `Error` with category; no row created by a failure (AC-008).
- Exception thrown by the client → failure, schedule and host keep running (AC-002).
- Missing or invalid settings → host does not start, key named, value hidden (AC-001).
- Client: 18 invalid `200` bodies → `UnparseableAnswer`; 14 other statuses (incl.
  `404` without the outcome body, `3xx`, `201`) → `ErrorAnswer`; connection and TLS
  failure → `Unreachable` (AC-008).
- Public port: every method and path → `404`, empty body, no cookie, no redirect;
  health paths also with forged `Host` / `X-Forwarded-Host` / `X-Forwarded-Port`
  (AC-014).

### Boundary
- `applicationVersion` `0.0.0` and `999999.999999.999999`; `contractVersion` 1 and
  999999 accepted; `1000000.0.0`, `0`, `1000000` refused (VR-002).
- Grace period: exactly 7 days → not read-only; 7 days + 1 s → read-only; clock
  moving past 7 days without any check (I-4).
- Next check at 6 h − 1 s not yet called; at 6 h called (AC-002).
- Timeout: at 29 s the call is pending, at 30 s `Timeout`; answer at 29 s is an answer
  (I-6).
- Private port 1 and 65535 accepted; 0, −1, 65536 refused; private port equal to a
  public port refused (VR-001).
- Client ID 10 and 32 digits; domain 3 and 253 characters (VR-003, db-design §4.1).

### Validation
VR-001 (installation configuration), VR-002 (request), VR-003 (answer), VR-004
(Control Plane version settings) — each rule has a positive and a negative case above.

### Security
- The check POST is the only anonymous POST and the only antiforgery exemption of the
  Control Plane (`AnonymousEndpointTests`, `AntiforgeryTests`); a session cookie
  changes nothing (S-01, AC-013).
- No audit row for any call — known, unknown or invalid (S-14).
- Answers and `404`/`400` bodies carry no domain for unknown/invalid calls; answers do
  not echo id or versions (S-06, SC-12).
- Logs of both hosts contain no domain, client ID, installation name, rejected body or
  exception text from the call (S-08, AC-015).
- Readiness body is one word, no reason/time/version; no HSTS or redirect on the
  private port (S-09, DC-6).
- Installation endpoints: only `health/live` and `health/ready`, GET, anonymous (S-02).
- Settings never stored in the database (FR-001).
- Architecture: Control Plane references only `Contracts`; `Application` references no
  EF Core, Npgsql, ASP.NET Core, `Contracts` or `Infrastructure`; no `DbContext` in
  `Web`; `Contracts` has no teaching-data type (S-03, S-11).

### Persistence
- `instance_license_check`: columns, `pk_`, `uq_…_installation_id`, FK, four checks;
  duplicate row and unknown installation refused; concurrent first calls → one row,
  all `200` (db-design §3).
- `legitimacy_state`: columns, `singleton` default `true`, single-row unique and
  check, four value checks, nullable last success (db-design §4).
- Migrations: Control Plane four in order; installation exactly
  `InitialLegitimacyState`, no row, no pending model changes; host start applies no
  migration (PC-2).
- Restart over the same database reads the stored row (AC-011).

## 5. Fixtures

| Fixture | Purpose |
|---|---|
| `PostgreSqlFixture` (existing) | one container, a database per test, dropped on disposal |
| `ControlPlaneTestHost` (extended) | optional extra settings; `CreateServerHandler()` for the contract tests |
| `InstallationTestHost` | create → seed → script → start; manual clock; fake client; private/public requests; SQL helpers; log reading; restart |
| `InstallationFactory` | `WebApplicationFactory<ClassroomAgent.Web.Program>` replacing `TimeProvider` and `IControlPlaneClient` |
| `ManualTimeProvider` | time and timers moved by the test |
| `FakeControlPlaneClient` | scripted replies in order; blocks until cancelled when none left; records calls |
| `LogEvent` | compact JSON log line parser (level, event name, properties) |
| `LegitimacyCheckHostExtensions`, `LegitimacyCheckTestData`, rows | channel calls, JSON reading, row readers |

## 6. Excluded scenarios

| Scenario | Why |
|---|---|
| Real certificate validation of the Control Plane address | needs a TLS server with an untrusted certificate; covered by "no bypass" code review (S-05) and the TLS-failure classification test |
| Redirects not followed by the production `HttpClient` handler | the handler configuration is DI wiring; the classification of a `3xx` is tested with a scripted handler |
| `LegitimacyState` save failure (`SaveFailed`) | no reliable way to fail one save in a running host without a production seam; category name fixed in §3 for the implementation |
| Log file rotation and retention | DC-10 configuration, same sink as US-001 |
| Graceful-shutdown timing beyond "stop completes, no new check" | framework behaviour |

## 7. Known limitations

- `LegitimacyCheckResultChanged` count is asserted as 2–3: whether the very first
  check (no previous result) counts as a change is left open by spec FR-010.
- Concurrency tests (8 parallel first calls, 3 rounds) show the absence of a failure
  over those runs; the guarantee rests on the unique index and the retry (db-design §3.2).
- "Only one check at a time" is asserted while the call hangs for 29 s of clock time,
  below the call limit; that implementations sequence checks is also forced by
  "interval counts from completion".
- The schedule is observed through pending timers; an implementation that waits in
  some other way than `TimeProvider` timers fails these tests by design (§3 item 3).
- A failing host start may leave its test database undropped (4 Control Plane
  version-setting cases, installation configuration cases) — the container is removed
  after the run.

## 8. Open Decisions affecting testing

None open. OD-001 (packages) and OD-002 (compile-only skeleton at TEST_WRITING) were
resolved option 1 and applied: the skeleton is listed in the test-generation report.
