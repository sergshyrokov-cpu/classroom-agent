---
artifact_type: security_review
story: US-006
version: 1
status: APPROVED
created_at: 2026-09-19T09:45:00Z
updated_at: 2026-09-19T09:45:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-006-control-plane-push.md
    version: null
  - path: docs/specifications/US-006-spec.md
    version: 2
  - path: docs/decisions/US-006-open-decisions.md
    version: 2
  - path: docs/evidence/US-006-implementation-report.md
    version: 2
  - path: docs/designs/api/US-006-api-design.md
    version: 1
  - path: docs/designs/api/US-006-openapi.yaml
    version: 1
  - path: docs/designs/database/US-006-db-design.md
    version: 1
  - path: docs/designs/database/US-006-entity-model.md
    version: 1
  - path: docs/tests/US-006-test-strategy.md
    version: 1
  - path: docs/tests/US-006-ac-test-matrix.md
    version: 2
  - path: trebovaniya.md
    version: 77
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 2
informational_findings: 2
security_sensitive: true
runtime_checks: PARTIAL
---

# US-006 Security Review — Control Plane push on status change

## 1. Executive Summary

**Result: PASS.** No Critical and no Major finding. Two Minor and two
Informational findings are recorded; none of them blocks the commit.

The Story adds one anonymous endpoint and one outbound flow, and both are
already on the closed lists of `security-conventions.md`: the status-change push
receiver appears in the SC-4 anonymous list and in the SC-4 antiforgery
exemption list, and the Control Plane → installation direction of the service
channel is the one SC-9 describes. Nothing in the change widens either list.

The four security properties that carry the most weight were verified
independently and hold:

1. **A push cannot set a status.** `StatusPushEndpoint` compares the received id
   with the configured one and calls the coordinator; it holds no repository and
   writes nothing. The status still comes from the ordinary US-005 check.
2. **The receiver answers on the private port only.** Two independent controls —
   `PublicPortMiddleware` short-circuits every request that did not arrive on the
   private port, and the endpoint itself carries `PrivatePortEndpointFilter`.
   Neither consults `Host` or `X-Forwarded-Host`.
3. **Push checks cannot be used to flood the Control Plane.** At most one check
   runs at a time, a push-triggered check starts at most once a minute, and a
   push that cannot start one leaves a single flag — never a queue.
4. **The push address never reaches an audit row or a log line.** The audit
   factory carries ids only, and the address that `IHttpClientFactory` used to
   write into the log at `Information` no longer appears anywhere.

Principal residual risk: the Owner supplies the push address and the Control
Plane calls it, which is a request-forgery surface by design (SC-9, v76). It is
narrow — `http://host:port` only, a fixed one-UUID body, no redirects followed,
no cookies or credentials sent, and the response body is never read — so nothing
can be read back through it. Recorded as Informational (F-3).

Review limitation: that Kestrel really binds the private endpoint to
`Hosting:PrivateAddress` alone cannot be observed under `TestServer`, which has
no listening socket. This is the check DC-2 assigns to deployment, and the
Specification records it as out of scope (§10). Recorded in section 21.

Recommended next action: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| User Story | `docs/stories/US-006-control-plane-push.md` | — |
| Specification | `docs/specifications/US-006-spec.md` | 2 (APPROVED) |
| Open Decisions | `docs/decisions/US-006-open-decisions.md` | 2 (APPROVED) |
| Implementation Report | `docs/evidence/US-006-implementation-report.md` | 2 (`PASS`) |
| API design | `docs/designs/api/US-006-api-design.md` | 1 |
| OpenAPI | `docs/designs/api/US-006-openapi.yaml` | 1 |
| Database design | `docs/designs/database/US-006-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-006-entity-model.md` | 1 |
| Test strategy | `docs/tests/US-006-test-strategy.md` | 1 |
| AC → test matrix | `docs/tests/US-006-ac-test-matrix.md` | 2 |
| Requirements | `trebovaniya.md` | 77 |
| Security conventions | `docs/architecture/security-conventions.md` | — |

No input is `SUPERSEDED`. `HUMAN_SPEC_APPROVAL` is recorded (2026-09-17T15:37:50Z).
No unresolved `TODO` / `TBD` / `FIXME` / Open Decision marker remains in any of
them; OD-001 is RESOLVED.

## 3. Security-Relevant Scope

**Installation host.** One new endpoint, `POST /service/v1/status-pushes`, on the
private port: anonymous, antiforgery-exempt, accepting a JSON body of at most
4 KB. One new mandatory setting, `Hosting:PrivateAddress`, which decides the
address the private endpoint binds to. One new piece of process state,
`PushCheckCoordinator`, which decides when a legitimacy check may start.

**Control Plane.** Two new Owner-only pages, `GET`/`POST
/installations/{id}/push-address`, and an optional field on the existing
registration form. One new nullable column, `installation.push_address`, with a
check constraint and a new audit action code. One new **outbound** flow: an HTTP
`POST` from the Control Plane to a school's private port.

**Assets touched.** The `Installation` status (the school's whole working state),
the push address (infrastructure detail, not personal data), Control Plane audit
rows, the Owner's session. No student or staff personal data is read, written,
shown or exported anywhere in this Story.

**Trust boundaries crossed.** A new one, in a direction that did not exist
before: Control Plane → a school's private port, unauthenticated, protected by
network isolation (SC-9). The existing installation → Control Plane direction is
unchanged.

## 4. Environment and Tools

- .NET SDK 10.0.401.
- Docker Desktop 29.8.0; Testcontainers with PostgreSQL available, so the
  integration and schema tests ran against a real database (TC-2).
- Commands run for this review: `dotnet build ClassroomAgent.sln`
  (0 errors, 0 warnings), the full test suite through the xUnit v3 runner
  (**1085 passed, 0 failed, 0 skipped**), `dotnet list package --vulnerable`
  (no vulnerable package in any of the seven projects), `git status` for
  repository hygiene, and direct reading of the changed source, configuration
  and migration files.
- Not run: any live Google API call, any live Control Plane call, any connection
  to a database other than the Testcontainers instances. No application was
  exposed on an external interface.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | The Story adds no role, policy or permission cell. The push address pages reuse the existing `Owner` policy. |
| SC-2 Authentication | NOT_APPLICABLE | No password, credential, cookie or sign-in path is touched. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Not touched; `AddingAndRevokingAnAllowedAdmin_SendNoPush` confirms the Story does not react to those changes either. |
| SC-4 Authorization | **PASS** | Section 6. The receiver is on the anonymous list and on the antiforgery exemption list; the two new Control Plane routes are Owner-only, token-protected and behind the setup gate; no other endpoint became anonymous or exempt. |
| SC-5 Read-only mode | **PASS** | The Story adds no write use case to the installation. The only write the triggered check performs is the `LegitimacyState` upsert, which is on the BR-026 closed list; `StatusPushEndpointTests.InReadOnlyMode_ThePushIsAccepted_AndTheCheckWritesTheState` proves it behaves identically in read-only mode, which is what AC-014 requires. |
| SC-6 No DB UI | **PASS** | No database, SQL or diagnostic surface is added; the receiver returns `202`/`400`/`404`/`413` with no body or a one-word `ServiceOutcome`. |
| SC-7 Key | **PASS** | No secret is added to source or configuration. The one new setting is an IP literal or `*`. No Data Protection change. |
| SC-8 Google | NOT_APPLICABLE | No Google scope, call or credential is touched. |
| SC-9 Channel | **PASS** | Section 6 and section 10. Private-port binding with no default; a push only triggers a check; at most one check at a time and at most one push-triggered check a minute with a single pending flag; a foreign id answers `404` and does nothing. |
| SC-10 Hygiene | **PASS** | Section 8 and section 12. No internals in any response; no rejected body, address or domain in any log line. |
| SC-11 Audit | **PASS** | Section 12. `installation_push_address_changed` is written for set, change and clear, in the same transaction, with ids only and no address; refused and unchanged submissions write nothing; push delivery is never audited. |
| SC-12 Owner | **PASS** | `ClassroomAgent.Contracts` gains only `StatusPushRequest(Guid? InstallationId)`. `ClassroomAgent.ControlPlane.csproj` references `Contracts` alone — no `Domain`, no installation project. No school statistic reaches the Control Plane. |
| SC-13 Outbound | **PASS** | Section 10. The one new outbound flow is the Control Plane → installation direction of the service channel SC-9 already describes, and it carries one UUID and nothing else. |

## 6. Authentication and Authorization

### Installation host

`HostEndpoint` enumeration over the live `EndpointDataSource` (not the source
code) gives exactly three routed endpoints: `health/live`, `health/ready` and
`service/v1/status-pushes`. All three are anonymous, and all three appear on the
SC-4 anonymous list — the first two under "Liveness and readiness", the third
under "Status-change push receiver". The receiver is the only one accepting an
unsafe method, it accepts `POST` alone, and it carries antiforgery-exemption
metadata, which the SC-4 exemption list also permits for exactly this endpoint.
Verified by `Web.Security.InstallationEndpointTests`
(`RoutedEndpoints_AreLivenessReadinessAndThePushReceiver_AllAnonymous`,
`ThePushReceiver_IsTheOnlyUnsafeEndpoint_PostOnly_AndExemptFromAntiforgery`).

The port restriction is two independent controls, both reading
`HttpContext.Connection.LocalPort` and neither consulting a header:

- `PublicPortMiddleware` — every request whose local port is not the private port
  answers `404` with an empty body before routing;
- `PrivatePortEndpointFilter`, attached to the receiver itself in
  `PrivateEndpoints.MapPrivateEndpoints`.

Observed, not assumed: `OnThePublicPort_ThePathIsNotFound_AndStartsNoCheck` and
`OnThePublicPort_AForgedHostDoesNotReachTheReceiver` (which sends both a forged
`Host` and a forged `X-Forwarded-Host` naming the private port) both get `404`
and start no check. `OtherMethods_DoNotReachTheReceiver` covers `GET`, `PUT` and
`DELETE`.

### Control Plane

The two new routes sit on `InstallationsController`, which declares
`[Authorize(Policy = OwnerSession.OwnerPolicy)]` at class level; the host's
`GlobalAntiforgeryFilter` validates every POST. Independently verified:

- `InstallationAuthorizationTests.InstallationEndpoints_ExistAndNoneAllowsAnonymous`
  now enumerates `installations/{id}/push-address` among the installation
  endpoints and asserts that none of them allows anonymous access;
- `PushAddressSecurityTests` covers no session → `302 /sign-in` (GET and POST,
  and the POST changes nothing), a session without the `Owner` role → `403`
  (again changing nothing), a POST without a token → `400` with the translated
  "page expired" text and nothing changed, and the setup gate → `302 /setup`
  before first-run setup;
- `GettingThePushAddressPage_ChangesNothing` confirms the GET form is not a
  state-changing action even with a `?pushAddress=` query (SC-4 "GET changes
  nothing", API-4).

The guards `ControlPlane.Security.AnonymousEndpointTests` and `AntiforgeryTests`
are green, so the SC-4 lists were not widened on the Control Plane either.

## 7. Credentials, Key and Google Access

Not touched by this Story. No password, hash, token, OAuth flow, Google scope,
service-account key or key reference is read, written, logged or added to
configuration. `Hosting:PrivateAddress` is an IP literal or `*` and is not a
secret. `dotnet list package --vulnerable` reports nothing, and no project
reference or NuGet package was added.

## 8. Sensitive Data Exposure

- **Responses.** The receiver answers `202` and `413` with an empty body, and
  `400`/`404` with `ServiceOutcome` — one of the two fixed words
  `invalid_request` / `unknown_installation`. No internal name, path, SQL or
  exception text is reachable through it.
- **Views and DTOs.** `InstallationDetailDto` gains `PushAddress` only; no
  domain entity crosses into a view model (AD-8). The address is rendered through
  Razor's automatic HTML encoding, and
  `InstallationPushAddressValidationTests.RefusedForm_EncodesTheTypedValue`
  proves a `"><script>` payload is neither executed nor stored.
- **Personal data.** None is involved: the Story touches the `Installation`
  record and process state only. No student or staff name, email or grade is read
  anywhere in the change.
- **Exports and telemetry.** Untouched. `docs/hooks/tool-usage.jsonl` remains
  git-ignored and metadata-only.
- **Test fixtures.** Synthetic throughout (`PushTestData`, `PushClientStub`,
  `FakeControlPlaneClient`); no test reaches a live Google API or Control Plane
  (TC-4).

## 9. Input Validation

**Push body (VR-002).** Validated before any use, in this order: `Content-Length`
over 4096 bytes → `413`; body absent or the content type not `application/json`
→ `400`; body not parsable into `StatusPushRequest`, or `installationId` missing
or not a UUID → `400`. The reader buffers at most 4097 bytes, so an oversized or
chunked body cannot be used to exhaust memory. Unknown JSON properties are
ignored, as DC-12 requires, and
`StatusPushEndpointTests.UnknownPropertiesInThePush_AreIgnored` proves a body
carrying `"status":"suspended"` changes nothing — the push still only triggers a
check. Seven malformed shapes are covered by `MalformedPush_IsRefused_AndStartsNoCheck`.

A valid UUID that is not this installation's is deliberately **not** a validation
failure: it is the `404` outcome, so the two cases stay distinguishable in the
log without either revealing an id.

**Push address (VR-001).** `PushAddressRules` applies the six rules in the fixed
order of api-design §7 and reports the first failure as its own translation key;
`InstallationPushAddressAttribute` runs it through Data Annotations, so model
binding activates it on both the registration form and the push address page
(`InvalidAddressAtRegistration_IsReportedTogetherWithOtherFieldErrors` shows it
joining the other field errors). Sixteen invalid shapes and seven valid boundary
values are covered on both surfaces — 42 cases in total. The canonical form is
what reaches the database, and the check constraint
`ck_installation_push_address_format` refuses anything else written by another
route (eight non-canonical shapes proved by direct SQL).

## 10. API Security

The implementation exposes exactly the endpoints the approved API design lists
and no others; the enumeration tests of both hosts would fail otherwise.

**The outbound push** was reviewed as carefully as the inbound one, because it is
the new trust boundary:

| Property | Evidence |
|---|---|
| carries one UUID and nothing else | `Suspending_PostsThePushToTheStoredAddress` asserts the JSON object has exactly the property `installationId` |
| goes only to the address stored for that `Installation` | the address is read inside the status-change transaction and handed over after commit (`InstallationStatusService`); `PushesToDifferentSchools_AreIndependent` shows each school's own address used |
| no cookies, no credentials | asserted on the recorded attempt headers; the named client is configured `UseCookies = false` |
| redirects not followed | `AllowAutoRedirect = false`; a `302` is classified as a failed attempt and retried (`UnexpectedStatus_IsRetriedThreeTimes_ThenAbandoned(Found)`) |
| the response body is never read | `HttpCompletionOption.ResponseHeadersRead` and only `StatusCode` inspected |
| bounded in time and number | 10-second attempt timeout on the injected clock; at most four attempts at +0, +5 s, +35 s, +155 s; `404` never retried |
| leaves nothing behind | `DeliveryIsNotAudited_AndChangesNothingStored`: one `installation_suspended` row, no push row, the address unchanged |

## 11. Persistence and Configuration

- `installation.push_address` is `character varying(255)`, nullable, with no
  index and no unique constraint — confirmed against the live schema by
  `InstallationPushAddressSchemaTests`, and the US-002 guard
  `InstallationSchemaTests` now enumerates it with its exact type and
  nullability.
- The shape constraint `ck_installation_push_address_format` is defence in depth
  behind VR-001: it accepts the canonical form and NULL and refuses `https://`,
  a missing port, a trailing slash, a path, an upper-case host, a leading-zero
  port, a scheme-less value and the empty string.
- The immutability trigger was correctly **not** extended: the address is
  changeable at any status, while `domain` and `identifier` stay immutable —
  both halves proved in one test.
- The schema change ships as the fifth Control Plane migration,
  `AddInstallationPushAddress`, with a working `Down`; no `EnsureCreated()` or
  `EnsureDeleted()` exists anywhere in the change (PC-2). The installation
  database is unchanged, so the two databases stay separate (AD-1).
- Configuration: one new mandatory installation setting with no default. A
  missing, empty or invalid value refuses startup and the refusal names the key
  and the rule, never the value — proved for eleven invalid values and separately
  for the log file (`InvalidAddress_IsNotWrittenToTheLog`). No connection string,
  password or secret was added or committed.

## 12. Logging, Audit and Telemetry

**Audit (SC-11).** `AuditEvent.InstallationPushAddressChanged` is built through
the existing `OwnerActsOnInstallation` factory, so a row can carry only the
Owner's internal id, the installation's internal id, the action code, outcome
`succeeded`, the UTC time and the request id. Verified against the stored rows:
one row per set, change and clear; the row and the change commit together; no
row for a refused or an unchanged submission; registration with an address
writes only `installation_created`; the row contains no address; and the rows
remain non-updatable.

**Logs (SC-10).** Events 5011 … 5017 (Control Plane) and 5111 … 5114
(installation) carry internal identifiers, attempt numbers, categories and
status codes only. Three independent tests search the whole log output for the
address, the domain and the client ID and find none, and the rejected push body
and the refused push address are searched for by literal value and are absent.

One real defect was found by the implementation and is fixed:
`IHttpClientFactory` writes `Start processing HTTP request POST {Uri}` at
`Information`, which put the school's push address into the Control Plane log.
The named client is now registered with `RemoveAllLoggers()`. This review
confirms the address no longer appears, and records F-1 below as defence in
depth for the next outbound client.

## 13. Dependencies

No NuGet package was added, removed or upgraded — no `.csproj` is modified. No
project reference changed; `ClassroomAgent.ControlPlane` still references only
`ClassroomAgent.Contracts` (SC-12, AD-1). `IHttpClientFactory` and
`RemoveAllLoggers()` come from the `Microsoft.AspNetCore.App` shared framework,
as interpretation I-13 requires. `dotnet list package --vulnerable` reports no
vulnerable package in any of the seven projects; this is NuGet's advisory
database at review time and not a guarantee of absence.

## 14. Security Test Coverage

| Security requirement | Test | Status |
|---|---|---|
| S-01 receiver is the only anonymous, exempt, unsafe endpoint | `InstallationEndpointTests` (2 enumeration cases) | PASS |
| S-02 private port only; public port `404` incl. forged `Host` | `StatusPushEndpointTests` (3 cases) | PASS |
| S-03 private port bound to the configured address, `*` explicit, no default | `PrivateAddressConfigurationTests` (19 cases) | PASS for the setting; the binding itself is section 21 |
| S-04 a push never sets a status | `ThePushItselfWritesNothing`, `UnknownPropertiesInThePush_AreIgnored` | PASS |
| S-05 at most one push check a minute, never concurrent | `PushCheckCoordinationTests` (9 cases) | PASS |
| S-06 another installation's id → `404`, nothing happens | `PushForAnotherInstallation_IsNotFound_AndStartsNoCheck` | PASS |
| S-07 body validated and size-limited, never logged or echoed | `MalformedPush_…` (7), `OversizedPush_…`, `StatusPushLoggingTests` (3) | PASS |
| S-08 no teaching data in `Contracts`; no `Domain` reference | contract type read; `.csproj` read | PASS |
| S-09 push only to the stored address | `PushesToDifferentSchools_AreIndependent`, `WithoutAPushAddress_NothingIsSent` | PASS |
| S-10 no redirects, no cookies, body never read | `UnexpectedStatus_…(Found)`, header assertions | PASS |
| S-11 push address pages Owner-only with a token | `PushAddressSecurityTests` (7 cases) | PASS |
| S-12 address changes audited without the address; delivery not audited | `InstallationPushAddressAuditTests` (8), `DeliveryIsNotAudited_…` | PASS |
| S-13 logs carry ids, attempts, categories, status codes only | `StatusPushLoggingTests` both hosts (12 cases) | PASS |
| S-14 the address is HTML-encoded wherever shown | `RefusedForm_EncodesTheTypedValue`, detail page cases | PASS |
| S-15 DTOs only; no `DbContext` in presentation | code read; `InstallationDetailDto` | PASS |
| S-16 no secret in configuration or source | `git status`, configuration read | PASS |

The tests exercise the real `StatusPushClient`: the stub replaces the **primary
HTTP handler** of the named client, not the client itself, so the URI
construction, the 10-second timeout, the classification table and the "body never
read" behaviour all run as written. This closes the concern the test strategy §6
recorded about the client being untested.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| An attacker on the school's public interface posts a push | never reaches the receiver | public port → `404` before routing; forged `Host` and `X-Forwarded-Host` also `404` | PASS |
| An attacker who reaches the private port pushes another school's id | `404`, nothing happens, id not logged | `PushForAnotherInstallation_…`; the log carries no id | PASS |
| An attacker floods the private port with valid pushes | at most one check a minute, at most one pending, `202` throughout | `ManyPushesWithinAMinute_LeaveOnlyOnePendingCheck`, `PushedChecksNeverRunConcurrently` | PASS |
| A push body claims a status | ignored; the status comes from the check | `UnknownPropertiesInThePush_AreIgnored`, `ThePushItselfWritesNothing` | PASS |
| A push body is huge or malformed | `413`/`400`, no check, body never logged | 9 cases | PASS |
| A suspended school is pushed | accepted; the check runs and may release read-only mode | `InReadOnlyMode_ThePushIsAccepted_AndTheCheckWritesTheState` | PASS |
| A signed-in non-Owner changes a push address | `403`, nothing changed | `WithoutTheOwnerRole_ThePostIsForbidden_AndChangesNothing` | PASS |
| A cross-site form posts a push address | `400` page-expired, nothing changed | `WithoutAnAntiforgeryToken_ThePostIsRefused_AndChangesNothing` | PASS |
| A stored XSS payload is entered as an address | refused by VR-001, never stored, encoded when echoed | `RefusedForm_EncodesTheTypedValue` | PASS |
| The Owner points a push address at an internal service | narrow by design — see F-3 | validation, fixed body, no redirects, body never read | Informational |

Rate limiting beyond the one-minute rule is not required by any approved
artifact and was not invented as a requirement.

## 16. Repository Hygiene

`git status` over the whole change set shows only source, test, documentation and
migration files. No generated database file, no `.xlsx` export, no `.env`, no
key or token, no connection string with a password. `google_credentials.json`
and `dac-classroom-agent-*.json` remain untracked and were not opened. No
`.csproj` or IDE-local configuration file is in the change set.

## 17. Deviations

No deviation from the approved security requirements was found. Three deviations
from other expectations are recorded in the Implementation Report §7 and were
checked here for security impact:

- the four corrected story-level test cases (the one-minute rule and the page
  language) — no security property is involved, and the corrections neither
  weaken an assertion nor remove a case;
- `InstallationSchemaTests` extended with the new column and constraint — a guard
  that grew with the schema, asserting exact type and nullability;
- `RemoveAllLoggers()` on the named push client — a security **fix**, reviewed in
  section 12 and followed up by F-1.

## 18. Findings

### F-1 — Minor — LOGGING (SC-10)

**File:** `src/ClassroomAgent.ControlPlane/Program.cs`

**Observed.** The push address was kept out of the log by removing the logging of
one named HTTP client (`RemoveAllLoggers()`). The Control Plane's Serilog
configuration has no `MinimumLevel.Override("System.Net.Http", …)`, unlike the
installation host, which sets it to `Warning`.

**Expected.** SC-10 binds every line the application writes. The current control
is correct but local: the next outbound client added to the Control Plane would
log its URI at `Information` by default, and only a test searching for the
literal value would catch it.

**Risk.** Low today — no other outbound client exists in the Control Plane.

**Correction.** Add `MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)`
to the Control Plane logger configuration, as the installation host already has,
so the protection is the host's default rather than one client's opt-out.

**Loop-back:** none required. **Verification:** the existing log-content tests
continue to pass.

### F-2 — Minor — INPUT_VALIDATION (SC-10, VR-002)

**File:** `src/ClassroomAgent.Web/Security/StatusPushEndpoint.cs`

**Observed.** When the request carries no `Content-Length`, the size limit is
re-checked as `body.Length > MaximumBodyBytes` — the length of the **decoded
string**, that is characters, compared against a limit expressed in bytes. The
reader caps at 4097 bytes, so the memory bound holds either way, but a body
between 4097 bytes and 4096 characters of multi-byte UTF-8 would be answered
`400` (or parsed) rather than `413`.

**Expected.** VR-002 and api-design §4 express the limit in bytes.

**Risk.** Very low: the intake is bounded at 4097 bytes regardless, so this
affects only which refusal an oversized body receives.

**Correction.** Have the reader return the number of bytes read alongside the
text and compare that against `MaximumBodyBytes`.

**Loop-back:** none required. **Verification:** `OversizedPush_IsRefused_AndStartsNoCheck`
plus a case with a multi-byte body.

### F-3 — Informational — OUTBOUND_DATA (SC-9, SC-13)

The Owner enters the push address and the Control Plane sends an HTTP `POST` to
it, which is a request-forgery surface by design and by `trebovaniya.md` v76. It
was reviewed rather than accepted on trust, and the surface is narrow: VR-001
permits `http://host:port` only — no path, query, fragment or user info — the
body is a fixed JSON object with one UUID, redirects are not followed, no cookie
or credential is sent, and the response status is the only thing read, never the
body. So an address aimed at an internal service can cause one bounded `POST`
and can return nothing to the Owner: there is no read-back channel. The Owner is
the service operator and the only role that can set the value. No change is
recommended.

### F-4 — Informational — TEST_COVERAGE (SC-9, DC-2)

That Kestrel binds the private endpoint to `Hosting:PrivateAddress` alone is the
second of the two protections SC-9 requires and is not observable under
`TestServer`. Every invalid value is proved to refuse startup, which is the half
that code can enforce; the binding itself stays the DC-2 deployment check, as
specification §10 records. Recorded so it is not mistaken for a verified control.

## 19. Positive Controls

Independently observed and verified, not taken from the Implementation Report:

- The installation's routed endpoints enumerate to exactly three, all anonymous,
  with the receiver the only unsafe one and the only antiforgery-exempt one —
  read off the live `EndpointDataSource`.
- Two independent port controls, both on `Connection.LocalPort`, neither reading
  a header; a forged `Host` and `X-Forwarded-Host` do not reach the receiver.
- The receiver holds no repository and writes nothing; the stored
  `LegitimacyState` is byte-for-byte unchanged while a pushed check is in flight.
- A push body carrying a `status` property changes nothing.
- At most one check runs at a time; push-triggered checks start at most once a
  minute; any number of deferred pushes leave exactly one pending check.
- The push address is read inside the status-change transaction, so the address
  used is the one stored as the change commits.
- Audit rows for the address carry ids only and are written in the same
  transaction as the change; refused and unchanged submissions write nothing;
  delivery is never audited.
- No log line anywhere carries the push address, the domain, the client ID, a
  request body or a response body.
- The outbound client follows no redirects, sends no cookies or credentials and
  never reads a response body.
- The schema change is a migration with a working `Down`; the check constraint
  refuses eight non-canonical shapes by direct SQL; no unique index was added.
- No package, project reference or secret was added; no vulnerable package is
  reported.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 is RESOLVED and its
option 2 — one pending check — is what the implementation enforces.

## 21. Review Limitations

- **Kestrel address binding not observed.** `WebApplicationFactory` runs on
  `TestServer`, which has no listening socket, so "the private endpoint answers on
  the configured address only" could not be verified at runtime. Mitigated by the
  local-port filter and the public-port middleware, both verified, and assigned
  to the DC-2 deployment check. See F-4.
- **No network-level verification.** That the Control Plane and the private port
  are unreachable from the public internet is a deployment property (SC-9, DC-6),
  not something this review can observe.
- **Vulnerability data is a point-in-time snapshot** from NuGet's advisory
  database at review time; it is not proof that the dependencies are free of
  unknown vulnerabilities.
- **This is a code, configuration and test review**, not penetration testing and
  not a specialised security assessment.

## 22. Verdict Rationale

`PASS`. The Implementation Report records a green build and a green suite, and
both were re-run for this review with the same result: 1085 passed, 0 failed, 0
skipped. Every SC item the scope touches is `PASS`, with evidence that was
checked against the code, the live endpoint table, the live schema and the log
output rather than taken from the report. No Critical and no Major finding was
identified, no security-sensitive Open Decision is unresolved, and the two new
security surfaces — one anonymous endpoint and one outbound flow — were both
already on the closed lists of `security-conventions.md`, so no list was widened.

The two Minor findings are hardening, not defects in an approved requirement:
F-1 makes an existing correct control a host default instead of one client's
opt-out, and F-2 makes a size limit exact in the unit it is specified in. Neither
changes what an attacker can reach, so neither blocks the commit; both are worth
folding into the next Story that touches those files.

The workflow may proceed to `HUMAN_PR_APPROVAL`.
