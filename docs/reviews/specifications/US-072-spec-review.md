---
artifact_type: specification_review
story: US-072
version: 1
status: APPROVED
created_at: 2026-11-03T20:23:20Z
updated_at: 2026-11-03T20:23:20Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-072-control-plane-error-page-without-switcher.md
    version: null
  - path: trebovaniya.md
    version: 109
  - path: docs/specifications/US-072-spec.md
    version: 1
  - path: docs/specifications/US-072-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-072-open-decisions.md
    version: 1
supersedes: null
---

# US-072 Specification Review

## Summary

**PASS.** No Critical or Major findings, two Minor findings.

The Specification's factual claims about today's behaviour were checked against the code and the tests and hold. Its one real tension with `trebovaniya.md` (the back link, I-4) is disclosed in both the Specification and the digest, but it has no home after this Story (m-1).

Nothing was delegated: all sources and code were read directly.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-072-control-plane-error-page-without-switcher.md` | current |
| Requirements | `trebovaniya.md` | 109 |
| Specification | `docs/specifications/US-072-spec.md` | 1 |
| Digest | `docs/specifications/US-072-spec-digest.ru.md` | 1 |
| Open Decisions | `docs/decisions/US-072-open-decisions.md` | 1 |

Inputs are not stale: the Specification names v109, US-039 spec v1 and US-071 spec v2, which are the current versions; the digest names Specification v1.

## Fidelity

Checked against the cited text and the code:

| Item | Source | Result |
|---|---|---|
| FR-001 rows 1, 2: the switcher appears under a protected endpoint | `_LanguageSwitcher.cshtml` (endpoint rule only); `GlobalAntiforgeryFilter`; `UiLanguageController` (`[Authorize]`) | Holds |
| FR-001: these are the only callers of `ErrorController.Page` outside `ErrorController` | Code search | Holds |
| FR-001 rows 3–5: answered under the anonymous `ErrorController` | `Program.cs` `UseStatusCodePagesWithReExecute("/error/{0}")`, `UseExceptionHandler("/error/500")`; `ControlPlaneSecurityServices` (403 by status, then re-execution); `[AllowAnonymous]` on the controller | Holds |
| FR-002: `ErrorPageModel` marks every path; the installation's rule | `ErrorController.Page`; `PageHeaderRules.IsErrorPage`, `ShowsLanguageSwitcher`; US-071 FR-010 | Holds |
| FR-002: the Control Plane has no header search field | `_Layout.cshtml`; US-071 FR-001 | Holds |
| FR-003: status, text, language | §8 v64, v65: translated text, no details, a signed-in user in their language | Holds |
| FR-004: the fallback stays on other pages | Story out-of-scope; installation's `_LanguageSwitcher.cshtml` | Holds |
| FR-005: the comment is false today | `_LanguageSwitcher.cshtml` comment | Holds |
| Section 6: VR-001, VR-002; SC-4, API-7 | US-039 spec; `security-conventions.md`, `api-conventions.md` | Holds |
| Section 7: nothing on the error page posts once the switcher is gone | `_Layout.cshtml` (the switcher is the header's only content), `Views/Error/Index.cshtml` | Holds |
| Section 9: the header is empty, no new text | `_Layout.cshtml`; NFR-073, NFR-076, SC-15 | Holds |
| I-1 | Code search, as FR-001 | Holds |
| I-2: a re-executed `404` has neither switcher nor marker today | Anonymous `ErrorController`; the address is `/error/404` | Holds |
| I-3: an English Owner's page without a switcher already holds on `/error/404` | `AnonymousPages_HaveNoSwitcher_AndStayUkrainian` | Holds. The test uses a direct `GET`, not a re-execution, but both answer under the same endpoint, so the conclusion stands |
| I-5: no test asserts a switcher on an error page under a protected endpoint | `OwnerUiLanguageTests`: `EveryOwnerPage_RendersTheSwitcher` covers three `200` pages; the two refusal tests assert status, text, stored value and logs. No other Control Plane test looks for the switcher | Holds |

## Story Coverage

| Story item | Specification | Result |
|---|---|---|
| Scope 1 | FR-001 | Covered |
| Scope 2 | FR-002 | Covered |
| Scope 3 | FR-005 | Covered |
| Out of scope | Section 10, FR-004 | Covered; the back link's target is added to it, explained by I-4 |
| AC-001 (a, b, red first) | AC-001 | Covered; `400` is added from §8 and FR-003 |
| AC-002 | AC-002 | Covered |
| AC-003 | AC-003, narrowed by I-3 | Covered |
| AC-004 | AC-004 | Covered |

Nothing goes beyond the Story. The Story expects "empty deltas" for API and database design; the Specification's NOT_APPLICABLE is the workflow's form of the same thing (`stage-map.yaml` `optional_when`), and the required explicit statement is in section 13.

## Consistency

- **US-039** I-5 and FR-002 intend no switcher on the error page. This Story completes that on the Control Plane.
- **US-071** FR-010 and I-13: the same choice, removing the switcher rather than cleaning its return path.
- **§8 v64, v65:** the page and its behaviour are the same in both hosts; this Story brings them closer.
- **Permission matrix:** no cell touched. **BR-026:** not touched, the error page writes nothing. **NFR-073:** no new text. **SC-11:** no audit action added. **SC-13:** no outbound data.

## Open Items

No item of `trebovaniya.md` section 7 concerns the error page, its back link or the switcher. The Story and the Specification hold no `OD-` items; the Owner's decision of 2026-11-03 is recorded in the ledger (`US-071/API_DESIGN/m-1`, `US-071/SECURITY_REVIEW/INFO-2`).

## Interpretations

| Place | Choice made | In the digest |
|---|---|---|
| I-1, FR-002 | The rule looks at the page, so it covers all paths, though only two misbehave | Decision 2 |
| I-2 | AC-002 stays as a guard that passes before the fix | Decision 3 |
| I-3 | AC-003 is shown on a refused `POST` | Decision 4 |
| I-4 | "Keeps its back link" means the link it has today, leading to `/` | Decision 1 |
| I-5 | Not a choice: a check of the current tests. New tests go in a new class, a purely technical matter | Not needed |
| Section 7 | A hidden field copied from the request counts as a "detail" of §8. Nothing depends on this reading: the Story already asks for the removal | Not needed |

## Digest Fidelity

Every statement traces to the Specification. All four decisions are named, the empty Open Decisions section is true, the out-of-scope list matches section 10. The digest does not overstate the change and is in plain Russian. It does not mention that API and database design will be skipped; the Owner decides nothing there, so this is not a finding.

## Findings

### m-1 Minor: the back link's divergence from §8 has no home

**Location:** Specification I-4, section 10; digest decision 1. **Checked against:** `trebovaniya.md` §8 (v64) "ссылкой на ту же страницу"; US-001 FR-015; US-008; both hosts' `GlobalAntiforgeryFilter`.

The disclosure is correct and leaving it out of this Story is right. But the divergence is wider than these two refusals: every protected form of **both** hosts links to the home page, not the same page, and §8 has said otherwise since v64. No `trebovaniya.md` section 7 item, finding or Story records it, so once US-072 closes it is known only from an interpretation. The digest presents it as a decision about this Story, and does not tell the Owner that the requirement and the code disagree across the system.

**Suggested handling:** the Owner decides at the gate whether the text of §8 or the code is right, and the answer goes to the next `trebovaniya.md` version or a Story. No change to this Specification is needed.

### m-2 Minor: the rule's own sources are not cited

**Location:** Specification FR-001, section 1. **Checked against:** the Story's front matter and `trebovaniya.md` §8.

FR-001 cites no source. The Specification lists US-039 as an input but never cites its I-5, the origin of "no switcher on the error page", which the Story names. Nor does it cite §8's "текст и поведение в обоих хостах одинаковы", the requirement behind "both hosts follow one rule". The rule is not invented: US-071 FR-010 in section 1 and the Story carry it, so this affects traceability only.

## Verdict

**PASS** — ready for `HUMAN_SPEC_APPROVAL`. A `PASS` here is not human approval.
