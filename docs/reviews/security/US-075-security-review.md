---
artifact_type: security_review
story: US-075
version: 2
status: APPROVED
created_at: 2026-11-07T19:59:12Z
updated_at: 2026-11-07T22:41:52Z
produced_by: security-reviewer
attempt: 2
inputs:
  - path: docs/stories/US-075-data-problem-hints.md
    version: null
  - path: docs/specifications/US-075-spec.md
    version: 2
  - path: docs/decisions/US-075-open-decisions.md
    version: 2
  - path: docs/designs/api/US-075-api-design.md
    version: 1
  - path: docs/designs/database/US-075-db-design.md
    version: 1
  - path: docs/tests/US-075-test-strategy.md
    version: 1
  - path: docs/tests/US-075-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-075-implementation-report.md
    version: 2
  - path: docs/reviews/implementation/US-075-implementation-verification.md
    version: 2
supersedes: docs/reviews/security/US-075-security-review.md@v1
verdict: PASS
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: false
runtime_checks: FULL
---

# US-075 Security Review (attempt 2)

## 1. Executive Summary

**PASS — 0 Critical, 0 Major, 0 Minor, 3 Informational.**

Attempt 2 follows the Owner's rejection at `HUMAN_PR_APPROVAL` (R-1, R-2: banner line breaking at 360 px). The
attempt-2 change is markup and CSS only:

- `_Layout.cshtml`: the first banner part and the separator's comma are wrapped in
  `<span class="data-health-banner-lead">`;
- `site.css`: `.data-health-banner-lead { white-space: nowrap; }` and a phone-width rule
  (`max-width: 575.98px`) that drops the banner's right reserve.

Everything reviewed in v1 still holds and was re-checked against the current working tree:

- no endpoint, policy, use case, persistence, Google access, audit or outbound change;
- SEC-002 (no technical detail for a Dean) holds in two layers — `GetDataHealthQuery` strips `Details` for a
  non-Admin, and the view renders the FR-003 hints only under `Model.IsAdmin`;
- every new fragment is HTML-encoded; no script and no inline style (SC-15).

The independent full suite is green: **7838/7838**.

## 2. Reviewed Artifacts

As in the front matter `inputs`. `HUMAN_SPEC_APPROVAL` is recorded in `history.jsonl` at 2026-11-07T18:45:53Z. The
`HUMAN_PR_APPROVAL` rejection is recorded at 2026-11-07T22:03:20Z. No input is `SUPERSEDED`; v1 of this review was made
against implementation report v1 and is superseded by this version.

## 3. Security-Relevant Scope

**Host:** Data Plane (`ClassroomAgent.Web`) only.

**Changed production files (whole Story):**

- `Views/Shared/_Layout.cshtml`: the banner, its `BannerPart` function, and (attempt 2) the lead span;
- `Views/DataHealth/Index.cshtml`: the hints, the `archived` parameter, the Dean's X4 title;
- `Security/DataHealthTextKeys.cs`: constants and `Hint(kind)`;
- two `SharedResource.*.resx` (8 entries each);
- `wwwroot/css/site.css` (attempt 2 adds two rules).

**Assets:** the technical details of data problems (Admin-only). Course names and grades on the page are unchanged.

**Trust boundaries:** browser → installation host. No new boundary is crossed.

## 4. Environment and Tools

- .NET SDK 10.0.401. Docker 29.8.0 (Testcontainers available). Free RAM 5.9 GB at start.
- `dotnet test ClassroomAgent.sln` (builds the solution): exit 0, **7838 passed / 0 failed / 0 skipped**,
  12 min 36 s, 0 warnings in the log, no Docker/WSL timeouts.
- `dotnet list ClassroomAgent.sln package --vulnerable`: no vulnerable packages in any of the 7 projects (nuget.org).
- `git diff`, `git status`, `git check-ignore`, `git ls-files`, and pattern sweeps (`Html.Raw`, `<script`, `style=`,
  `AllowAnonymous`, `IgnoreAntiforgery`, logger calls) over the changed views and `DataHealthTextKeys.cs`: no hit.
- **Delegation:** none. The change set is small; every check ran inline.

On the count: 7838 is one more than attempt 1's 7837. It matches the untracked `LookCheck/LookCheckStand.cs`, a
`[Fact]` that returns at once unless `LOOKCHECK=1`, so it is counted as passed. See INFO-3.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role, policy or seed touched; hint selection uses the existing `IsAdmin` flag |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie code changed |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Not touched |
| SC-4 Authorization | PASS | No endpoint, page, policy or antiforgery change; banner links are the protected `SignInRoutes.DataHealth` plus a constant fragment; the X5 form keeps `@Html.AntiForgeryToken()` |
| SC-5 Read-only mode | PASS | No write use case added; the X5 hint only when `Model.ReadOnly is null`; `InReadOnlyMode_TheX5Problem_HasNoHint` |
| SC-6 No DB UI | NOT_APPLICABLE | Not touched |
| SC-7 Key | NOT_APPLICABLE | Not touched |
| SC-8 Google | PASS | No Google call; `ThePageWithEveryHint_UsesNoScript_CallsNoGoogleApi_AndWritesNoAuditEvent` |
| SC-9 Channel | NOT_APPLICABLE | Not touched |
| SC-10 Hygiene | PASS | Hints are fixed resx texts with no personal data (SEC-003); no new log statement |
| SC-11 Audit | PASS | No audited action introduced; the test above asserts no audit event |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` untouched |
| SC-13 Outbound | PASS | No outbound call, no external resource |
| SC-15 CSP (SEC-004) | PASS | No `<script>`, no `style=`; both attempt-2 rules sit in the vendored `site.css` |

## 6. Authentication and Authorization

- Access to the page and the banner is unchanged: Admin and Dean, deny by default (SEC-001).
- **SEC-002, layer 1 (data):** `GetDataHealthQuery` sets `Details = null` on every technical problem for a non-Admin.
  The X5 hint and form sit inside the `CoursesNotVisible when details.NotVisibleCourses is { }` case, so a Dean's
  model cannot reach them.
- **SEC-002, layer 2 (view):** the X2a, X2b, X3 and X4 hints and the connection-page link are inside
  `@if (Model.IsAdmin)`. The Dean's X4 title uses `CourseListEmpty.Dean`, with no technical-account wording.
- Attempt 2 did not touch `Index.cshtml` or the query.
- **Tests (green in the full run):** `TheDean_SeesNoTechnicalHint_OnlyTheHintToTellTheAdmin` (×10),
  `TheDeansX4Title_EndsAtItsDate` (×2), `TheAdmin_SeesEachTechnicalKindsHint_AfterItsTitle` (×10).

## 7. Credentials, Key and Google Access

Not touched.

## 8. Sensitive Data Exposure

- The hints are static translated strings.
- **Encoding (re-checked after attempt 2):**
  - `BannerPart` builds the link with `TagBuilder.InnerHtml.Append` and the zero case with
    `HtmlContentBuilder.Append`; both HTML-encode.
  - The slices of the format string — prefix, `separatorGlue`, the remaining separator, suffix — are `string`
    values emitted with `@`, which encodes. The new `<span>` is literal markup with a constant class.
  - The `href` is a constant route plus a constant fragment. No `Html.Raw` added.
- `DataHealthPageModel` is unchanged. Logs, audit, exports and telemetry are unchanged.

## 9. Input Validation

No new input is accepted.

## 10. API Security

No endpoint or contract change; the API design is NOT_APPLICABLE.

## 11. Persistence and Configuration

No entity, migration or configuration change.

## 12. Logging, Audit and Telemetry

- No logger call added.
- `docs/hooks/tool-usage.jsonl` and `docs/hooks/phase-timing.jsonl` are git-ignored.

## 13. Dependencies

No `.csproj` or package change; the vulnerability scan is clean.

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| SEC-001 / SEC-005 | full suite including the endpoint-enumeration tests; `ThePageWithEveryHint_UsesNoScript_CallsNoGoogleApi_AndWritesNoAuditEvent` |
| SEC-002 | `TheDean_SeesNoTechnicalHint_OnlyTheHintToTellTheAdmin`, `TheDeansX4Title_EndsAtItsDate` |
| SEC-003 | `DataHealthHintTranslationTests` (texts verbatim from FR-008) |
| SEC-004 | the no-script test above; `TheStylesheet_KeepsEachBannerLinkOnOneLine` |
| FR-005.3 (read-only) | `InReadOnlyMode_TheX5Problem_HasNoHint`, `InReadOnlyMode_TheHintsThatPromiseNothingBlocked_AreShown` |

The R-1/R-2 line breaking has no automated test (IMPLEMENTATION/INFO-2, IMPLEMENTATION_VERIFICATION/m-2). That is
layout, not a security property.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Dean reads the page source for technical hints | Details stripped in Application; hints Admin-only in the view | §6 | PASS |
| Dean follows a banner link to the technical section | Same page, same policy; only a title and «Повідомте Адміністратора» | `DataHealthBannerTests`, `DataHealthHintsTests` | PASS |
| X5 confirmation in read-only mode prompted by a hint | No hint in read-only mode; the confirmation is refused in Application (US-062, unchanged) | `InReadOnlyMode_TheX5Problem_HasNoHint` | PASS |
| Markup injection through a course name or translation text | Razor encoding of every fragment | §8 | PASS |

## 16. Repository Hygiene

- `google_credentials.json`, `dac-classroom-agent-*.json` and `classroom_cache.db` are git-ignored (confirmed by
  `git check-ignore` only; not opened). No `.db` or `.xlsx` outside `docs/product/report-templates/` is tracked.
- The untracked files are Story artifacts, Story tests, and `tests/ClassroomAgent.Tests/LookCheck/LookCheckStand.cs`.
- **`LookCheckStand.cs`** (the Owner's temporary AC-008 stand) was read in full. It is env-gated (`LOOKCHECK=1`),
  listens on loopback only (`ListenLocalhost`, `IPAddress.Loopback`), seeds synthetic data from the test fixtures,
  and its fake Google consent endpoint exists only inside the test host. It holds no secret: the Dean password is
  the synthetic `JournalTestData.DeanPassword` constant. It is not git-ignored, so it must stay out of the Story
  commit — already raised as IMPLEMENTATION_VERIFICATION/m-1; see INFO-3.

## 17. Deviations

D-1 … D-4 are as in the implementation report and were accepted at verification. None is security-relevant. On D-2,
see INFO-1.

## 18. Findings

### INFO-1 — the banner layout trusts the translation's placeholders (carried from v1, re-checked)

- **Category:** OTHER.
- **File:** `src/ClassroomAgent.Web/Views/Shared/_Layout.cshtml` (banner block).
- **Observation:** attempt 2 adds one more slice, `bannerFormat[(technicalAt + 3)..teachingAt]`, under the same
  assumption: both placeholders present and `{0}` before `{1}`. A translation that breaks this throws on every page
  that shows the banner — a 500 through the single exception handler, no internals in the response. Availability,
  not disclosure.
- **Mitigation present:** both files are correct today; `TheAdminsX4Title_AndTheBannerKeys_AreUnchanged` pins the
  banner keys.
- **Required correction:** none.

### INFO-2 — the X5 hint's Admin-only property rests on the data layer (carried from v1, re-checked)

- **Category:** AUTHORIZATION.
- **File:** `src/ClassroomAgent.Web/Views/DataHealth/Index.cshtml` (X5 case), unchanged in attempt 2.
- **Observation:** the X5 hint is not inside `Model.IsAdmin`. It stays Admin-only because `GetDataHealthQuery`
  never gives a Dean `Details` — the stronger layer — and the Dean test covers it across all kinds.
- **Required correction:** none.

### INFO-3 — the look-check stand is compiled into the suite and counted as a passing test

- **Category:** REPOSITORY_HYGIENE.
- **File:** `tests/ClassroomAgent.Tests/LookCheck/LookCheckStand.cs` (untracked).
- **Observation:** it explains the 7838 vs 7837 count (§4). The file is safe as it stands (§16), but committed it
  would add a no-op test and a test-host Google consent bypass to the repository.
- **Required correction:** none at this stage; delete it or leave it unstaged at the final commit
  (IMPLEMENTATION_VERIFICATION/m-1 stays open for that).

## 19. Positive Controls

- Dean detail stripping in Application; role-gated hints in the view.
- HTML encoding of every new fragment, including the attempt-2 slices.
- No script or inline style.
- Read-only suppression of the X5 hint; antiforgery kept on the X5 form.
- No outbound or audit change, proven by test.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- Code, configuration and test review, not a penetration test.
- The AC-008 browser check (repeated for R-1/R-2) is the Owner's, before `HUMAN_PR_APPROVAL`; it is not security scope.

## 22. Verdict Rationale

**PASS**: the build and the full suite are green; no Critical, Major or Minor finding; every touched SC item is PASS;
SEC-001 … SEC-005 are verified with tests; no open security decision. The three Informational notes need no
correction.
