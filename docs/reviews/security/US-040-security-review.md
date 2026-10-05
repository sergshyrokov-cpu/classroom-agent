---
artifact_type: security_review
story: US-040
version: 1
status: APPROVED
created_at: 2026-10-05T07:05:22Z
updated_at: 2026-10-05T07:05:22Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-040-no-store-responses.md
    version: null
  - path: docs/specifications/US-040-spec.md
    version: 2
  - path: docs/decisions/US-040-open-decisions.md
    version: 2
  - path: docs/designs/api/US-040-openapi.yaml
    version: 1
  - path: docs/designs/api/US-040-api-design.md
    version: 1
  - path: docs/designs/database/US-040-db-design.md
    version: 1
  - path: docs/tests/US-040-test-strategy.md
    version: 1
  - path: docs/tests/US-040-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-040-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: true
runtime_checks: FULL
---

# US-040 Security Review — Responses are not cached

## 1. Executive summary

**PASS.** The Story adds a confidentiality control (SC-14): one middleware per
host sets `Cache-Control: no-store` on every response except a served static
file. Verified by reading the code and wiring and by 35 integration tests over
real responses of both hosts (both ports of the installation, every routed
endpoint, every caller kind, every refusal kind). No authentication,
authorization, persistence, Google, channel or logging behaviour changed. No
Critical, Major or Minor findings; four informational notes.

## 2. Reviewed artifacts

Front matter `inputs`. All current; no `SUPERSEDED` input.

## 3. Security-relevant scope

- Changed: `src/ClassroomAgent.Web/Security/NoStoreMiddleware.cs`,
  `src/ClassroomAgent.ControlPlane/Security/NoStoreMiddleware.cs`, two lines in
  each `Program.cs` (registration first in the pipeline; `OnPrepareResponse`
  hook on `UseStaticFiles`).
- Asset protected: personal data of students in pages (and, later, exports) as
  a browser or proxy on a shared computer would cache it.
- Boundary: host → browser and intermediate caches. No new boundary.

## 4. Environment and tools

.NET SDK 10.0.401; Docker 29.8.0 with Testcontainers available. Commands run:
`git status`, `git diff`, `dotnet list ClassroomAgent.sln package --vulnerable`
(no vulnerable package in any project). Test evidence from the implementation
report: 35/35 story tests; full suite 3438/3439 with one database-connect
timeout, re-run green.

## 5. Project security checklist

| SC | Status | Evidence |
|---|---|---|
| SC-14 Not cached | PASS | The middleware is the first `app.Use…` in both `Program.cs` — before `PublicPortMiddleware` and the public-port branch (Web), before `UseExceptionHandler` (Control Plane). It decides in `Response.OnStarting`, so redirects, status-code re-execution, the exception handler and refusals are covered — proven by `NoStoreResponseTests` (302, 308, 400, 403, 404, 409, 500, private port, setup gate). The only exception is a response the static-file middleware marked through `OnPrepareResponse`; a missing file under `/css` gets `no-store` (tested). No per-page attribute (`NoStoreRuleTests`). |
| SC-4 Authorization | PASS (unchanged) | No endpoint, policy or anonymous entry added. The middleware only sets a header and calls `next`; it never ends the request or touches the principal. Existing TC-5 enumeration tests green. |
| SC-10 Hygiene | PASS | The middleware logs nothing and reads no request data. |
| SC-2 Authentication | PASS (unchanged) | No cookie, session, HTTPS or HSTS change; the HTTPS redirection still answers `308`, now with `no-store`. |
| SC-1, SC-3, SC-5 … SC-9, SC-11 … SC-13 | NOT_APPLICABLE | No role, AllowedAdmin, write use case, DB UI, key, Google access, channel, audit, `Contracts` or outbound flow touched. |

## 6. Authentication and authorization

Unchanged. The rule runs before authentication and applies whatever the role,
session or read-only mode (spec SR-001). It cannot grant access.

## 7. Credentials, key and Google access

Not touched.

## 8. Sensitive data exposure

The control reduces exposure. The static-file marker is a private object used
as a key in `HttpContext.Items`: a request cannot set it, so a client cannot
opt a data page out of `no-store`. Only the static-file middleware, which
serves the files of `wwwroot` alone, sets it.

## 9. Input validation

No new input.

## 10. API security

No endpoint added or changed. Behaviour matches openapi `x-host-wide-rules`:
D-1 (a value with `no-store` is kept), D-2 (no `Pragma`/`Expires` added), D-4
(exception by served file), D-5 (both ports, one registration).

## 11. Persistence and configuration

No schema, migration or configuration change (DB_DESIGN `NOT_APPLICABLE`).

## 12. Logging, audit and telemetry

No log line or audit event added; none required (spec §8).

## 13. Dependencies

No package or project reference added (`*.csproj` unchanged).
`dotnet list package --vulnerable`: no vulnerable package reported.

## 14. Security test coverage

AC-001 … AC-004 are each covered by tests that failed before the change and
pass after it (ac_test_matrix). The enumeration tests make a future endpoint
without the header fail the build. Fixtures are synthetic; no live Google or
Control Plane call (TC-4).

## 15. Abuse cases

| Scenario | Protection | Evidence | Status |
|---|---|---|---|
| Back button after sign-out shows the journal from cache | `no-store` on the journal page | `Web … TheJournal_Returns200_WithNoStore` | PASS |
| A refusal or error page cached and replayed | `no-store` on 302/400/403/404/409/500 | per-kind tests, both hosts | PASS |
| A path crafted to look like a static file to escape the rule | exemption by served file, not by path | `AMissingFileUnderAStaticPath_CarriesNoStore`, both hosts | PASS |
| A future page added without the header | enumeration of every routed endpoint | `EveryEndpoint_*` | PASS |

## 16. Repository hygiene

No secret-like, database or `.xlsx` file in the change set. Live credential
files were not opened. See INFO-1 for files outside this Story's scope.

## 17. Deviations

None from the Specification or the designs.

## 18. Findings

- **INFO-1** (REPOSITORY_HYGIENE) — The working tree also holds
  `docs/stories/US-041-unknown-file-path-404.md` and the US-041 entry in
  `docs/catalog/stories.yaml`, a draft prepared from TEST F-2. They are outside
  the scope of US-040 (AGENTS.md Git Policy) and must be left out of the US-040
  commit and committed separately. No security effect.
- **INFO-2** (CONFIGURATION) — The middleware exists twice, once per host, with
  identical logic. Expected: the hosts share no project for pipeline code
  (`package-map.md`). Keep both in step if either changes.
- **INFO-3** (OTHER) — Responses Kestrel writes before the pipeline (malformed
  request `400`, `431`) carry no `no-store`; accepted by the Owner as OD-001 (a).
  They carry no data.
- **INFO-4** (TEST_COVERAGE) — One full-suite run hit an Npgsql connect timeout
  while creating a test database (IMPL F-1), unrelated to this change; the
  re-run was green.

## 19. Positive controls

The rule is first in each pipeline; the decision is taken when the response
starts; the static-file marker cannot be forged; existing `no-store` values are
preserved; no caching attribute exists; enumeration tests guard future
endpoints.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review limitations

Code, configuration and test review only — no penetration test. A real reverse
proxy in front of the hosts was not exercised.

## 22. Verdict rationale

Build and story tests green with recorded evidence; every touched SC item is
PASS; no Critical, Major or Minor finding; no open security decision. PASS →
HUMAN_PR_APPROVAL.
