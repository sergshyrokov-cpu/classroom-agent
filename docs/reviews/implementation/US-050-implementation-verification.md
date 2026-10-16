---
artifact_type: implementation_verification
story: US-050
version: 1
status: APPROVED
created_at: 2026-10-16T11:10:00Z
updated_at: 2026-10-16T11:10:00Z
produced_by: implementation-verifier
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
  - path: docs/evidence/US-050-test-generation-report.md
    version: 1
  - path: docs/evidence/US-050-implementation-report.md
    version: 1
supersedes: null
---

# US-050 Implementation Verification — Every response carries the browser security headers

## 1. Summary

**Verdict: PASS.** No Critical or Major finding. Each host has one
`SecurityHeadersMiddleware` in its `Security` namespace, registered directly after
`NoStoreMiddleware` (before `PublicPortMiddleware`, the exception handler and the
static-file middleware), writing the four headers in `Response.OnStarting` by
assignment (so antiforgery's `SAMEORIGIN` is replaced). Header values match spec
FR-003 / FR-004 character for character. The Story's 75 tests pass. The AC-004
manual real-browser sign-in is still pending and is the Owner's to do before or at
`HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

Story US-050; Specification v1 (APPROVED); Open Decisions v1 (none); API design v1
(NOT_APPLICABLE — no `openapi`); DB design v1 (NOT_APPLICABLE — no `entity_model`);
test strategy v1; AC test matrix v1; test generation report v1; implementation
report v1. The report's `inputs` name the current versions — no staleness.
Working tree: `git status`, `git diff -- src`, the untracked files read directly.

## 3. AC Verification

| AC | Tests (matrix) | Verdict |
|---|---|---|
| AC-001 | `Web.Security.SecurityHeadersResponseTests`: endpoint × method enumeration on the public port for Admin/Dean/anonymous (asserts the enumeration reaches `sign-in` and `workspace/journal`), private-port enumeration (reaches `health/live`, `service/v1/status-pushes`), private 404, pages, antiforgery page `DENY` only, journal, export file, API refusal, sign-in redirect, HTTPS redirection, 403, 404, antiforgery 400, read-only 409, exception-handler 500, every static file, missing static file, Google callback | Proven. `SecurityHeaders.Problems` checks each header occurs exactly once with the exact value (VR-001, VR-002); the tests were red before the middleware (test generation report), so they are not vacuous. |
| AC-002 | `ControlPlane.Security.SecurityHeadersResponseTests`: enumeration for Owner / anonymous / before setup, pages incl. `/setup`, antiforgery `DENY` only, redirects (sign-in, setup gate), 404 page + `/api/v1`, antiforgery 400, 500, static files, missing static file | Proven. |
| AC-003 | `TheSentPolicy_IsStrict` (W), `TheSentPolicy_IsStrict_AndAllowsNoFormTargetOutsideTheHost` (P) | Proven: directive-by-directive on the sent header, no duplicate directives, no `'unsafe-inline'`/`'unsafe-eval'`, `form-action` exactly `['self', https://accounts.google.com]` (W) / `['self']` (P). Exact-value checks in every other test also exclude any other origin. |
| AC-004 | `TheGoogleSignInStart_RedirectsToTheOriginTheFormActionAllows`, `TheGoogleCallbackAddress_CarriesTheSecurityHeaders` | Automated part proven: the real Google handler's redirect origin equals the `form-action` origin, compared against the redirect actually produced (spec FR-004 last bullet). **Manual part PENDING** (implementation report §3.1). |
| AC-005 | `Architecture.InlineCodeRuleTests` | Proven: the scanner is self-tested positively (script body, uppercase, `onclick`, multi-line `onSubmit`, `style=` both cases, `<style>` block — SPEC_REVIEW m-1) and negatively (`<script src>`, `data-copy-target`, `class="online"`, comments), and `TheScan_ReachesTheViewsThatIncludeScripts` stops it from passing on an empty set. |
| AC-006 | whole suite | Implementation report: 6037/6037. Not rerun here (security-reviewer runs it). |

## 4. Specification Coverage

- FR-001 one rule per host — the two middleware classes; `SecurityHeadersRuleTests` guards that no other source file outside `Security` names a header and no view uses `http-equiv`.
- FR-002 every response, every port — pipeline position confirmed in both `Program.cs`; `OnStarting` survives the exception handler's response clear (500 tests green).
- FR-003 / FR-004 — values identical to the spec; the Google origin is derived from `GoogleDefaults.AuthorizationEndpoint`; `InstallationSecurityServices` does not override `AuthorizationEndpoint` (only `OnRedirectToAuthorizationEndpoint` at line 265).
- FR-005 — assignment, not append; antiforgery tests assert `[DENY]`.
- FR-006 — no view changed; guard test added.
- FR-007 / FR-008 — no other code changed; `NoStoreMiddleware` and its static-file hook untouched.
- No behaviour beyond the artifacts.

## 5. Design Conformance

API and DB stages NOT_APPLICABLE; no endpoint, route, status code, body, entity or
migration changed. AD-3/AD-4/AD-6/AD-8 unaffected (presentation-layer middleware
with constant values; `Microsoft.AspNetCore.Authentication.Google` already
referenced by `Web`).

## 6. Deviations

None from the artifacts. Two technical choices the spec explicitly left open are
disclosed in report §7 and are sound:

- Google origin derived from `GoogleDefaults.AuthorizationEndpoint` rather than a constant — accepted; a future override of `GoogleOptions.AuthorizationEndpoint` would be caught by the AC-004 automated test.
- Antiforgery's header overwritten rather than suppressed — accepted; observable result fixed by FR-005 and tested.

## 7. Report Accuracy

The change set in report §4 equals the working tree: 2 new middleware, 2 modified
`Program.cs` (+2 lines each), 6 new test files, 4 Story artifacts, plus the
workflow state files. `.claude/settings.local.json` is modified but correctly
listed as out of scope — keep it out of the commit. AC-to-test locations exist.
No `TODO`/`TBD`/`FIXME`, skipped test or `NotImplementedException` in the changed
files (ripgrep).

## 8. Story Test Run

Delegated to `cheap-worker` (no files touched):

```
dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~SecurityHeadersResponseTests|FullyQualifiedName~SecurityHeadersRuleTests|FullyQualifiedName~InlineCodeRuleTests"
exit 0 — total 75, failed 0, passed 75, skipped 0 (21 s)
```

The worker did not capture the build's warning count; the implementation report's
build (0 warnings, 0 errors) stands, and the security review reruns the build.

## 9. Findings

- **N-1 (Minor, advisory)** — AC-004 manual real-browser Google sign-in not yet
  performed (implementation report §3.1). It is not a code defect but the AC is
  only partly proven until the Owner signs in once on a running installation and
  sees no CSP violation in the browser console. Record the result in report §3.1
  before or at `HUMAN_PR_APPROVAL`. If another origin is blocked, it goes back to
  the Specification (spec I-2), not into code.
- **N-2 (Minor, advisory)** — the expected CSP strings are duplicated in
  `TestInfrastructure/SecurityHeaders.cs` and in each middleware. Intentional
  (tests pin the spec value independently of the code); noted only so that a
  future policy change updates both.

## 10. Verdict

PASS — next stage SECURITY_REVIEW.
