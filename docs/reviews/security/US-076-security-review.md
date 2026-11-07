---
artifact_type: security_review
story: US-076
version: 1
status: APPROVED
verdict: PASS
created_at: 2026-11-07T12:17:42Z
updated_at: 2026-11-07T12:17:42Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-076-page-help.md
    version: null
  - path: docs/specifications/US-076-spec.md
    version: 3
  - path: docs/decisions/US-076-open-decisions.md
    version: 1
  - path: docs/designs/api/US-076-api-design.md
    version: 2
  - path: docs/designs/database/US-076-db-design.md
    version: 2
  - path: docs/tests/US-076-test-strategy.md
    version: 2
  - path: docs/tests/US-076-ac-test-matrix.md
    version: 2
  - path: docs/evidence/US-076-implementation-report.md
    version: 2
  - path: docs/reviews/implementation/US-076-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: false
runtime_checks: FULL
---

# US-076 Security Review — page help («?») on five Dean pages

## 1. Executive Summary

**PASS.** The Story adds a static, script-free `<details>` disclosure beside the title of five already-protected
installation pages (journal, Meet codes, Meet reports, one meeting, report templates). Its text comes only from the
two translation files and is rendered through `IStringLocalizer` with Razor HTML encoding. No endpoint, policy, request
parameter, entity, migration, configuration, header, package, log line or audit event was added or changed. Independent
build 0/0 and full suite 7779/7779. No Critical, Major or Minor findings; two Informational notes.

## 2. Reviewed Artifacts

As listed in `inputs`. Spec v3 approved at `HUMAN_SPEC_APPROVAL` (history 2026-11-07T10:59:53Z). Open Decisions: OD-001
RESOLVED, none open. No input is SUPERSEDED.

## 3. Security-Relevant Scope

Changed production files (`git diff`, untracked files by `ls`):

| File | Change |
|---|---|
| `Application/Localization/SharedResource.{uk,en}.resx` | 17 new static entries `PageHelp.*` each |
| `Web/Views/Shared/_Layout.cshtml` | wraps `<h1>` in `<div class="page-title">` and renders `_PageHelp` when `ViewData["PageHelp"]` is one of five ids |
| `Web/Views/Shared/_PageHelp.cshtml` (new) | `<details><summary aria-label=…>?</summary>` + `<p>@T[key]</p>` lines |
| `Web/Security/PageHelpTextKeys.cs` (new) | page ids, key list; unknown id → null (plain title) |
| five views | one `ViewData[...] = PageHelpTextKeys.X` line each |
| `Web/wwwroot/css/site.css` | layout rules only; no `url()`, no import, no external resource |

Assets: none new. The help holds no Google data, no school text, no personal data. Trust boundary touched: browser →
installation host (rendered HTML only). Page id source is server-side `ViewData` set by the view, never request input.

## 4. Environment and Tools

.NET SDK 10.0.401; Docker 29.8.0 (Testcontainers). Free RAM 6.9 GB at start.

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors (incremental, up to date with the working tree) |
| `dotnet test ClassroomAgent.sln --no-build` | 7779 total, 7779 passed, 0 failed, 0 skipped; 11 min 39 s |
| `dotnet list ClassroomAgent.sln package --vulnerable` | no vulnerable packages in any of the 7 projects (nuget.org feed) |

Delegated: nothing; all reads, sweeps and commands run inline (small change set).

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or seed change |
| SC-2 Authentication | NOT_APPLICABLE | no sign-in change; `TheForcedPasswordChange_HasNoHelp` confirms the help is absent there |
| SC-3 AllowedAdmin | NOT_APPLICABLE | untouched |
| SC-4 Authorization | PASS | no new endpoint or page; the five pages keep their policies (no controller diff); `NoOtherInstallationPage_HasTheHelp`, `NoControlPlanePage_HasTheHelp`; error page sets no id, so the anonymous-reachable error page shows no help |
| SC-5 Read-only mode | PASS | no write path; `InReadOnlyMode_TheHelpIsTheSame` |
| SC-6 No DB UI | NOT_APPLICABLE | untouched |
| SC-7 Key | NOT_APPLICABLE | untouched |
| SC-8 Google | NOT_APPLICABLE | no Google call |
| SC-9 Channel | NOT_APPLICABLE | untouched |
| SC-10 Hygiene | PASS | no log statement added; texts carry no personal data; nothing user-supplied is echoed |
| SC-11 Audit | NOT_APPLICABLE | viewing help is not an SC-11 action (spec §7) |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` untouched |
| SC-13 Outbound | PASS | no script, no external URL in markup or CSS; the disclosure sends no request |
| SC-15 Headers / CSP | PASS | `SecurityHeadersMiddleware.cs` unchanged; no inline script, style attribute or event handler; `EveryLoad_IsCollapsed_NothingIsStoredOrWritten_AndTheHeadersAreUnchanged` |

## 6. Authentication and Authorization

The help renders only inside the layout of five views that already require their existing policies; it adds no route.
`PageOf` accepts only the five constant ids from `ViewData`, which no request can set.

## 7. Credentials, Key and Google Access

Not touched.

## 8. Sensitive Data Exposure

Static translated texts only. `T[key]` is `IStringLocalizer<SharedResource>` (`_ViewImports.cshtml:7`); `LocalizedString`
is HTML-encoded by Razor — no `Html.Raw`, no `IHtmlLocalizer` (sweep of the three new/changed shared files). Nothing is
stored (no cookie, no storage, no form) — asserted by the test above.

## 9. Input Validation

No new input. The page id is server-side; an unknown value falls back to the plain title (VR-001) and `KeySegment`'s
throw is unreachable because `LineKeys` is called only after `PageOf` validated the id.

## 10. API Security

No REST or Razor endpoint added or changed; `/api/v1` untouched.

## 11. Persistence and Configuration

No entity, migration, `appsettings` or `.csproj` change. Database design v2 records "no change".

## 12. Logging, Audit and Telemetry

No logger call or audit event added. Hook telemetry untouched by the Story.

## 13. Dependencies

No package or project reference added. Vulnerability scan clean (section 4).

## 14. Security Test Coverage

| Property | Test |
|---|---|
| help only on the five pages, both hosts | `PageHelpTests.NoOtherInstallationPage_HasTheHelp`, `NoControlPlanePage_HasTheHelp` |
| not on forced password change | `TheForcedPasswordChange_HasNoHelp` |
| read-only mode unchanged | `InReadOnlyMode_TheHelpIsTheSame` |
| headers unchanged, nothing stored | `EveryLoad_IsCollapsed_NothingIsStoredOrWritten_AndTheHeadersAreUnchanged` |
| only approved texts | `TheTranslationFiles_HoldExactlyTheApprovedTexts` |
| allowed/forbidden role per page | existing tests of the five pages (unchanged, green in the full run) |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Anonymous user opens a page with help | redirect/403 as before | policies unchanged; full suite green | PASS |
| Attacker injects markup via help | impossible — texts are static resources, encoded | `_PageHelp.cshtml`, resx | PASS |
| Help used to exfiltrate data | no script, no external resource | CSS/markup sweep, CSP unchanged | PASS |

## 16. Repository Hygiene

`classroom_cache.db`, the two `.xlsx` exports, `google_credentials.json`, `dac-classroom-agent-*.json` and
`.claude/settings.local.json` remain git-ignored (`git status --ignored`); credential files not opened. No secret-like
file among the Story's changes.

## 17. Deviations

None security-relevant. The two IMPLEMENTATION_VERIFICATION Minor findings (spec wording; test readers stripping every
`<details>`) are not security matters and stay open with their owner.

## 18. Findings

- **INFO-1** (TEST_COVERAGE). `PageResponse.Text` / `VisibleText` strip every `<details>` page-wide (already
  IMPLEMENTATION_VERIFICATION/m-2). Security angle: a future `<details>` holding sensitive data would be invisible to
  whole-page "does not contain" assertions; harmless today (one `<details>` in `src`). No correction required now;
  narrowing per m-2 also closes this.
- **INFO-2** (OTHER). The independent build was incremental (2 s, up to date); tests ran on binaries matching the
  working tree. No correction required.

## 19. Positive Controls

Razor encoding of localized strings; no script and unchanged CSP; server-side, closed-list page id; help absent from
anonymous, forced-password-change and Control Plane pages; no state, storage or request created by the control.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

Code, configuration and test review plus the full automated suite; no browser or penetration testing. The AC-007 look
walk is the Owner's manual check and not security evidence.

## 22. Verdict Rationale

The change is presentation-only, adds no attack surface and every touched SC item is PASS with test evidence; build and
full suite green; no blocking Open Decision. PASS → HUMAN_PR_APPROVAL.

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-076
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-076-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "INFO-1: PageResponse.Text/VisibleText strip every <details>; a future <details> would escape whole-page negative assertions (same fix as IMPLEMENTATION_VERIFICATION/m-2)"
    - "INFO-2: independent build was incremental (up to date); tests ran on binaries matching the working tree"
  closed_findings: []
```
