---
artifact_type: implementation_report
story: US-050
version: 1
status: DRAFT
created_at: 2026-10-16T10:45:25Z
updated_at: 2026-10-16T10:45:25Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-050-browser-security-headers.md
    version: null
  - path: docs/specifications/US-050-spec.md
    version: 1
  - path: docs/decisions/US-050-open-decisions.md
    version: 1
  - path: docs/designs/api/US-050-api-design.md
    version: 1
  - path: docs/designs/database/US-050-db-design.md
    version: 1
  - path: docs/tests/US-050-test-strategy.md
    version: 1
  - path: docs/tests/US-050-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-050 Implementation Report — Every response carries the browser security headers

## 1. Summary

Each host gained one middleware, `SecurityHeadersMiddleware` in its `Security` namespace, registered
right after `NoStoreMiddleware` at the start of the pipeline. When a response starts it sets
`X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin` and the
host's `Content-Security-Policy` (spec FR-003/FR-004). Setting (not appending) replaces
antiforgery's `SAMEORIGIN`, so each header occurs once (FR-005). No view, script, endpoint, entity,
translation, audit or log event changed. Build, the full suite and the format check pass.

Limitation: the AC-004 real-browser sign-in check is **not yet done** (§3.1).

## 2. Source artifacts

Story `docs/stories/US-050-browser-security-headers.md`; Specification v1 (APPROVED); Open Decisions
v1 (none); API design v1 and DB design v1 (both NOT_APPLICABLE); test strategy v1; AC test matrix v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `ClassroomAgent.Web.Security.SecurityHeadersMiddleware` (`InvokeAsync`, `Apply`); `ClassroomAgent.Web.Program` registration | `Web.Security.SecurityHeadersResponseTests` (all), `Architecture.SecurityHeadersRuleTests` | PASS |
| AC-002 | `ClassroomAgent.ControlPlane.Security.SecurityHeadersMiddleware`; `ClassroomAgent.ControlPlane.Program` registration | `ControlPlane.Security.SecurityHeadersResponseTests` (all), `Architecture.SecurityHeadersRuleTests` | PASS |
| AC-003 | the `ContentSecurityPolicy` value of each middleware | `TheSentPolicy_IsStrict`, `TheSentPolicy_IsStrict_AndAllowsNoFormTargetOutsideTheHost` | PASS |
| AC-004 | installation `form-action 'self' https://accounts.google.com`, origin derived from `GoogleDefaults.AuthorizationEndpoint` | `TheGoogleSignInStart_RedirectsToTheOriginTheFormActionAllows`, `TheGoogleCallbackAddress_CarriesTheSecurityHeaders`; existing US-008/US-051 sign-in tests green | automated PASS; manual PENDING (§3.1) |
| AC-005 | no change needed — every view already complies (spec FR-006) | `Architecture.InlineCodeRuleTests` | PASS |
| AC-006 | — | full suite 6037/6037 | PASS |

### 3.1 AC-004 manual check — pending

A header test cannot prove that a browser follows the form under the policy. Needed: on a running
installation with a real Google OAuth client, press "Sign in with Google", complete the sign-in, and
confirm (a) the Admin lands signed in and (b) the browser console shows no
`Content-Security-Policy` violation. If the browser blocks another origin in the redirect chain,
that origin is **not** added in code — it goes back to the Specification (spec I-2).

Not performed here: it needs a real Google OAuth client and a person in a browser, which this
environment does not have and tests may not use (TC-4). As with US-047 FR-009, it is to be done by
the Owner and recorded here before or at `HUMAN_PR_APPROVAL`.

**Deferred by the Owner (2026-10-16).** The check is run on the pilot server, at the first Admin
sign-in of deployment stage 6, with the school's real OAuth client and domain — the same deferral
as US-047 FR-009. Approval at `HUMAN_PR_APPROVAL` accepts that it is not yet performed. Outcome, when
done: _not yet performed_.

## 4. Change set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Web/Security/SecurityHeadersMiddleware.cs` | new | FR-001…FR-005, AC-001, AC-003, AC-004 |
| `src/ClassroomAgent.ControlPlane/Security/SecurityHeadersMiddleware.cs` | new | FR-001…FR-005, AC-002, AC-003 |
| `src/ClassroomAgent.Web/Program.cs` | +2 lines: registration after `NoStoreMiddleware` | FR-002 (pipeline position) |
| `src/ClassroomAgent.ControlPlane/Program.cs` | +2 lines: registration after `NoStoreMiddleware` | FR-002 |
| `tests/ClassroomAgent.Tests/TestInfrastructure/SecurityHeaders.cs` | new (TEST_WRITING) | ac_test_matrix |
| `tests/ClassroomAgent.Tests/TestInfrastructure/InlineCode.cs` | new (TEST_WRITING) | ac_test_matrix |
| `tests/ClassroomAgent.Tests/Web/Security/SecurityHeadersResponseTests.cs` | new (TEST_WRITING) | ac_test_matrix AC-001, AC-003, AC-004 |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/SecurityHeadersResponseTests.cs` | new (TEST_WRITING) | ac_test_matrix AC-002, AC-003 |
| `tests/ClassroomAgent.Tests/Architecture/InlineCodeRuleTests.cs` | new (TEST_WRITING) | ac_test_matrix AC-005, AC-006 |
| `tests/ClassroomAgent.Tests/Architecture/SecurityHeadersRuleTests.cs` | new (TEST_WRITING) | ac_test_matrix FR-001 |
| `docs/tests/US-050-*.md`, `docs/evidence/US-050-*.md` | workflow artifacts | `{story_id}` patterns |

Not in scope and to stay out of the commit: `.claude/settings.local.json` (IDE-local, modified before
this Story).

## 5. Validation evidence

Baseline: the TEST_WRITING report recorded the red phase (42 header tests red for the missing
headers, 33 guards green) and the existing suite green.

After implementation (all four runs delegated to a `cheap-worker` subagent; edits made inline —
they are the security rule itself and two registration lines):

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --no-build --filter "…SecurityHeadersResponseTests\|…InlineCodeRuleTests\|…SecurityHeadersRuleTests\|…NoStoreResponseTests\|…UnknownFileLikePathTests"` | 0 | 145/145 passed (28 s) |
| `dotnet test ClassroomAgent.sln --no-build` | 0 | **6037 total, 6037 passed, 0 failed, 0 skipped** (7 min 39 s) |
| `dotnet format ClassroomAgent.sln --verify-no-changes --include <10 changed .cs files>` | 0 | no diagnostics |

## 6. Configuration changes

None (no `appsettings`, no new package; `Microsoft.AspNetCore.Authentication.Google` was already
referenced by `ClassroomAgent.Web`).

## 7. Deviations and discovered problems

- None from the artifacts. Technical choices the spec left open: the Google origin is derived from
  `GoogleDefaults.AuthorizationEndpoint` (FR-004 "derive or constant"); antiforgery's header is
  overwritten rather than suppressed (FR-005).
- If `GoogleOptions.AuthorizationEndpoint` is ever overridden in configuration, the CSP would not
  follow it; `TheGoogleSignInStart_RedirectsToTheOriginTheFormActionAllows` then fails and shows the
  mismatch.

## 8. Open Decisions

None touched or required.
