---
artifact_type: security_review
story: US-072
version: 1
status: APPROVED
created_at: 2026-11-03T21:14:00Z
updated_at: 2026-11-03T21:14:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-072-control-plane-error-page-without-switcher.md
    version: null
  - path: docs/specifications/US-072-spec.md
    version: 1
  - path: docs/decisions/US-072-open-decisions.md
    version: 1
  - path: docs/designs/api/US-072-api-design.md
    version: 1
  - path: docs/designs/database/US-072-db-design.md
    version: 1
  - path: docs/tests/US-072-test-strategy.md
    version: 1
  - path: docs/tests/US-072-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-072-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-072-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 0
security_sensitive: false
runtime_checks: FULL
---

# US-072 Security Review

## 1. Executive Summary

**PASS: no finding of any severity.** The change removes a reflection of the request's address from the Control Plane's error page. It adds no input, endpoint, policy, text, package or schema. An independent build, the full test suite and a package vulnerability scan are green.

The principal control is one view condition, `ViewData.Model is ErrorPageModel`. It was read in the code and proved by requests in `OwnerErrorPageHeaderTests`.

Limitation: this is a code, configuration and test review, not a penetration test. The red phase was not rerun; section 21 says why.

**Delegated** to `cheap-worker`: the build, the vulnerability scan, the package and configuration diff and the full test run, reported verbatim. All code reading and judgement were done in this stage.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| User Story | `docs/stories/US-072-control-plane-error-page-without-switcher.md` | current |
| Specification | `docs/specifications/US-072-spec.md` | 1, APPROVED |
| Open Decisions | `docs/decisions/US-072-open-decisions.md` | 1, none open |
| API design | `docs/designs/api/US-072-api-design.md` | 1, NOT_APPLICABLE, no OpenAPI file by decision |
| Database design | `docs/designs/database/US-072-db-design.md` | 1, NOT_APPLICABLE, no entity model by decision |
| Test strategy | `docs/tests/US-072-test-strategy.md` | 1 |
| AC test matrix | `docs/tests/US-072-ac-test-matrix.md` | 1 |
| Test generation report | `docs/evidence/US-072-test-generation-report.md` | 1 |
| Implementation report | `docs/evidence/US-072-implementation-report.md` | 1 |
| Implementation verification | `docs/reviews/implementation/US-072-implementation-verification.md` | 1, PASS |

No input is superseded or stale. `HUMAN_SPEC_APPROVAL` is recorded in `history.jsonl`.

## 3. Security-Relevant Scope

- **Host:** Control Plane only. The installation does not change.
- **Changed code:** `src/ClassroomAgent.ControlPlane/Views/Shared/_LanguageSwitcher.cshtml`: one added condition and a reworded comment.
- **Added test:** `tests/ClassroomAgent.Tests/ControlPlane/Security/OwnerErrorPageHeaderTests.cs`.
- **Assets:** the address of a refused request, which may hold whatever a caller put in its query. No teaching data, credential or key is touched.
- **Trust boundary:** the Owner's browser to the Control Plane (private network, DC-6).

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0, running; Testcontainers available.
- Free RAM before the run: 5.8 GB.

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` | 0 | no vulnerable package in any of the 7 projects, against api.nuget.org |
| `git diff --stat HEAD -- "*.csproj" "Directory.*.props" "*.json"` | 0 | empty |
| `dotnet test ClassroomAgent.sln` | 0 | 7571 total, 7571 passed, 0 failed, 0 skipped, 11m 45s |

The total equals the implementation report's 7571.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed in the diff |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie code changed. Only the form on the error page is gone |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Not touched |
| SC-4 Authorization | PASS | No endpoint, policy, attribute or anonymous entry in the diff. The error page holds only translated text and a fixed back link: `Views/Error/Index.cshtml`, `ErrorController.Page`. The antiforgery filter and its refusal are unchanged and asserted by status and text. The switcher's form still carries its token on ordinary pages |
| SC-5 Read-only mode | NOT_APPLICABLE | The Control Plane has none; no write use case changed |
| SC-6 No DB UI | NOT_APPLICABLE | Not touched |
| SC-7 Key | NOT_APPLICABLE | Not touched |
| SC-8 Google | NOT_APPLICABLE | Not touched |
| SC-9 Channel | NOT_APPLICABLE | No channel or `Contracts` change |
| SC-10 Hygiene | PASS | The raw query no longer reaches the error page on any path. The refused language code is neither echoed nor logged: `UiLanguageController:31-34`, `ARefusedLanguageCode_LeavesTheMarkerNowhere`, and the existing `AnInvalidCode_IsRefused_NotStored_NotLogged`, still green. No logging change |
| SC-11 Audit | NOT_APPLICABLE | No audited action added or changed |
| SC-12 Owner | NOT_APPLICABLE | No reference or `Contracts` change |
| SC-13 Outbound | NOT_APPLICABLE | No outbound call |
| SC-14 No-store, SC-15 Headers | PASS | Set by host-wide rules not changed here. The change adds no inline code, style or resource |

## 6. Authentication and Authorization

No access rule changes. The new condition can only remove the switcher, never add it: it is joined by `&&` to the two existing conditions, so an anonymous visitor still gets none. The condition reads the server-side view model, which the request cannot set: the model is built only in `ErrorController.Page`.

**Every path to the page is covered.** A sweep of the Control Plane finds `ErrorPageModel` built in one place and `~/Views/Error/Index.cshtml` named in one place, and `ErrorController.Page` called only from `GlobalAntiforgeryFilter:36`, `UiLanguageController:33` and `ErrorController.Index`. `_LanguageSwitcher` is rendered only from `_Layout.cshtml`, through `Html.PartialAsync` with no model of its own, so it sees the page's model. The refusal tests prove this at runtime.

## 7. Credentials, Key and Google Access

Not touched. With the switcher gone, the error page no longer issues an antiforgery token, as spec section 7 states. Nothing on the page posts; the next ordinary page issues its own token.

## 8. Sensitive Data Exposure

- **Views:** after the fix, `Context.Request` values reach Control Plane markup only through `returnPath` on an ordinary page, HTML-encoded by Razor. A sweep of `Views` finds no `Html.Raw` or `innerHTML`.
- **Back link:** `/` or the fixed `/setup` and `/sign-in`, never a value from the query. `GlobalAntiforgeryFilter.BackLink` compares the path with those two constants.
- **Logs, audit, exports, telemetry:** no change.

The fallback on ordinary pages stays by the Story's scope and spec FR-004. The Owner decided this; it is not a finding (risk assessed in US-071 SECURITY_REVIEW INFO-2).

## 9. Input Validation

No input is added or changed. The refused code and the return path keep the US-039 validation. `LocalReturnPath` is unchanged, so an open redirect through `returnPath` stays closed.

## 10. API Security

No endpoint, method, status code or response contract changes, which confirms the API design's NOT_APPLICABLE. `/api/v1` is not touched.

## 11. Persistence and Configuration

No entity, migration or configuration change. The configuration and project-file diff is empty.

## 12. Logging, Audit and Telemetry

No log statement or audit event changes. `docs/hooks/tool-usage.jsonl` remains git-ignored (`.gitignore:66`).

## 13. Dependencies

No package or project reference was added. The vulnerability scan lists nothing for the current sources.

## 14. Security Test Coverage

| Requirement | Test | Status |
|---|---|---|
| No raw query after an antiforgery refusal | `TheAntiforgeryRefusal_LeavesTheMarkerNowhere` (2) | Pass; red before the fix per the test generation report |
| No raw query or refused value after a refused code | `ARefusedLanguageCode_LeavesTheMarkerNowhere` (2) | Pass; red before the fix per the same report |
| No raw query on a `404` | `ANotFoundAnswer_LeavesTheMarkerNowhere` (2) | Pass; a guard |
| No form and no token on the error page | `AssertBareErrorPage`, in all of the above | Pass |
| The language stays, with no switcher | `TheRefusal_StaysInTheOwnersLanguage` (2) | Pass; red before the fix per the same report |
| The rule follows the page, not the status | `AFormRefusedByValidation_KeepsItsSwitcher` | Pass |

The fixtures are synthetic, and no live Google or Control Plane service is called. TC-5 adds no requirement, because no endpoint was added.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A crafted link puts text in the query of a refused `POST` | The text does not reach the page | Marker tests above | PASS |
| A tampered language code is reflected | Neither echoed nor logged | `ARefusedLanguageCode_LeavesTheMarkerNowhere`, `AnInvalidCode_IsRefused_NotStored_NotLogged` | PASS |
| The refused path steers the back link | The link holds only fixed values | `GlobalAntiforgeryFilter.BackLink`; `Assert.Equal([Home], ...)` | PASS |
| A request forces the switcher on, or an anonymous visitor gets it | The rule reads server state only | The view condition | PASS |

## 16. Repository Hygiene

`git status` shows only the Story's code, test and workflow files. No database, `.xlsx`, `.env`, key or `appsettings` file is changed, and no such file is tracked. `google_credentials.json` and `classroom_cache.db` are git-ignored, confirmed by `git check-ignore` without opening them. The credential files were not opened.

## 17. Deviations

None from an approved security requirement. The rule is a view condition, not a class as in the installation; spec FR-002 asks for the rule and its home, not a class, and the verification reached the same conclusion.

## 18. Findings

None.

## 19. Positive Controls

- The error page holds translated text and a fixed link only, and no form.
- The rule recognises the page by its server-side model, so a later caller of `ErrorController.Page` cannot forget a flag.
- A refused value is neither echoed nor logged.
- Razor encodes the fallback that stays on ordinary pages.
- The antiforgery filter and return-path validation are unchanged.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- **Red phase not rerun.** This stage does not edit code. It rests on the test generation report, whose failure messages name the exact hidden field, and on the diff.
- **Not looked at in a browser.** No Acceptance Criterion asks for it, and an empty header has no security effect.
- **Not a penetration test.** No request was sent other than those in the test suite.

## 22. Verdict Rationale

The implementation report records a green build and tests with concrete output, and this stage reproduced them independently. There is no Critical, Major or Minor finding, every touched SC item is PASS, and no Open Decision is open.

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-072
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-072-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings: []
  closed_findings: []
```
