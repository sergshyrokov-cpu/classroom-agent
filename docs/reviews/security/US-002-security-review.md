---
artifact_type: security_review
story: US-002
version: 1
status: APPROVED
created_at: 2026-09-17T08:43:03Z
updated_at: 2026-09-17T08:43:03Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-002-register-installation.md
    version: null
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/decisions/US-002-open-decisions.md
    version: 2
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
  - path: docs/designs/database/US-002-db-design.md
    version: 1
  - path: docs/designs/database/US-002-entity-model.md
    version: 1
  - path: docs/tests/US-002-test-strategy.md
    version: 1
  - path: docs/tests/US-002-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-002-implementation-report.md
    version: 1
  - path: trebovaniya.md
    version: 69
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 1
informational_findings: 2
security_sensitive: true
runtime_checks: PARTIAL
---

# US-002 Security Review — Register an Installation

## 1. Executive Summary

**Result: PASS.** No Critical or Major finding.

The Story adds five Owner-only routes to the Control Plane (list, registration,
detail, name, client ID) and one anonymous static script, plus the
`installation` table. Principal controls, each verified in code and by passing
tests: the Owner policy on the controller behind the host's deny-by-default
fallback policy; the global antiforgery filter on every POST; server-side
validation with a first-failing-rule per field; database unique indexes and
check constraints; immutability and no-delete triggers; audit rows written in
the same transaction as the change, with internal ids only; Razor HTML encoding
and no inline script.

One Minor finding: the lost-race registration path logs an EF Core error whose
content was checked statically only. Two informational notes. Limitations: no
penetration test; the reviewer runs in the same session as the implementor
(§21).

Next: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

As listed in `inputs`. `HUMAN_SPEC_APPROVAL` recorded 2026-09-16T13:45:30Z. No
input is `SUPERSEDED`. The implementation report v1 records concrete evidence:
build 0 warnings/0 errors, 359/359 tests, `dotnet format` exit 0.

## 3. Security-Relevant Scope

| Host | Exposed | Access |
|---|---|---|
| Control Plane (private network, HTTPS) | `GET /installations`, `GET /installations/new`, `POST /installations`, `GET /installations/{id:guid}`, `GET|POST /installations/{id:guid}/name`, `GET|POST /installations/{id:guid}/client-id` | Owner policy |
| Control Plane | `GET /` (link added) | Owner policy (unchanged) |
| Control Plane | `/js/copy-identifier.js` | anonymous static file (SC-4 "Static files") |
| Installation (`Web`) | nothing changed | — |

**Assets:** `Installation` records (the domain each school may work with — the
source of truth for BR-020), the installation UUID, `AuditEvent` rows, the Owner
session. No student or staff personal data, no password, no service-account
key are touched.

**Trust boundaries:** Owner browser → Control Plane (form posts);
`Controllers` → `Services` (DTOs only); `Services` → PostgreSQL (constraints,
triggers).

**Components changed:** `Controllers/Installations*`, request types and
validation attributes, `Services/InstallationRegistry` and result/DTO types,
`Persistence/Installation*`, audit vocabulary, `TimestampInterceptor`,
migration `AddInstallation`, views, `wwwroot/js/copy-identifier.js`,
translations, DI registration.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 (Testcontainers PostgreSQL).
- `dotnet list ClassroomAgent.sln package --vulnerable` — no vulnerable packages
  reported for `ClassroomAgent.ControlPlane` and `ClassroomAgent.Tests` against
  nuget.org.
- `dotnet test --project tests/ClassroomAgent.Tests --no-build --filter-method "*ConcurrentRegistrations*"`
  — 2/2 pass (both concurrent races re-run by the reviewer).
- Full-suite evidence taken from the implementation report (359/359) and not
  re-run in full here.
- Static review: code reading and searches over `src/ClassroomAgent.ControlPlane`
  (`Html.Raw`, `AllowAnonymous`, `IgnoreAntiforgery`, `HttpClient`,
  `ExecuteDelete`/`ExecuteUpdate`/`Remove(`, logging calls, `EnsureCreated`,
  `innerHTML`), `.gitignore`, `git status`.
- Not performed: penetration testing; reading a log file produced by a lost
  race (the test host deletes its log directory on disposal).

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role added; the controller uses the existing `Owner` policy. |
| SC-2 Authentication | NOT_APPLICABLE | Cookies, sign-in, password handling unchanged. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | US-003. |
| SC-4 Authorization | PASS | `[Authorize(Policy = OwnerSession.OwnerPolicy)]` on `InstallationsController`; fallback policy unchanged; no new `[AllowAnonymous]` (search: only Setup, SignIn, Error, fallback); no antiforgery exemption; the only anonymous addition is a static file from `wwwroot`. Tests: `InstallationAuthorizationTests` (anonymous → `302 /sign-in`, before setup → `302 /setup`, role-less principal → `403`, enumeration), `AnonymousEndpointTests`, `InstallationAntiforgeryTests`, `AntiforgeryTests`. All state changes are POST; GETs only read (`GetRequestsWithFormValues_ChangeNothing`). |
| SC-5 Read-only mode | NOT_APPLICABLE | Read-only mode applies to installations, not the Control Plane. |
| SC-6 No DB UI | PASS | No diagnostic endpoint added. |
| SC-7 Key | PASS | `installation` has exactly the 8 designed columns — no key, key reference or secret (`InstallationColumns_MatchDesign_NoKeyOrSecretColumn`). The client ID is a public identifier (glossary). |
| SC-8 Google | NOT_APPLICABLE | No Google call. |
| SC-9 Channel | NOT_APPLICABLE | US-005. Domain uniqueness (the precondition for SC-9's domain check) is enforced by `uq_installation_domain` and `ck_installation_domain_lower`. |
| SC-10 Hygiene | PASS (see MIN-1) | No new log statement in the Story's code. Validation and conflict errors are translation keys; `404` uses the host error page. `RejectedAndStoredValues_NeverReachTheLogFile` passes. Lost-race path reviewed statically (MIN-1). |
| SC-11 Audit | PASS | The three v69 Control Plane events (`installation_created`, `installation_renamed`, `installation_client_id_changed`) written via typed factories in the same transaction; internal ids only; no row for refusals. Tests: `InstallationAuditTests` (5), incl. JSON of every row without name/domain/client ID/UUID. The US-001 `audit_event` immutability trigger and interceptor guard are unchanged. |
| SC-12 Owner | PASS | No teaching data; no `Contracts` change; `ControlPlaneReferenceTests` passes. |
| SC-13 Outbound | PASS | No `HttpClient` or outbound call; the client ID and domain are not verified externally (spec S-12). |

## 6. Authentication and Authorization

- **Requirement:** every US-002 endpoint Owner-only (FR-010, S-01).
- **Implementation:** class-level policy; the `SetupGateMiddleware` runs before
  authentication and applies to the new endpoints because they are matched
  routes.
- **Non-GUID `{id}`:** matches no route; the anonymous catch-all answers `404`
  for anyone, revealing nothing about installations. A GUID path without a
  session gets `302 /sign-in` before any lookup, so existence is not disclosed to
  an anonymous caller.
- **IDOR:** the Control Plane has one Owner; every installation belongs to that
  Owner. Not applicable.
- **Actor id:** taken from the session principal (`OwnerSession.OwnerId`), never
  from the form.
- **Tests:** allowed role (5 pages `200`) and forbidden cases (8 operations,
  anonymous and role-less) per TC-5 — pass.

## 7. Credentials, Key and Google Access

Not touched. No password, token or key handling in the change set. No Google
scope or port.

## 8. Sensitive Data Exposure

- **Views:** receive `InstallationListItemDto` / `InstallationDetailDto` and page
  models; neither exposes the internal `id` or `updated_at`. Entities never reach
  a view (AD-8). No `Html.Raw`; `EnteredValues_AreHtmlEncoded_OnDetailListAndForms`
  passes with markup in a name.
- **Conflict messages:** name no other installation (I-8); tests assert the other
  name is absent.
- **Logs:** see §12 and MIN-1.
- **Audit rows:** no entered value or UUID (tested).
- **Exceptions:** unhandled exceptions go to the existing handler and the
  generic `500` page.
- **Exports, telemetry:** none added.
- **Test fixtures:** synthetic (`*.example.test`, fake client IDs) — TC-4.

## 9. Input Validation

- **Binding:** `[FromForm]` request classes with only the designed properties;
  posted `status`, `identifier`, `id`, `createdAt` are ignored
  (`PostedStatusIdentifierAndCreationTime_AreIgnored`).
- **Rules:** `InstallationNameAttribute` (≤ 200 code points, edge whitespace,
  `Cc`/`Cf`), `InstallationDomainAttribute` (length, ASCII letters/digits/`.`/`-`,
  dot, labels, `xn--`), `InstallationClientIdAttribute` (`char.IsAsciiDigit`,
  10–32). Server-side; `ModelState` is checked before any service call. 69
  negative/boundary cases pass, including astral characters, NBSP, ZWSP, RLM and
  Arabic-Indic digits.
- **Defence in depth:** database checks restate the stored-shape rules; every
  value accepted by validation also satisfies them (domain lower-cased with
  `ToLowerInvariant` over ASCII; code-point length equals PostgreSQL
  `char_length`), so valid input cannot turn into a `500`.
- **Order:** existence is checked before validation on edit posts (`404` before
  `400`), as the API design requires.
- **Oversized input:** limited by the host's default form limits; field lengths
  are rejected at validation before persistence.

## 10. API Security

- Only the approved routes exist (`InstallationEndpoints_ExistAndNoneAllowsAnonymous`:
  exactly five patterns, no PUT/PATCH/DELETE; PUT/PATCH/DELETE answer ≥ `400` and
  change nothing).
- Status codes match the contract: `400` validation, `409` conflict (pre-check and
  lost race — both race tests pass), `404` unknown, `302` success/unchanged.
- Redirect targets are built from the server-side GUID, never from input (no open
  redirect).
- No `/api/v1` endpoint; no JSON body.

## 11. Persistence and Configuration

- Migration `AddInstallation` only (no `EnsureCreated`); model drift test passes.
- Constraints: `uq_installation_identifier`, `uq_installation_domain`,
  `uq_installation_client_id`, five `ck_installation_*` checks — tested against
  the real database.
- Triggers `trg_installation_immutable_columns` (identifier, domain) and
  `trg_installation_no_delete` — tested; no delete path in code (search found no
  `Remove(`/`ExecuteDelete`).
- Separate Control Plane database (AD-1) unchanged.
- No configuration change; no connection string or secret added.

## 12. Logging, Audit and Telemetry

- **Logging:** the Story's code adds no log statement. The pre-check conflict and
  validation paths log nothing (tested). The lost-race path lets EF Core log its
  failed save (`Microsoft.EntityFrameworkCore` at `Error`, above the host's
  `Warning` override). Static analysis: sensitive-data logging is not enabled, so
  SQL parameters are logged as placeholders; no connection string sets
  `Include Error Detail`, so Npgsql redacts the PostgreSQL `DETAIL` that would
  carry the duplicate key value; the message names only the constraint. Not
  confirmed at runtime → MIN-1.
- **Audit:** complete for the three v69 events (§5 SC-11).
- **Hook telemetry:** `docs/hooks/tool-usage.jsonl` remains git-ignored; not
  changed by the Story.

## 13. Dependencies

No package or project reference added (the `.csproj` files are unchanged).
Vulnerability scan: none reported (§4).

## 14. Security Test Coverage

| Requirement | Tests | Status |
|---|---|---|
| S-01 Owner only, not anonymous | `InstallationAuthorizationTests` (30), `AnonymousEndpointTests` | PASS |
| S-02 POST + antiforgery; GET safe | `InstallationAntiforgeryTests` (8), `AntiforgeryTests` | PASS |
| S-03 server-generated UUIDv4 | `ValidRegistration_GeneratesDistinctVersion4Identifiers`, over-posting test | PASS |
| S-04 uniqueness under concurrency | `InstallationUniquenessTests` (incl. 2 races), schema tests | PASS |
| S-05 no domain change, no delete | detail page test, trigger tests | PASS |
| S-06 no secret column | `InstallationColumns_MatchDesign_NoKeyOrSecretColumn` | PASS |
| S-07 audit | `InstallationAuditTests` (5) | PASS |
| S-08 no values in logs | `RejectedAndStoredValues_NeverReachTheLogFile` (pre-check path) | PASS; race path MIN-1 |
| S-09 output encoding, no inline script | `EnteredValues_AreHtmlEncoded_OnDetailListAndForms`, `Detail_NoInlineScriptOrInlineEventHandler` | PASS |
| S-10 DTOs only | code review | PASS |
| S-11 no teaching data / Domain reference | `ControlPlaneReferenceTests` | PASS |
| S-12 no outbound call | code search | PASS |
| S-13 no internals, no `500` on unique violation | race tests (`409`), error page tests | PASS |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Anonymous POST registering a domain | `302 /sign-in`, nothing stored | `Anonymous_WithOwnerAccount_RedirectsToSignIn_ChangesNothing` | PASS |
| Cross-site form post from the Owner's browser | antiforgery `400` | `Register_WithoutToken…`, `EditForm_WithTokenFromAnotherSession…` | PASS |
| Registering a second installation for an existing domain (other case) to widen a school's reach | `409`, no row | uniqueness tests, `ck_installation_domain_lower` | PASS |
| Racing two registrations for one domain | one row, other `409` | race tests | PASS |
| Changing a domain or deleting a record via a crafted request or SQL path | no route; triggers refuse | enumeration, PUT/PATCH/DELETE test, trigger tests | PASS |
| Posting `status=suspended` / a chosen identifier | ignored | over-posting test | PASS |
| Stored XSS through the school name | encoded on list, detail, form | encoding test | PASS |
| Probing for existence with random UUIDs anonymously | `302 /sign-in`, no lookup | authorization tests | PASS |

## 16. Repository Hygiene

- `git status`: only Story code, tests, docs and workflow files; no `.db`,
  `.xlsx`, `.env`, key or credential file in the change set.
- `google_credentials.json`, `dac-classroom-agent-*.json`, `classroom_cache.db`,
  `*.xlsx` (except report templates) and `tool-usage.jsonl` are git-ignored
  (checked in `.gitignore`; the files were not opened).
- `TestResults/` is ignored.

## 17. Deviations

None from the approved security requirements. The implementation report's
claims were checked against the code and, for concurrency, re-run.

## 18. Findings

### MIN-1 — Lost-race log content not verified at runtime

- **Severity:** Minor. **Category:** LOGGING. **SC:** SC-10.
- **Affected:** `src/ClassroomAgent.ControlPlane/Services/InstallationRegistry.cs`
  (`RegisterAsync`, `ChangeClientIdAsync` catch paths); test coverage in
  `InstallationValidationTests.RejectedAndStoredValues_NeverReachTheLogFile`.
- **Evidence:** on a unique violation during save, EF Core logs the failure at
  `Error`. Static analysis shows no value in that entry (placeholders; Npgsql
  detail redacted; no `EnableSensitiveDataLogging`, no `Include Error Detail`).
  No test produces a race and then reads the log.
- **Expected:** no domain or client ID in any log line (FR-014, SC-10).
- **Risk:** low — a future configuration change (for example enabling Npgsql
  error detail for debugging) would put the duplicate domain or client ID into
  the log without any test failing.
- **Correction (recommended, not required for this Story):** a test that runs a
  lost race and reads the log file, or a note in DC-10 that `Include Error Detail`
  and sensitive-data logging must stay off in the Control Plane.
- **Loop-back:** none (non-blocking).
- **Verification:** the new test passes and fails when `Include Error Detail` is
  set.

### INF-1 — Reviewer independence

- **Severity:** Informational. **Category:** OTHER.
- The review ran in the same agent session that implemented the Story. Evidence
  was taken from code, tests and tool output rather than from the report, but
  this is not an independent human review; `HUMAN_PR_APPROVAL` should include a
  human look at `InstallationsController` and `InstallationRegistry`.

### INF-2 — Edit posts read before validating

- **Severity:** Informational. **Category:** API_SECURITY.
- `POST …/name` and `…/client-id` query the installation before checking
  `ModelState` (required by the API design: `404` before `400`). One extra
  indexed read per Owner submission; no exposure, since only the signed-in Owner
  reaches it.

## 19. Positive Controls

- Owner policy on every new route plus deny-by-default fallback (tested per
  operation, allowed and forbidden).
- Global antiforgery validation reaches the new POSTs (tested with no token and a
  foreign token).
- Unique indexes and lower-case check make domain uniqueness a database
  guarantee; lost races return `409`, not `500` (re-run).
- Triggers make identifier and domain immutable and forbid deletion at the
  database level.
- Audit rows in the same transaction, internal ids only, no refusal rows.
- HTML encoding everywhere; no inline script; copy script uses `textContent` and
  carries no text or data.
- Over-posting prevented by narrow request types.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- No penetration test or dynamic scanning.
- MIN-1 verified statically only.
- Full suite not re-run by the reviewer (implementation report evidence used;
  concurrency tests re-run).
- Reviewer independence limited (INF-1).
- Vulnerability scan relies on nuget.org advisory data at the time of the run.

## 22. Verdict Rationale

The implementation report records a green build and 359/359 tests with concrete
output. Every touched SC item is PASS with code and test evidence; no Critical or
Major finding; no unresolved security Open Decision. MIN-1 and the
informational notes do not block. Verdict **PASS**; next stage
`HUMAN_PR_APPROVAL`.

## Result envelope

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-002
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-002-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "MIN-1 (SC-10): lost-race path lets EF Core log the failed save; statically free of values (placeholders, redacted Npgsql detail) but no test reads the log after a race."
    - "INF-1: review ran in the same session as the implementation; a human look at InstallationsController and InstallationRegistry at HUMAN_PR_APPROVAL is advised."
    - "INF-2: edit posts read the installation before validation (404 before 400, by design)."
```
