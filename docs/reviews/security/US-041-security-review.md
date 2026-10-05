---
artifact_type: security_review
story: US-041
version: 1
status: APPROVED
created_at: 2026-10-05T07:45:37Z
updated_at: 2026-10-05T07:45:37Z
produced_by: security-reviewer
inputs:
  - path: docs/evidence/US-041-implementation-report.md
    version: 1
  - path: docs/specifications/US-041-spec.md
    version: 2
  - path: docs/decisions/US-041-open-decisions.md
    version: 2
  - path: docs/designs/api/US-041-openapi.yaml
    version: 1
  - path: docs/designs/api/US-041-api-design.md
    version: 1
  - path: docs/designs/database/US-041-db-design.md
    version: 1
  - path: docs/tests/US-041-ac-test-matrix.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: true
runtime_checks: FULL
---

# US-041 Security Review — An unknown file-like address answers 404

## 1. Executive Summary

**PASS.** The change is one route pattern per host: the SC-4 anonymous 404
catch-all is mapped as `{*path}` instead of `MapFallback`'s default
`{*path:nonfile}`. It narrows anonymous behaviour toward SC-4 (an unknown
file-like path now gets `404` instead of a sign-in challenge) and adds no
anonymous endpoint, no data access, no write. No Critical, Major or Minor
finding. Limitation: the reviewer is the same agent session as the implementor
(see §21).

## 2. Reviewed Artifacts

Front matter `inputs`, plus the working-tree diff of
`src/ClassroomAgent.Web/Program.cs`, `src/ClassroomAgent.ControlPlane/Program.cs`
and the three test files.

## 3. Security-Relevant Scope

- Installation public port and private port; Control Plane (before/after setup).
- Assets: none read or written by the catch-all. Indirectly protected: every
  real endpoint behind deny-by-default (SC-4) and the setup gate.
- Trust boundary: browser → host, at routing.

## 4. Environment and Tools

.NET SDK 10.0.401; Docker Desktop with Testcontainers available. Commands:
`git diff`, `dotnet list ClassroomAgent.sln package --vulnerable`; build and
test evidence taken from the implementation report and the stage runs in this
session (full suite 3476/3476).

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or seed change |
| SC-2 Authentication | NOT_APPLICABLE | no sign-in, cookie or HTTPS change; `/signin-google` handled in `UseAuthentication` before endpoint execution — existing US-008 tests green |
| SC-4 Authorization | PASS | catch-all remains the single listed anonymous entry; `MapFallback` keeps lowest precedence so no real endpoint is shadowed; fallback authorization policy untouched; regression tests `ARealProtectedPage_StillRedirects…`, `AfterSetup_ARealProtectedEndpoint_StillRedirects…`, `BeforeSetup_ARealEndpoint_IsStillRedirectedToSetup` green; private routes still port-checked (`PublicPortMiddleware`, `PrivatePortEndpointFilter` unchanged) |
| SC-5 Read-only mode | NOT_APPLICABLE | no use case |
| SC-6 No DB UI | PASS | no endpoint added; developer exception page test green |
| SC-7 Key | NOT_APPLICABLE | no configuration change |
| SC-8 Google | NOT_APPLICABLE | no Google port |
| SC-9 Channel | PASS | private port: outside private paths still `404` from the port filter; an unmatched path under a private prefix now `404` with no body (tests `OnThePrivatePort_*`) |
| SC-10 Hygiene | PASS | response is the translated error page; path not echoed (`AMissingFileLikePath_IsNotEchoed`); no log statement added |
| SC-11 Audit | PASS | no audited action; tests assert no audit row on both hosts |
| SC-12, SC-13 | NOT_APPLICABLE | no Contracts change, no outbound flow |

## 6. Authentication and Authorization

Anonymous, Admin, Dean (installation) and anonymous, Owner (Control Plane) all
get `404` for an unmatched file-like path; real endpoints keep their policies.
A file-like path that matches a real endpoint is not possible to steal: route
matching prefers any non-fallback endpoint.

## 7. Credentials, Key and Google Access

Not touched.

## 8. Sensitive Data Exposure

The `404` page carries only `Error.NotFound`. Uniform `404` for every caller
and every unknown path reveals nothing; previously the `302` already revealed
nothing.

## 9. Input Validation

The path is not interpreted or reflected. Static-file traversal test
(`StaticFiles_ComeOnlyFromTheApplicationsOwnDirectory`) green.

## 10. API Security

No endpoint added; behaviour matches US-041-openapi.yaml. `/api/v1` unchanged
(OD-002).

## 11. Persistence and Configuration

No entity, migration or configuration change.

## 12. Logging, Audit and Telemetry

No logging change; no audit change.

## 13. Dependencies

No `.csproj` change. `dotnet list package --vulnerable`: no vulnerable packages
in any of the 7 projects for the current sources.

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| unknown file-like path → 404, every caller | Web/ControlPlane `UnknownFileLikePathTests` |
| deny-by-default intact | redirect regression tests (both hosts) |
| setup gate intact; OD-001 | `BeforeSetup_*` |
| no write | `AMissingFileLikePath_WritesNothing`, `…WritesNoAuditRow` |
| private port isolation | `OnThePrivatePort_*` |
| anonymous-endpoint enumeration (TC-5) | existing suite, green |

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| Probe for protected pages by file-like names | uniform `404`, no hint | tests above | PASS |
| Reach a private route via the public port with a dotted path | `404` | catch-all or `PrivatePortEndpointFilter` | PASS |
| POST to an unknown path to provoke a write | `404`, nothing written | `…AnyMethod…`, no audit row | PASS |

## 16. Repository Hygiene

Change set: 2 production files, 3 test files, Story artifacts and workflow
state. No secret, database file, `.xlsx` or IDE config. Live credential files
not opened.

## 17. Deviations

None.

## 18. Findings

- **INFO-1** (API_SECURITY, SC-4): the catch-all is a minimal-API delegate, so
  the global MVC antiforgery filter does not run on it; a POST to an unknown
  path gets `404` without token validation. Unchanged from before US-041 for
  non-file paths; the endpoint writes nothing, so no CSRF risk. No correction.
- **INFO-2** (OTHER): SC-4's "API-6 body under `/api/v1`" for the catch-all is
  still not implemented on either host — out of scope by OD-002; to be closed
  by the first Story that adds an `/api/v1` resource.

## 19. Positive Controls

Lowest-precedence fallback; deny-by-default fallback policy; setup-gate
exemption limited to the catch-all and listed controllers; port isolation;
uniform translated error page; no reflection of the path.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- Same agent session as the implementation — independence is procedural, not
  personal; the human review at HUMAN_PR_APPROVAL covers this.
- No penetration testing; code, configuration and test review only.

## 22. Verdict Rationale

All touched SC items PASS with runtime test evidence; zero Critical/Major/Minor
findings; dependencies unchanged and scanned. → PASS, next HUMAN_PR_APPROVAL.
