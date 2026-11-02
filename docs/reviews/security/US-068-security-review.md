---
artifact_type: security_review
story: US-068
version: 2
status: APPROVED
created_at: 2026-11-01T15:01:21Z
updated_at: 2026-11-02T10:41:50Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-068-restrained-look-with-bootstrap.md
    version: null
  - path: docs/specifications/US-068-spec.md
    version: 2
  - path: docs/decisions/US-068-open-decisions.md
    version: 2
  - path: docs/designs/api/US-068-api-design.md
    version: 1
  - path: docs/designs/database/US-068-db-design.md
    version: 1
  - path: docs/tests/US-068-test-strategy.md
    version: 1
  - path: docs/tests/US-068-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-068-implementation-report.md
    version: 2
  - path: docs/reviews/implementation/US-068-implementation-verification.md
    version: 2
  - path: trebovaniya.md
    version: 105
supersedes: docs/reviews/security/US-068-security-review.md@v1
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 1
security_sensitive: true
runtime_checks: FULL
---

# US-068 Security Review — Both hosts share one restrained look built on Bootstrap

Revision v2: re-run after `/so:reject` at HUMAN_PR_APPROVAL (finding `US-068/HUMAN_PR_APPROVAL/R-1`) and IMPLEMENTATION
attempt 2. Reviewed against the current working tree, not against v1's conclusions.

## 1. Executive Summary

**Result: PASS — 0 Critical, 0 Major, 0 Minor, 1 Informational.**

The Story stays presentation-only after the look check. Attempt 2 added CSS rules to both `site.css` files and `text-end`
classes to 9 views. No `.cs`, `.csproj`, `Program.cs`, middleware, policy, route, migration, `.resx`, `appsettings` or
script changed. The security property at stake, SC-15 "styling does not loosen the headers", holds: the CSP string is
untouched, nothing is loaded from another site, and no inline code appeared. The Owner's deviation from FR-005.3 (Meet
codes actions look like links) is CSS only: the state change stays a POST form with its antiforgery token.

Build clean. The full suite ran 6989 of 6990 green; the one failure is a log-wait timeout in a background sync test that
this Story's change cannot reach, and its class is 5/5 green on rerun (INFO-3). No vulnerable packages. Next:
`HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

See front matter `inputs`. API and DB design are NOT_APPLICABLE records (v1). No input is `SUPERSEDED`.
`HUMAN_SPEC_APPROVAL` is recorded in `history.jsonl`. Both `site.css` files were read in full (installation whole file;
Control Plane its own part, the shared part being identical per the implementation report's `diff`, and swept by the
pattern search in §4).

## 3. Security-Relevant Scope

- Installation host and Control Plane host: every page (layout and CSS), including the anonymous ones (sign-in, Control
  Plane first-run setup, error page).
- Anonymous static paths added by this Story: `/lib/bootstrap/css/bootstrap.min.css`, `/lib/bootstrap/LICENSE.txt`,
  `/lib/bootstrap/VERSION.txt` (both hosts), `/img/bootstrap/*.svg` (installation).
- Assets: the CSP and security headers (v95, SC-15), the integrity of third-party code served to users, antiforgery tokens
  in forms, and the visibility of notices that tell a user about read-only mode or a data problem.
- Trust boundaries: browser to each host; developer environment to repository (third-party file supply chain).

## 4. Environment and Tools

.NET SDK 10.0.401; Docker 29.8.0 running (Testcontainers). Delegated to one `cheap-worker`, read only: build, full test
run, package vulnerability list, project-file diff. Inline: everything else.

- `dotnet build ClassroomAgent.sln` — exit 0, 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — exit 2: 6990 total, 6989 passed, 1 failed, 0 skipped, 10 m 30 s. Failed:
  `IncrementalSyncHostTests.TheLog_NamesNoSkippedCourse`, `Timed out waiting for 2 'SyncRunCompleted' log event(s); 1 written.`
- `dotnet test --filter FullyQualifiedName~IncrementalSyncHostTests` — exit 0, 5/5.
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` — no vulnerable packages in any of the 7
  projects (nuget.org reachable).
- `git diff` of `*.csproj`, `Directory.*`, `nuget.config`, `*.props`, `*.targets` — empty. `git status` shows no modified
  `.cs`; `git diff --stat` of `src/*/Security/*`, `src/*/Program.cs`, `*.resx`, `src/*/appsettings*`, `src/*/wwwroot/js/*`
  — empty.
- Own script: strip `class` attributes from HEAD and the working copy of all 40 changed views and compare. 37 identical;
  3 differ: the two layouts (one `<link>`), `InstallationStatus/Confirmation` (one `@using`; its button differs only inside
  the class attribute).
- `openssl dgst -sha384` on both `bootstrap.min.css`, `cmp` of the hosts' three files, `git check-attr text`.
- Pattern search in both `site.css`: `url(`, `@import`, `content:`, `expression`, `behavior`, `display: none`, `visibility`,
  `opacity`, `pointer-events`, `z-index`, `position: absolute|fixed`.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed touched. |
| SC-2 Authentication | NOT_APPLICABLE | Sign-in views gained classes only; no Identity, cookie or HSTS code changed. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | No Admin login code changed. |
| SC-4 Authorization | PASS | No endpoint added; new files are static files, anonymous like every `wwwroot` file. Class-strip comparison: no `@Html.AntiForgeryToken()`, `<form>`, `method`, `action` or field line altered. `MarkForm` keeps its POST form and token (`MeetCodes/Index.cshtml:217-223`). Antiforgery and endpoint-enumeration tests green in the full run. |
| SC-5 Read-only mode | NOT_APPLICABLE | No use case touched. Read-only `disabled` attributes on Erasure buttons kept; `.notice` is not hidden by any rule (§8). |
| SC-6 No DB UI | NOT_APPLICABLE | No endpoint added; `/database` changed by CSS only. |
| SC-7 Key | PASS | No configuration changed; credential files git-ignored (`git check-ignore`), not opened. |
| SC-8 Google | NOT_APPLICABLE | No Google code touched. The installation CSP's Google sign-in `form-action` is unchanged. |
| SC-9 Channel | NOT_APPLICABLE | No channel code touched. |
| SC-10 Hygiene | PASS | No log statement added; `VERSION.txt` and `LICENSE.txt` hold only version, URLs, date, hash, licence text (`VERSION.txt` read). |
| SC-11 Audit | NOT_APPLICABLE | No audited action added. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` unchanged. |
| SC-13 Outbound | PASS | Layouts link only root-relative same-host paths. `bootstrap.min.css`: no `@import`; its 23 `url()` values are all `data:` images, none external. `site.css`: the only URLs are the four root-relative SVG paths. SVGs carry no external reference. No CDN. |
| SC-14 Caching | PASS | `NoStoreMiddleware` not modified; `AStaticFile_DoesNotCarryNoStore` and `UnknownFileLikePathTests` green over every new file. |
| SC-15 Headers / vendoring | PASS | `SecurityHeadersMiddleware.cs` unchanged; `TheSentPolicy_IsStrict…` and `EveryStaticFile_CarriesTheSecurityHeaders` green on both hosts; `InlineCodeRuleTests` green; `img-src` has no `data:`, `style-src` no `'unsafe-inline'`. |

## 6. Authentication and Authorization

No authentication or authorization code changed. The Control Plane `InstallationStatus/Confirmation` view reads the
`InstallationStatusTransition` enum its page model already exposes, for the button colour only: a view-level type
reference, no `DbContext` or service call (AD-3 intact).

## 7. Credentials, Key and Google Access

Not touched. No secret-like file among the changed or untracked files. The Dean accounts password fields are restyled by
CSS only; their markup is unchanged apart from classes.

## 8. Sensitive Data Exposure

No response, DTO, view model, log, audit row or export changed. CSS produces no text (no `content:`).

Attempt 2 specific: the rules move or tighten elements, so they were read for what they could hide. Neither `site.css`
contains `display: none`, `visibility`, `opacity`, `z-index` or `pointer-events`. `#database-synchronization > p`
(`width: 0; min-width: 100%`) and the same trick on the Dean accounts notice set the box to the block's width, leaving the
text visible. Two elements are absolutely positioned: the language switcher and the date-form submit button, both
ordinary controls on their own forms; neither covers another control in a browser with `:has()` (the case without it is
OPEN finding `IMPLEMENTATION_VERIFICATION/N-2`: text overlap only, the switcher is a same-origin language POST).

## 9. Input Validation

No input path changed: field names, ids and validation attributes untouched (class-strip comparison).

## 10. API Security

No endpoint added, removed or changed. Content types of new files are framework defaults. The four SVGs were read in full:
only `<svg>`, `<path>`, `<circle>`, no script, event attribute, `href` or `data:` (also pinned by
`EmbeddedImageOverrideTests`).

## 11. Persistence and Configuration

No migration, entity, configuration or `appsettings` change. `.gitattributes` adds `src/*/wwwroot/lib/** -text`, confirmed
effective by `git check-attr` (`text: unset`), so the vendored files keep the bytes the hash is pinned to. No generated
database file or `.xlsx` in the change set. The temporary look-check stand is deleted: the folder does not exist and no
`.cs`, `.cshtml`, `.csproj` or `.json` under `src` or `tests` mentions `LookCheck`.

## 12. Logging, Audit and Telemetry

No logging or audit change. `docs/hooks/tool-usage.jsonl` is git-ignored and not in the change set.

## 13. Dependencies

No NuGet package or project reference added. Third-party code added as a static file: Bootstrap 5.3.8 CSS (MIT), approved
by OD-001 a). SHA-384 of both copies: `sRIl4kxILFvY47J16cr9ZwB07vP4J8+LH7qKQnuqkuIAvNWLzeN8tE5YBujZqJLB`, equal to
`VERSION.txt`; the hosts' three files are byte-identical. No `.js`, `.map` or font shipped. `dotnet list package
--vulnerable` is clean; it covers NuGet only, not the vendored CSS, which carries no executable code under this CSP.

## 14. Security Test Coverage

| Property | Test |
|---|---|
| CSP unchanged, strict | both hosts' `SecurityHeadersResponseTests.TheSentPolicy_IsStrict…` |
| Every new static file has the headers, 200, no `no-store` | `EveryStaticFile_CarriesTheSecurityHeaders`, `AStaticFile_DoesNotCarryNoStore`, `UnknownFileLikePathTests` |
| No `data:` image left for used components; SVGs clean | `EmbeddedImageOverrideTests` |
| Vendored file unmodified, hash pinned, no scripts | `VendoredBootstrapTests` |
| No inline style or script | `InlineCodeRuleTests` |
| Only same-host stylesheets, layout everywhere | `BootstrapLayoutRuleTests`, `InstallationStylesheetLinkTests`, `ControlPlaneStylesheetLinkTests` |

All green in the independent full run. No automated test renders the CSS in a browser; a real CSP violation in the console
is AC-007's manual part (OPEN finding `IMPLEMENTATION/m-1`).

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Tampered or substituted library file | Hash pinned; byte-for-byte via `.gitattributes` | `VendoredBootstrapTests`, independent `openssl`, `git check-attr` | PASS |
| Styling pulls a resource from another site | Same-host links only; CSP `'self'` | §5 SC-13 sweep; CSP tests | PASS |
| Loosening CSP to allow `data:` images | Images replaced by files; CSP unchanged | `SecurityHeadersMiddleware.cs` untouched; CSP tests | PASS |
| SVG used as a script vector | No script or href; `script-src 'self'` | SVGs read; `EmbeddedImageOverrideTests` | PASS |
| Markup edit drops an antiforgery token | Token lines untouched | class-strip comparison; antiforgery tests green | PASS |
| A link-styled action turned into a GET | Stays a POST button (O-1) | `MarkForm` code read; CSS only restyles `.btn` | PASS |
| CSS hides a read-only notice or a warning | No hiding rule exists | §8 pattern search | PASS |

## 16. Repository Hygiene

`google_credentials.json`, `dac-classroom-agent-*.json`, `classroom_cache.db` and `docs/hooks/tool-usage.jsonl` are
git-ignored (not opened). No `.env`, database file, `.xlsx`, log, key or IDE-local config in `git status -uall`. Untracked
files are the Story's artifacts, vendored files, SVGs and new test classes.

## 17. Deviations

Implementation report O-1…O-4 (the Owner's) and D-1…D-5: none has a security effect. O-1 was checked specifically (§15).

## 18. Findings

- **INFO-3** — Informational, TEST_COVERAGE. `IncrementalSyncHostTests.TheLog_NamesNoSkippedCourse` failed once in the full
  run (`Timed out waiting for 2 'SyncRunCompleted' log event(s); 1 written.`, after 32 s) and its class is 5/5 green on
  rerun. It waits for a background sync in a host; this Story changes no `.cs` file, so it cannot be the cause. A
  flaky log-wait test under the load of the full suite; the full suite was not rerun as a whole, so the green count is
  6989 plus this class's rerun. No correction required in this Story.

Local ids INFO-1 and INFO-2 belong to v1 and are closed, see §22.

## 19. Positive Controls

Independently observed: CSP and security-header middleware unchanged and tested on both hosts; no inline code; same-host
assets only; vendored file hash-pinned and line-ending-protected; no script component of Bootstrap shipped; antiforgery
tokens untouched; no rule hides content; no `.cs` or package change.

## 20. Open Decisions

No blocking security Open Decisions were identified (OD-001…OD-003 resolved).

## 21. Review Limitations

Code, configuration and test review plus an independent build and full test run: not a penetration test. The review did
not open the pages in a browser; console CSP messages rest on the Owner's check of five pages and on the automated tests.
The published Bootstrap hash was not fetched live: the review makes no outbound requests beyond the NuGet vulnerability
feed; the implementor's download-and-check is in the implementation report §5. The vendored CSS is not covered by a
vulnerability database.

## 22. Verdict Rationale

Green build, no Critical, Major or Minor finding, every touched SC item PASS, security tests present and passing, no
blocking Open Decision: **PASS**. The one test failure is outside the Story's reach and green on rerun (INFO-3).

OPEN findings of the Story:

- `SECURITY_REVIEW/INFO-1` (`.map` 404) and `SECURITY_REVIEW/INFO-2` (hash from knowledge): this stage's own advisory notes,
  closed `ACCEPTED`. The vendored files are unchanged; INFO-1's behaviour is FR-001.4 as approved, INFO-2's limit is stated
  in §21.
- `IMPLEMENTATION/m-1` (console on five pages), `IMPLEMENTATION_VERIFICATION/m-2` (marks and banner look not recorded),
  `IMPLEMENTATION/INFO-1` (Control Plane sign-out as primary), `IMPLEMENTATION_VERIFICATION/m-1` (banner rule in the Control
  Plane file), `IMPLEMENTATION_VERIFICATION/N-2` (`:has()`): no security effect found, not this stage's to close; left OPEN
  for the Owner at `HUMAN_PR_APPROVAL`.
