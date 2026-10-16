---
artifact_type: security_review
story: US-050
version: 1
status: APPROVED
created_at: 2026-10-16T11:40:00Z
updated_at: 2026-10-16T11:40:00Z
produced_by: security-reviewer
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
  - path: docs/evidence/US-050-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-050-implementation-verification.md
    version: 1
  - path: trebovaniya.md
    version: 96
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: true
runtime_checks: PARTIAL
---

# US-050 Security Review — Browser security headers

## 1. Executive Summary

**PASS.** The Story adds one middleware per host (`SecurityHeadersMiddleware`, in each
host's `Security` namespace) that writes `X-Frame-Options: DENY`,
`X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin` and a strict
`Content-Security-Policy` on every response, registered directly after
`NoStoreMiddleware` and before every other middleware. It is a pure hardening change:
no endpoint, route, body, entity, audit or log event changes. The independent build
is 0 warnings / 0 errors and the full suite is 6037/6037 green.

No Critical, Major or Minor findings. Four Informational observations. The one
open item is the manual real-browser Google sign-in of AC-004 (INFO-1), which a
header test cannot prove; it is the Owner's check before or at
`HUMAN_PR_APPROVAL` and is already disclosed in the implementation report §3.1.

Runtime checks are PARTIAL: everything automated was run; the browser's
enforcement of the policy was not observed (INFO-1).

## 2. Reviewed Artifacts

See front matter `inputs`. Code reviewed: both `SecurityHeadersMiddleware.cs`, the
two `Program.cs` diffs, `InstallationSecurityServices.ConfigureGoogle`
(`OnRedirectToAuthorizationEndpoint`), all `.cshtml` and `wwwroot` files of both
hosts (pattern sweep), the four new test classes and the two test helpers.

## 3. Security-Relevant Scope

- **Exposed functionality:** none added. Every response of the installation
  (public and private port) and of the Control Plane gains four headers.
- **Assets protected:** signed-in sessions of Admin, Dean and Owner against
  clickjacking, MIME sniffing, injected script/style, foreign form targets and
  referrer leakage of page addresses (which may carry query parameters).
- **Trust boundary:** browser ↔ each host. No new outbound flow.
- **Components:** `ClassroomAgent.Web.Security`, `ClassroomAgent.ControlPlane.Security`,
  both pipelines.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 (Testcontainers available).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 6037 total, 6037 passed, 0 failed, 0 skipped (7 m 59 s).
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` — no
  vulnerable packages in any project (nuget.org source).
- `git status`, `git diff`, `git check-ignore` on credential and generated files.
- Delegated (cheap-worker, read only, facts only): build, full test run, package
  scan, `git diff --stat` / `git status`. All judgement, the code reading and the
  pattern sweeps were done by this review.
- Not performed: a real browser enforcing the policy (INFO-1); no penetration test.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-15 Browser headers | PASS | Values in both middlewares equal FR-003/FR-004 exactly; set (not appended) in `Response.OnStarting`, so antiforgery's `SAMEORIGIN` is overwritten (`APageWithAnAntiforgeryToken_CarriesOnlyXFrameOptionsDeny`, both hosts); no other source file names a header (`SecurityHeadersRuleTests`, grep confirms); no `<meta http-equiv>` (`NoView_SetsAHeaderThroughMeta`); no inline script/style/handler in views and no `eval`/inline-attribute construct in scripts (`InlineCodeRuleTests`); no `Html.Raw` in any view. |
| SC-13 Outbound | PASS | Only foreign origin is `https://accounts.google.com` in the installation `form-action`, derived from `GoogleDefaults.AuthorizationEndpoint`; `ConfigureGoogle` does not override the endpoint; no `report-uri`/`report-to`. |
| SC-14 no-store | PASS | `NoStoreMiddleware` and its static-file exemption untouched; suite green. |
| SC-2 HSTS / HTTPS | PASS | Public-port branch untouched; HTTPS redirection response carries the headers (`TheHttpsRedirection_CarriesTheSecurityHeaders`). |
| SC-4 Authorization / antiforgery | PASS | No route, policy or `[AllowAnonymous]` change; antiforgery refusal carries headers (both hosts). |
| SC-10 Hygiene | PASS | Constant values; nothing read from the request; no logging added. |
| SC-1, SC-3, SC-5–SC-9, SC-11, SC-12 | NOT_APPLICABLE | No role, AllowedAdmin, write use case, DB UI, key, Google data call, channel, audit or Contracts change. |

## 6. Authentication and Authorization

Unchanged. The Google sign-in start (redirect to Google) carries the installation
CSP and its `Location` origin equals the origin in `form-action`
(`TheGoogleSignInStart_RedirectsToTheOriginTheFormActionAllows`); the callback
address carries the headers. No anonymous access added.

## 7. Credentials, Key and Google Access

Not touched. No scope, impersonation or key handling change.

## 8. Sensitive Data Exposure

No response body, DTO, view model, log, audit row or export changes.
`Referrer-Policy: same-origin` reduces exposure: page addresses no longer reach
other sites in `Referer`.

## 9. Input Validation

No external input added; the middleware reads nothing from the request.

## 10. API Security

No endpoint change (API design NOT_APPLICABLE). `/api/v1` refusals carry the
headers (`AnApiRefusal_CarriesTheSecurityHeaders`); the export download carries them.

## 11. Persistence and Configuration

No schema, migration or configuration change (DB design NOT_APPLICABLE).

## 12. Logging, Audit and Telemetry

No log or audit event added. `docs/hooks/tool-usage.jsonl` is git-ignored
(`.gitignore:63`).

## 13. Dependencies

No package or project reference added; `Microsoft.AspNetCore.Authentication.Google`
(for `GoogleDefaults`) was already referenced by `Web`. Vulnerability scan clean.

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| Every endpoint/method, both ports, both hosts (VR-004) | `EveryEndpoint_OnThePublicPort_…`, `EveryEndpoint_OnThePrivatePort_…`, CP `EveryEndpoint_CarriesTheSecurityHeaders` (per caller, incl. before Owner exists) |
| Error paths (500 after `ClearResponse`, 403, 404, 409 read-only, antiforgery 400, redirects) | dedicated tests per host |
| Static files incl. missing file | `EveryStaticFile_…`, `AMissingFileUnderAStaticPath_…` |
| One value per header (FR-005) | `SecurityHeaders` helper asserts single occurrence; antiforgery tests |
| Strict policy (AC-003) | `TheSentPolicy_IsStrict…` both hosts |
| No inline code (AC-005) | `InlineCodeRuleTests` with self-tested scanner |
| Google flow (AC-004 automated) | sign-in start + callback tests |

Tests assert the response actually sent, not a method call — they prove the property.

## 15. Abuse Case Review

| Scenario | Protection | Status |
|---|---|---|
| Hostile site frames a signed-in page | `X-Frame-Options: DENY` + `frame-ancestors 'none'` on every response | PASS |
| Injected markup (future bug) runs inline script / handler | CSP without `'unsafe-inline'`; views and scripts kept clean by guard tests | PASS |
| Injected `<form>` posts to a foreign site | `form-action 'self'` (+ Google origin on installation) | PASS |
| Injected `<base>` rewrites relative URLs | `base-uri 'self'` | PASS |
| Uploaded/exported file sniffed as HTML | `nosniff` on downloads and static files | PASS |
| Page address with query parameters leaks via `Referer` | `same-origin` | PASS |

## 16. Repository Hygiene

`google_credentials.json`, `dac-classroom-agent-*.json` and `classroom_cache.db`
are git-ignored (verified, not opened). No secret, database file or `.xlsx` in the
change set. `.claude/settings.local.json` is modified in the working tree and is
IDE-local — keep it out of the Story commit (INFO-3).

## 17. Deviations

None. The two technical choices (origin from `GoogleDefaults`, overwrite rather
than `SuppressXFrameOptionsHeader`) are both allowed by FR-004/FR-005.

## 18. Findings

- **INFO-1 (TEST_COVERAGE, SC-15/AC-004)** — Browser enforcement of the installation
  `form-action` during the real Google sign-in is unverified. Browsers check
  `form-action` against the whole redirect chain of the form submission; if Google
  redirected within that chain to another origin (e.g. a third-party SSO
  provider of a school domain), the sign-in would be blocked. Fails closed (no
  security risk, availability only). Required: the Owner's manual sign-in
  (impl report §3.1) before or at `HUMAN_PR_APPROVAL`; a blocked origin goes back
  to the Specification (spec I-2), not into code.
- **INFO-2 (CONFIGURATION)** — Responses Kestrel produces before the pipeline
  (malformed request → 400, header too large → 431) do not carry the headers.
  These bodies are empty and contain no page; outside what middleware can cover.
  No action.
- **INFO-3 (REPOSITORY_HYGIENE)** — `.claude/settings.local.json` is modified; do not
  stage it in the Story commit (DoD item 8).
- **INFO-4 (CONFIGURATION)** — `OnStarting` callbacks run last-registered-first; the
  rule, registered first, therefore runs last and wins over any later header
  write. This is the property FR-005 relies on; it holds only while the rule stays
  first in the pipeline. The pipeline-order comment and the antiforgery tests
  guard it. No action.

## 19. Positive Controls

- Exact header values on every response kind, both ports, both hosts, incl. the
  500 page after the exception handler clears the response — test-verified.
- Single host-wide rule enforced by an architecture test; no `<meta>` override.
- CSP free of `'unsafe-inline'`, `'unsafe-eval'`, scheme sources, nonces/hashes.
- Google origin derived from the handler's actual endpoint and test-pinned to the
  redirect `Location`.
- No new package; no vulnerable package.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- Browser enforcement not observed (INFO-1).
- No penetration test; code, configuration and test review only.
- Vulnerability scan relies on the nuget.org advisory data at run time.

## 22. Verdict Rationale

Build and full suite green in this review's own run; every touched SC item PASS;
security-sensitive ACs AC-001…AC-003 and AC-005 verified by passing tests;
AC-004 automated part passes and its manual part is disclosed and assigned to the
Owner. No Critical/Major finding, no open security decision → **PASS**.
