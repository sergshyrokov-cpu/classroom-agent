---
artifact_type: security_review
story: US-005
version: 1
status: APPROVED
created_at: 2026-09-17T15:02:33Z
updated_at: 2026-09-17T15:02:33Z
produced_by: security-reviewer
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
  - path: docs/tests/US-005-test-strategy.md
    version: 1
  - path: docs/tests/US-005-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-005-implementation-report.md
    version: 1
  - path: trebovaniya.md
    version: 73
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 1
informational_findings: 5
security_sensitive: true
runtime_checks: PARTIAL
---

# US-005 Security Review — Installation legitimacy check and grace period

## 1. Executive Summary

**Result: PASS.** No Critical or Major finding.

The Story opens the service channel in both directions of trust: an anonymous, antiforgery-exempt `POST
/service/v1/legitimacy-checks` in the Control Plane, and the first installation host with anonymous liveness
and readiness on a private port. Verified controls: the endpoint is the only addition to both SC-4 closed
lists and the enumeration tests enforce it; an unknown id or invalid body reveals nothing and records nothing;
the installation validates the Control Plane answer before use (VR-003), has no certificate bypass, follows no
redirect and keeps no cookie; logs carry ids, versions, categories and states only; the public port answers
`404` to everything regardless of `Host` / `X-Forwarded-*`; the contract carries no teaching data and the
Control Plane references only `Contracts`.

Principal residual risk (SEC-001, Minor): the private Kestrel endpoint is bound to all interfaces
(`http://*:{PrivatePort}`), while DC-6 says "bound only to the private network interface". No approved
artifact defines a bind-address setting, so IMPLEMENTATION cannot fix it without inventing configuration. In
this Story the port exposes only a state word; with US-006 it will carry the status push receiver, where the
same binding would let a firewall mistake lift a suspension. A human decision is needed **before US-006**.

Limitations: code, configuration and test review plus a full local test run; no penetration test; certificate
validation and the production handler's redirect setting are not covered by automated tests (test strategy
§6), only by code inspection.

## 2. Reviewed Artifacts

| Artifact | Path | Version / status |
|---|---|---|
| Story | `docs/stories/US-005-installation-legitimacy-check.md` | authored |
| Specification | `docs/specifications/US-005-spec.md` | v1 APPROVED |
| Open Decisions | `docs/decisions/US-005-open-decisions.md` | v1 APPROVED (OD-001, OD-002 resolved) |
| API design / OpenAPI | `docs/designs/api/US-005-api-design.md`, `US-005-openapi.yaml` | v1 |
| DB design / entity model | `docs/designs/database/US-005-db-design.md`, `US-005-entity-model.md` | v1 |
| Test strategy / AC matrix | `docs/tests/US-005-test-strategy.md`, `US-005-ac-test-matrix.md` | v1 |
| Implementation report | `docs/evidence/US-005-implementation-report.md` | v1, PASS |
| Requirements | `trebovaniya.md` | v73 |

No input is `SUPERSEDED`; HUMAN_SPEC_APPROVAL recorded 2026-09-17T13:29:44Z.

## 3. Security-Relevant Scope

**Exposed functionality**

| Host | Endpoint | Access |
|---|---|---|
| Control Plane | `POST /service/v1/legitimacy-checks` | anonymous, antiforgery-exempt (SC-4 "Legitimacy check") |
| Control Plane | `GET /installations/{id}` detail page — new "last check" section | Owner only (unchanged) |
| Installation, private port | `GET /health/live`, `GET /health/ready` | anonymous (SC-4 "Liveness and readiness") |
| Installation, public port | nothing mapped; every request `404` | — (I-13) |

**Assets:** `Installation` status (the lever that suspends a school), domain and client ID;
`InstanceLicenseCheck`; `LegitimacyState` (decides read-only mode); installation database connection string.

**Trust boundaries:** installation → Control Plane channel (HTTPS, private network); Control Plane host →
private network callers; installation private port → Owner's network; public port → internet;
`Application` → `IControlPlaneClient` / repository ports; both apps → their own PostgreSQL.

**Security components touched:** `GlobalAntiforgeryFilter`, `ControlPlaneExceptionHandler`,
`LegitimacyCheckController`, `LegitimacyCheckService`, `ControlPlaneClient`, `PrivatePortEndpointFilter`,
`PublicPortMiddleware`, `InstallationSettingsReader`, `InstallationLogging`, `LegitimacyCheckBackgroundService`.

## 4. Environment and Tools

- .NET SDK 10; Docker available (Testcontainers `postgres:17-alpine`).
- `dotnet build ClassroomAgent.sln` → 0 warnings, 0 errors.
- `dotnet test --solution ClassroomAgent.sln --no-build` → **882 total, 882 passed, 0 failed, 0 skipped** (51 s),
  re-run independently by this review.
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` → no vulnerable packages in any
  project for the configured sources.
- Text searches of `src`/`tests` for certificate-validation callbacks, `EnsureCreated`/`EnsureDeleted`/
  `Migrate()`, HTTP types in `Application`, Google references in project files, and secret-like strings.
- `git check-ignore` for live credential files, `classroom_cache.db`, telemetry and log directories.
- Not performed: penetration test; runtime check of Kestrel interface binding on a real multi-homed host;
  TLS handshake against an untrusted certificate outside the test server.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or account added; detail page stays Owner-only |
| SC-2 Authentication | PASS (scoped) | no change to Owner auth; private port plain HTTP without HSTS/redirect (`HealthEndpointTests.PrivatePort_HasNoHstsOrHttpsRedirect`); public-port HTTPS/HSTS deferred to US-008 by approved I-13 (SEC-002) |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Admin login check arrives with US-008 |
| SC-4 Authorization | PASS | check controller `[AllowAnonymous]`, `[IgnoreAntiforgeryToken]`, `[HttpPost]` only; `AnonymousEndpointTests.OnlySc4EndpointsAllowAnonymous`, `AntiforgeryTests.OnlyTheLegitimacyCheckIsExemptFromAntiforgery` (exactly one exempt endpoint, POST); `GlobalAntiforgeryFilter` skips only endpoints carrying the attribute; installation routes enumerated by `InstallationEndpointTests.RoutedEndpoints_AreOnlyLivenessAndReadiness_GetAndAnonymous`; private-port filter by `LocalPort` with forged-header tests |
| SC-5 Read-only mode | PASS (scoped) | determination in `Application` (`GetLegitimacyModeQuery`); `LegitimacyState` write is BR-026; refusals are US-007 (spec §10) |
| SC-6 No DB UI | PASS | readiness/liveness body is the state word only (`Readiness_BodyIsTheStateWordOnly_NoReasonTimeOrVersion`); no diagnostic endpoint; no developer exception page in the installation |
| SC-7 Key | PASS | no key, key reference or Data Protection ring introduced; no `appsettings.json` with values added to `Web` |
| SC-8 Google | NOT_APPLICABLE | no Google package or call (`ProjectReferenceTests`) |
| SC-9 Channel | PASS with finding | HTTPS required for `ControlPlane:Address`; no certificate bypass (search: none); private port binding → SEC-001 |
| SC-10 Hygiene | PASS | setting errors name key + rule, never value (`InstallationSettingsReader`); channel `500` empty body; rejected body never logged; logs without domain/client ID/exception text (`Logs_CarryNoDomainClientIdOrExceptionTextFromTheCall`, `KnownCall_IsLoggedAtInformation_UnknownAtWarning_WithoutDomainClientIdOrBody`); `System.Net.Http` at Warning |
| SC-11 Audit | PASS | the check writes no audit row on either side (S-14, `trebovaniya.md` §5/§9 v73) — none found |
| SC-12 Owner | PASS | `Contracts` has only id, versions, status, compatibility, domain, client ID; `ControlPlane.csproj` references `Contracts` only; no statistics sent |
| SC-13 Outbound | PASS | the installation's only `HttpClient` has the configured Control Plane base address; redirects disabled, cookies disabled |

## 6. Authentication and Authorization

- Control Plane: the new endpoint is anonymous by SC-4 and exempt by the SC-4 exemption list; both lists are
  enforced by enumeration tests that fail on any other anonymous or exempt endpoint. Other methods on the path
  are not handled. The detail page keeps its Owner policy; `InstallationLastCheckTests` run with an Owner session.
- Installation: no authentication exists yet. Every request not on the private port is answered `404` by
  `PublicPortMiddleware` before routing — no cookie, no redirect, nothing read. On the private port only the two
  GET health routes exist, protected by the route-group filter on `Connection.LocalPort`.
- No identifier in a request gives access to another installation's data: an unknown id gets only
  `{"outcome":"unknown_installation"}`; a known id returns only that installation's status, domain and client
  ID, which is the approved content of the channel on a private network (SC-9, SC-12).

## 7. Credentials, Key and Google Access

- No password, token, key or key reference is added. The connection string is read from configuration only and
  never logged (`InstallationSettingException` carries the key name).
- No Google package, scope or call.
- TLS: `SocketsHttpHandler` with platform certificate validation; no `ServerCertificateCustomValidationCallback`
  anywhere in `src` or `tests`; `ControlPlaneClientTests.TlsFailure_IsUnreachable` shows a TLS failure is a
  failed check, not a bypass.

## 8. Sensitive Data Exposure

| Channel | Result |
|---|---|
| Check response | known: status, compatibility, domain, client ID only; unknown / invalid: outcome word only |
| Health responses | state word only |
| Detail page | versions and labels rendered by Razor (HTML-encoded); application version constrained by VR-002 and a DB check constraint |
| Logs — installation | categories, reasons, states, exception type name only |
| Logs — Control Plane | installation id, versions, status, compatibility; unknown id at Warning |
| Exceptions | channel `500` has an empty body; Control Plane logs the exception locally as before (US-001) |
| Audit | none written (by requirement) |
| DTOs | `InstallationLastCheckDto` in the view model; no entity in a controller signature |

## 9. Input Validation

- **Check request (Control Plane):** body read manually; non-JSON content type, malformed JSON, wrong types,
  missing fields → `400 invalid_request`; Data Annotations on `LegitimacyCheckInput` (`[Required]`,
  `[Range(1, 999999)]`, `[ApplicationVersion]` ≤ 20 chars, no leading zeros). Unknown fields ignored (DC-12).
  Tested in `LegitimacyCheckEndpointTests`.
- **Check answer (installation):** `ControlPlaneClient.ParseAnswer` requires every field, allowed enum values,
  domain 3–253, client ID 10–32 ASCII digits; anything else is `UnparseableAnswer` and nothing is stored.
  The database repeats the constraints (`ck_legitimacy_state_*`).
- **Configuration:** `InstallationSettingsReader` validates UUID, https address without user info / query /
  fragment, port range and clash with `urls`, non-empty connection string; the host refuses to start.

## 10. API Security

- Endpoints match `US-005-openapi.yaml`; no undocumented route (installation enumeration test; Control Plane
  enumeration tests).
- POST only; JSON only; response fields minimal.
- Error behaviour: `400`/`404` with an outcome word; `500` empty body under `/service`.
- The request body limit is Kestrel's default (SEC-005, Informational).

## 11. Persistence and Configuration

- `instance_license_check` (Control Plane DB): unique `installation_id`, FK RESTRICT, check constraints on
  version, contract version, status, compatibility; no sensitive column.
- `legitimacy_state` (installation DB): single-row guarantee (`singleton` unique + check), client ID and domain
  checks; no key or secret.
- Both schemas only via migrations (`AddInstanceLicenseCheck`, `InitialLegitimacyState`); no
  `EnsureCreated`/`EnsureDeleted`/`Migrate()` in `src`. Separate `DbContext`s and connection strings (AD-1).
- Configuration: required settings fail closed; no committed values. D-5: the port clash check reads `urls`
  only (SEC-003, Informational).

## 12. Logging, Audit and Telemetry

- Log messages are `LoggerMessage` templates with typed parameters; none takes a domain, client ID, body or
  exception message. Startup refusal writes key + rule. Tests assert the absence of domain, client ID and
  exception text in captured events.
- No audit (required).
- `docs/hooks/tool-usage.jsonl` is git-ignored.

## 13. Dependencies

- Added exactly as OD-001: `ClassroomAgent.Web` → `Microsoft.EntityFrameworkCore.Design` 10.0.4
  (`PrivateAssets=all`), `Serilog.AspNetCore` 10.0.0, `Serilog.Sinks.File` 7.0.0; skeleton projects per OD-002.
- Project references match `package-map.md` (`ProjectReferenceTests`): no `Application → Infrastructure`,
  `ControlPlane → Contracts` only.
- Vulnerability scan (NuGet advisory data, transitive included): no vulnerable packages.

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| S-01 anonymous/exempt lists | `AnonymousEndpointTests`, `AntiforgeryTests`, `LegitimacyCheckEndpointTests` |
| S-02 private port, forged headers | `HealthEndpointTests.HealthPaths_OnPublicPort_*`, `InstallationEndpointTests` |
| S-03 contract / references | `ProjectReferenceTests`, `LegitimacyChannelContractTests` |
| S-04 outbound only to Control Plane | `ControlPlaneClientTests.PostsContractRequest_ToTheCheckPath_AsJson`; code inspection of DI |
| S-05 HTTPS + certificate | `InstallationConfigurationTests` (http refused); `TlsFailure_IsUnreachable`; bypass absent by search |
| S-06 unknown/invalid reveal nothing | `LegitimacyCheckEndpointTests`, `InstanceLicenseCheckRecordTests` |
| S-07 answer validated | `ControlPlaneClientTests.Status200_WithInvalidBody_IsUnparseableAnswer` |
| S-08 log hygiene | `LegitimacyLoggingTests`, `InstanceLicenseCheckRecordTests` |
| S-09 state only | `Readiness_BodyIsTheStateWordOnly_NoReasonTimeOrVersion` |
| S-10 config errors | `InstallationConfigurationTests` |
| S-12 encoding | Razor default encoding; VR-002 limits the value |
| S-14 no audit | code inspection (no audit write path in the service) |

No test calls a live Control Plane or Google; fixtures are synthetic (TC-4).

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Internet client calls `/health/ready` or a future private path on the public port, forging `Host`/`X-Forwarded-Port` | `404` by local port | forged-header tests | PASS |
| Internet client reaches the private port directly | network isolation + bind only to private interface (DC-6) | binds `*`; relies on firewall | SEC-001 |
| Caller enumerates installation ids on the Control Plane | UUID space; private network; nothing revealed for unknown ids | endpoint tests | PASS |
| Caller posts a malformed or oversized body to spoil `InstanceLicenseCheck` | `400`, nothing recorded; length limits | endpoint + schema tests | PASS |
| Cross-site POST from the Owner's browser to the check endpoint | no cookie-based effect: anonymous, no Owner state changes, records only a version of a known id | design (SC-4 exemption) | PASS |
| Man-in-the-middle / rogue Control Plane answers "active" or a different domain | HTTPS with validated certificate; answer validated | client code + TLS test | PASS |
| Control Plane redirects the installation elsewhere | redirects not followed → error answer | `ControlPlaneBeforeSetup_RedirectIsAnErrorAnswer` | PASS |
| Remote error text leaks into installation logs | category only | logging tests | PASS |

## 16. Repository Hygiene

- `google_credentials.json`, `dac-classroom-agent-*.json`, `classroom_cache.db`, `logs/`,
  `docs/hooks/tool-usage.jsonl` are git-ignored (verified with `git check-ignore`). Credential files not opened.
- Change set contains no database file, `.xlsx`, `.env`, certificate, key or IDE-local config; secret-pattern
  search of `src` and `tests` found nothing.
- Tracked spreadsheets: only `docs/product/report-templates/` (allowed).

## 17. Deviations

| Implementation note | Security assessment |
|---|---|
| D-1 test clock 1 h → 10 min (human-approved) | keeps the 30-minute Owner idle timeout intact — no weakening |
| D-2 explicit `singleton = true` | schema unchanged; no impact |
| D-3 no singleton retry | conflict → `SaveFailed`, state unchanged; fail-safe |
| D-4 private endpoint `http://*:{port}` | deviation from DC-6 wording → SEC-001 |
| D-5 port clash vs `urls` only | SEC-003 |
| D-6 `UnexpectedError`, `LegitimacyCheckException` (type name only) | improves hygiene |
| D-9 antiforgery filter honours `[IgnoreAntiforgeryToken]` | generic mechanism, bounded by the exemption enumeration test — a new attribute anywhere fails the build's tests |

## 18. Findings

### SEC-001 — Private endpoint bound to all interfaces (Minor, CONFIGURATION, SC-9 / DC-6)

- **Affected:** `src/ClassroomAgent.Web/Program.cs` (`UseUrls(..., "http://*:{PrivatePort}")`).
- **Observed:** the private Kestrel endpoint listens on every interface. DC-6: "a second Kestrel endpoint bound
  only to the private network interface". No approved artifact (spec FR-001, api-design §10) defines a
  bind-address setting.
- **Expected:** the private endpoint is not reachable from a public interface even if the host firewall is
  misconfigured.
- **Risk now:** low — only `Healthy`/`Degraded`/`Unhealthy` would be exposed (DC-11 says a publicly reachable
  state endpoint contradicts SC-6). **Risk from US-006:** high — the push receiver on the same port could lift a
  suspension or force read-only mode.
- **Required correction:** not by IMPLEMENTATION alone (a new configuration key would be invented). A human
  decides, before US-006 is specified, whether to add a private bind-address setting (e.g. through a new
  `trebovaniya.md` §7 question or an Open Decision of US-006) or to record that the deployment firewall alone
  satisfies DC-6.
- **Loop-back:** none for US-005 (non-blocking).
- **Verification after correction:** a test that the private endpoint URL uses the configured address; a
  deployment check that the private port refuses connections on the public interface.

### SEC-002 — Installation public-port baseline deferred (Informational, CONFIGURATION, SC-2 / SC-4)

No fallback authorization policy, antiforgery, HTTPS redirection or HSTS on the public port yet. Accepted by
approved interpretation I-13: nothing is mapped and every request is `404`
(`PublicPort_AnyRequest_Returns404EmptyBody_NoCookieNoRedirect`). Must arrive with US-008 before any page is
mapped.

### SEC-003 — Port clash check reads `urls` only (Informational, CONFIGURATION)

Ports from `http_ports`/`https_ports` are overridden by `UseUrls`; a `Kestrel:Endpoints` section would override
the private endpoint instead (health unreachable — fails closed). No exposure.

### SEC-004 — Response body read without a size cap (Informational, INPUT_VALIDATION)

`ControlPlaneClient` buffers the whole answer (`ReadAsByteArrayAsync`) under the 30-second limit. The peer is the
HTTPS-validated Control Plane; a cap would be defence in depth only.

### SEC-005 — Default request body limit on the check endpoint (Informational, API_SECURITY)

The endpoint accepts Kestrel's default body size (~30 MB) before validation rejects it. Reachable only from the
private network; no rate limiting is required by approved artifacts. A small `[RequestSizeLimit]` could be
considered in a later Story.

### SEC-006 — Certificate validation and redirect setting not test-covered (Informational, TEST_COVERAGE)

Excluded by the approved test strategy §6; verified here by code inspection (no validation callback; handler
with `AllowAutoRedirect = false`, `UseCookies = false`).

## 19. Positive Controls

- SC-4 anonymous and exemption lists enforced by enumeration tests on the Control Plane; installation route
  enumeration.
- Local-port filter and public-port middleware independent of forgeable headers, tested with forged headers.
- Fail-closed startup on invalid configuration without echoing values.
- HTTPS-only Control Plane address; platform certificate validation; no redirects, no cookies.
- Validation of the external answer before persistence, repeated by database constraints.
- Unknown / invalid calls reveal and record nothing.
- Log hygiene asserted by tests; exception type name only.
- Contract and project-reference boundaries (SC-12) asserted by tests.
- Migrations only; separate databases.

## 20. Open Decisions

No blocking security Open Decisions were identified. SEC-001 needs a human decision before US-006 (see §18).

## 21. Review Limitations

- No penetration test; no runtime check of interface binding on a multi-homed host.
- TLS behaviour against a real untrusted certificate outside the test server not exercised.
- Deployment firewall configuration is outside the repository and not verified.
- Vulnerability scan relies on the NuGet advisory database available at review time.

## 22. Verdict Rationale

The implementation report's green build and tests were reproduced (0 warnings; 882/882). Every SC item the
Story touches is PASS; no Critical or Major finding; required security tests exist and pass; no blocking Open
Decision. SEC-001 is a deviation from DC-6 wording with low impact in this Story and an upstream (not
implementation) remedy; it is carried as a non-blocking finding that must be decided before US-006.
Verdict: **PASS** → `HUMAN_PR_APPROVAL`.

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-005
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-005-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "SEC-001 (Minor): private Kestrel endpoint binds http://*:{PrivatePort}; DC-6 requires binding to the private interface only; no approved bind-address setting exists — human decision needed before US-006 (push receiver)."
    - "SEC-002 (Info): installation public port has no auth/antiforgery/HTTPS/HSTS baseline yet — accepted by I-13, due with US-008."
    - "SEC-003 (Info): port clash check reads `urls` only; other endpoint sources fail closed."
    - "SEC-004 (Info): ControlPlaneClient buffers the answer without a size cap (trusted HTTPS peer, 30 s limit)."
    - "SEC-005 (Info): check endpoint uses Kestrel's default request body limit."
    - "SEC-006 (Info): certificate validation and redirect setting verified by inspection, not by tests (test strategy §6)."
```
