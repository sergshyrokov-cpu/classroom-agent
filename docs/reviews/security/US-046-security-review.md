---
artifact_type: security_review
story: US-046
version: 1
status: APPROVED
created_at: 2026-10-13T18:40:00Z
updated_at: 2026-10-13T18:40:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-046-page-search-hint.md
    version: 1
  - path: docs/specifications/US-046-spec.md
    version: 1
  - path: docs/decisions/US-046-open-decisions.md
    version: 2
  - path: docs/designs/api/US-046-api-design.md
    version: 1
  - path: docs/designs/database/US-046-db-design.md
    version: 1
  - path: docs/tests/US-046-test-strategy.md
    version: 1
  - path: docs/tests/US-046-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-046-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: false
runtime_checks: FULL
---

# US-046 Security Review — Page-search hint

## 1. Executive Summary

**Result: PASS.** US-046 is presentation only: one shared partial
(`Views/Shared/_PageSearchHint.cshtml`) rendering a constant, translated line on
nine already-protected installation pages, three translation keys in each of
`uk`/`en`, a constants class and one CSS rule. No endpoint, route, policy,
request input, query, entity, migration, port call, log line, audit event,
cookie or dependency was added or changed. The hint text comes from the
translation files through `IStringLocalizer` and is HTML-encoded by Razor; the
`data-variant` attribute receives only compile-time constants. Build is clean,
the full suite is green (5561/5561) after the one test-list fix recorded in the
implementation report, and no package has a known vulnerability. Two
informational notes, nothing to correct.

## 2. Reviewed Artifacts

See front-matter `inputs`. Spec v1 is APPROVED (HUMAN_SPEC_APPROVAL
2026-10-13T17:44:49Z); open decisions v2 APPROVED (OD-001 a, OD-002 a, both
resolved). API and DB designs are `NOT_APPLICABLE` records; no `openapi.yaml`
and no entity model exist, by design. No input is SUPERSEDED.

## 3. Security-Relevant Scope

- **Host:** installation (Data Plane) only; the Control Plane is untouched.
- **Changed files:** 9 views (`Courses/Card`, `Courses/Index`, `Database/Index`,
  `Journal/Index`, `MeetCodes/Index`, `MeetReports/Index`, `MeetReports/Meeting`,
  `Report/Index`, `Teachers/Index`), `SharedResource.uk/en.resx` (+3 keys each),
  `wwwroot/css/site.css` (+1 rule); new `Views/Shared/_PageSearchHint.cshtml`,
  `Security/PageSearchHintTextKeys.cs`; tests `PageSearchHintTests`,
  `PageSearchHintTranslationTests`, `PageSearchHintTestData`.
- **Assets touched:** none. The hint reads no personal data, no Google data, no
  configuration.
- **Trust boundaries:** browser → installation host only, through pages whose
  authorization is unchanged.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 available (Testcontainers ran).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 5561 total, 5561 passed, 0 failed,
  0 skipped (6m03s).
- `dotnet list ClassroomAgent.sln package --vulnerable` — no vulnerable package
  in any of the 7 projects (sources: nuget.org, local cache).
- Delegated to a `cheap-worker` subagent (read only): the three commands above.
  Diff reading and every judgement were done by this review.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed change. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie-attribute change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | No login path touched. |
| SC-4 Authorization | PASS | No new endpoint or page; the hint is inside nine existing views whose controllers keep their policies. The existing authorization and anonymous-enumeration tests are unchanged and green in the full suite. No new anonymous surface; the partial is not called from the error page or sign-in pages (AC-005 test asserts absence elsewhere). |
| SC-5 Read-only mode | PASS | The hint writes nothing; `ShowingTheHint_WritesNothing_SetsNoNewCookie_AndIsNotRemembered` asserts teaching fingerprint and audit-row count unchanged across two rounds. |
| SC-6 No DB UI | NOT_APPLICABLE | — |
| SC-7 Key | NOT_APPLICABLE | — |
| SC-8 Google | NOT_APPLICABLE | No port call. |
| SC-9 Channel | NOT_APPLICABLE | — |
| SC-10 Hygiene | PASS | Text is a fixed translation, HTML-encoded (`@T[...]`, `IStringLocalizer` in `_ViewImports.cshtml`); no request value is echoed; no log statement added. |
| SC-11 Audit | NOT_APPLICABLE | No audited action introduced (and the test proves no audit row appears). |
| SC-12 Owner | NOT_APPLICABLE | `Contracts`/Control Plane untouched. |
| SC-13 Outbound | PASS | No outbound call, no external script, font or resource; CSS rule is local. |

## 6. Authentication and Authorization

Unchanged. Every page carrying the hint was already behind the fallback
authenticated policy and its Admin/Dean policy; the Story adds no route. The
full suite (including the existing allowed/forbidden-role and anonymous
enumeration tests) is green.

## 7. Credentials, Key and Google Access

Not touched.

## 8. Sensitive Data Exposure

The hint contains no data — only one of three fixed strings per language. No
DTO, view model, log, audit row, export or telemetry change. Test fixtures are
the existing synthetic ones.

## 9. Input Validation

No new input (spec VR-001). The partial's model is a `string` variant supplied
only by compile-time constants in the views; `PageSearchHintTextKeys.TextKey`
throws on any other value, so no request value can reach it.

## 10. API Security

No REST or page endpoint added, removed or changed; API design is
`NOT_APPLICABLE` with the markup hook as its only binding element, which the
implementation matches (`<p id="page-search-hint" class="page-search-hint"
data-variant="…">`).

## 11. Persistence and Configuration

No entity, migration or configuration change; model snapshot unchanged.

## 12. Logging, Audit and Telemetry

No logger call or audit write added. Hook telemetry untouched.

## 13. Dependencies

No package or project reference added. Vulnerability scan: clean.

## 14. Security Test Coverage

| Property | Test |
|---|---|
| Hint writes nothing, sets no new cookie, stores nothing | `PageSearchHintTests.ShowingTheHint_WritesNothing_SetsNoNewCookie_AndIsNotRemembered` |
| Hint absent on every other page incl. Control Plane | `PageSearchHintTests` AC-005 tests |
| Text comes from translation files in both languages | `PageSearchHintTranslationTests` |
| Existing authorization unchanged | existing authorization suites, green in the full run |

The test deviation (TempData cookie added to the allowed list) was reviewed:
the cookie is set by `DatabaseController` since US-019/US-024, not by the hint;
the assertion still rejects any other cookie. This does not weaken a security
test, and the Owner approved it at IMPLEMENTATION.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Inject markup through the hint | Fixed text, HTML-encoded; variant from constants | `_PageSearchHint.cshtml`, `_ViewImports.cshtml` | PASS |
| Reach the hint anonymously | Pages keep their policies; no new route | unchanged controllers, green suite | PASS |
| Use the hint to change state in read-only mode | Hint is pure rendering | write/audit fingerprint test | PASS |

## 16. Repository Hygiene

`classroom_cache.db`, the root `.xlsx` exports and both live credential files
remain git-ignored (`git status --ignored`; contents not opened). No secret-like
or generated file is among the Story's changes.

## 17. Deviations

None from security requirements. The one implementation deviation (test cookie
list) is covered in §14.

## 18. Findings

- **INFO-1 (Informational, OTHER)** — `src/ClassroomAgent.Web/Security/PageSearchHintTextKeys.cs`
  sits in the `Security` namespace although it holds no security logic. It
  follows the existing `*TextKeys` placement there (`CoursesTextKeys`,
  `MeetCodesTextKeys`, …), so no correction is required; noted for a future
  tidy-up of that namespace.
- **INFO-2 (Informational, OTHER)** — `PageSearchHintTextKeys.TextKey` throws
  `ArgumentOutOfRangeException` on an unknown variant. Only constants are passed
  today, so it is unreachable from a request; if a variant ever comes from input,
  it must be validated first (AD-9, SC-10).

## 19. Positive Controls

- Razor HTML-encoding through `IStringLocalizer` (not `IHtmlLocalizer`, no
  `Html.Raw`).
- No new route, input, cookie, write, log or outbound call — verified by diff
  and by the write/cookie test.
- Hint absent from pages outside the nine (AC-005), including the Control Plane.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

Code, configuration and test review plus a full automated run; no penetration
testing. Visual rendering (size, phone wrapping) is a human check, not a
security property.

## 22. Verdict Rationale

Green build and suite, no Critical/Major/Minor findings, every touched SC item
PASS, no open security decision. Verdict **PASS**; next stage
`HUMAN_PR_APPROVAL`.
