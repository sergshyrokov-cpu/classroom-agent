---
artifact_type: implementation_verification
story: US-072
version: 1
status: APPROVED
created_at: 2026-11-03T20:59:19Z
updated_at: 2026-11-03T20:59:19Z
produced_by: implementation-verifier
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
  - path: docs/evidence/US-072-test-generation-report.md
    version: 1
  - path: docs/evidence/US-072-implementation-report.md
    version: 1
supersedes: null
---

# US-072 Implementation Verification

## 1. Summary

**PASS: no Critical, Major or Minor finding.** The code does what the approved Specification says, the tests prove the Acceptance Criteria, and the implementation report matches the working tree. The change is one condition and a reworded comment in `_LanguageSwitcher.cshtml`, plus one new test class.

**Delegated.** One run of each of the Story's two test classes went to `cheap-worker`. All reading and judgement were done in this stage.

**Not reproduced here.** The red phase was not rerun, because this stage does not edit code. It rests on the test generation report, whose failure messages name the exact hidden field and value, and on the diff, whose only behavioural change is the condition those failures depend on.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| User Story | `docs/stories/US-072-control-plane-error-page-without-switcher.md` | current |
| Specification | `docs/specifications/US-072-spec.md` | 1, APPROVED |
| Open Decisions | `docs/decisions/US-072-open-decisions.md` | 1, none open |
| API design | `docs/designs/api/US-072-api-design.md` | 1, NOT_APPLICABLE |
| Database design | `docs/designs/database/US-072-db-design.md` | 1, NOT_APPLICABLE |
| Test strategy | `docs/tests/US-072-test-strategy.md` | 1 |
| AC test matrix | `docs/tests/US-072-ac-test-matrix.md` | 1 |
| Test generation report | `docs/evidence/US-072-test-generation-report.md` | 1 |
| Implementation report | `docs/evidence/US-072-implementation-report.md` | 1; its inputs name the current versions |

## 3. AC Verification

Test class, unless stated otherwise: `OwnerErrorPageHeaderTests`.

| AC | Test | Why it proves the criterion | Verdict |
|---|---|---|---|
| AC-001 (a) | `TheAntiforgeryRefusal_LeavesTheMarkerNowhere` (2) | A signed-in Owner posts with no token to `/installations` and `/account/language`, both protected, with the marker in the query. Asserts `400`, the page-expired text, the single link to `/`, and no marker, switcher, `aria-current`, form or token. The token is withheld explicitly, so the refusal is the filter's | Proved |
| AC-001 (b) | `ARefusedLanguageCode_LeavesTheMarkerNowhere` (2) | A valid token and a refused code reach `UiLanguageController`'s `ErrorController.Page` call. Same assertions, and the refused value is not echoed | Proved |
| AC-002 | `ANotFoundAnswer_LeavesTheMarkerNowhere` (2) | An unmatched address and an unknown card, each with the marker. Asserts `404` and the not-found text, so the error page is really shown. A regression guard, green before the fix by spec I-2, and the matrix says so | Proved as a guard |
| AC-003 | `TheRefusal_StaysInTheOwnersLanguage` (2) | The Owner chooses English through the real form, then is refused both ways under a protected endpoint. Asserts `html lang="en"`, the English text and a bare page | Proved |
| AC-004 | `AnOrdinaryPage_KeepsItsSwitcher_AndItsFallback`, `AFormRefusedByValidation_KeepsItsSwitcher`, `OwnerUiLanguageTests` (21) | The switcher and the raw path and query fallback stay on an ordinary page; a validation `400` keeps the switcher, which rejects a rule built on the status code. `OwnerUiLanguageTests.cs` has no change in `git status` | Proved |

**The "no switcher" assertions are not vacuous.** `HasSwitcher` looks for `action="/account/language"`. The ordinary-page tests assert the same helper true on the same host and session, and the page always offers one form, for the language not in use.

## 4. Specification Coverage

| Requirement | Evidence | Verdict |
|---|---|---|
| FR-001 | `ErrorController.Page` has exactly two callers outside its controller, `GlobalAntiforgeryFilter:36` and `UiLanguageController:33`, as the spec says. Every path builds `ErrorPageModel`, and `~/Views/Error/Index.cshtml` is named only there | Met |
| FR-002 | `ViewData.Model is ErrorPageModel` is the same test as the installation's `PageHeaderRules.IsErrorPage`; the three conditions are joined as specified; no flag exists; the code is in the Control Plane | Met |
| FR-003 | `ErrorController`, the filter, the language controller and the error view are unchanged; the tests assert status, text, link and language | Met |
| FR-004 | Only the added condition changes the switcher's logic; `returnPath` is unchanged; sign-in and setup stay covered by `AnonymousPages_HaveNoSwitcher_AndStayUkrainian` | Met |
| FR-005 | Read: the comment states the rule, cites US-072 FR-002, and carries the installation's reason in nearly the same words | Met |
| Section 7 | No form or token on the error page, asserted. No policy, audit or outbound change in the diff | Met |
| Section 9 | No translation entry and no new markup | Met |

No behaviour outside the artifacts was found.

## 5. Design Conformance

- **API and database:** both NOT_APPLICABLE and confirmed: no route, status code, entity or migration in the diff. The change brings the code into line with US-039's `x-ui-elements.LanguageSwitcher.not-rendered-on`, which already lists the error page of both hosts.
- **AD-3, AD-8:** the condition is a rendering decision in presentation code, beside the one already there. `ErrorPageModel` is a view model, not an entity. No `DbContext` is touched.
- **AD-4, AD-6:** not touched. The Control Plane has no read-only mode.

## 6. Deviations

None from an approved artifact, which agrees with the implementation report's section 7. Two items that the report discloses were checked:

| Item | Checked against | Verdict |
|---|---|---|
| The rule is a condition in the view, not a class like `PageHeaderRules` | Spec FR-002 requires the rule and its home in the Control Plane's own code, not a class. The installation's class exists because two controls share the rule; here there is one | Sound, not a deviation |
| The header element on the error page is now empty, and this was not looked at in a browser | Spec section 9 expects an empty header, as on the sign-in pages. The layout renders the same empty `<header>` on those pages | Sound; no AC asks for a look check |

## 7. Report Accuracy

- **Change set:** the report's two files equal the diff's code and test files. The other changes are on the closed list in the Git Policy (workflow state and this Story's artifacts).
- **Trace:** both files are traced. All test names in section 3 of the report exist, and the count of 10 cases is right (2+2+2+2+1+1).
- **Arithmetic:** 7561 + 10 = 7571, as reported. The full suite was not rerun here; `security-reviewer` runs it.

**Clean change.** Ripgrep over the two changed files finds no `TODO`, `TBD`, `FIXME`, `NotImplementedException` or skipped test.

## 8. Story Test Run

Run by `cheap-worker`, reported verbatim; Docker was running.

| Command | Exit | Total | Passed | Failed | Skipped | Duration |
|---|---|---|---|---|---|---|
| `dotnet test --filter FullyQualifiedName~OwnerErrorPageHeaderTests` | 0 | 10 | 10 | 0 | 0 | 10s 042ms |
| `dotnet test --filter FullyQualifiedName~OwnerUiLanguageTests` | 0 | 21 | 21 | 0 | 0 | 11s 758ms |

## 9. Findings

None.

## 10. Verdict

**PASS.** Next stage: SECURITY_REVIEW.

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION_VERIFICATION
  story: US-072
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/implementation/US-072-implementation-verification.md
  next_stage: SECURITY_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings: []
  closed_findings: []
```
