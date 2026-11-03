---
artifact_type: ac_test_matrix
story: US-072
version: 1
status: DRAFT
created_at: 2026-11-03T20:40:15Z
updated_at: 2026-11-03T20:40:15Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-072-control-plane-error-page-without-switcher.md
    version: null
  - path: docs/specifications/US-072-spec.md
    version: 1
  - path: docs/tests/US-072-test-strategy.md
    version: 1
supersedes: null
---

# US-072 Acceptance Criteria to Test Matrix

Test class, unless stated otherwise: `ClassroomAgent.Tests.ControlPlane.Security.OwnerErrorPageHeaderTests`. All tests are integration level. Status is the state before IMPLEMENTATION.

| Acceptance Criterion | Scenario | Test Method | Cases | Expected Result | Status |
|---|---|---|---|---|---|
| AC-001 (a) | Antiforgery refusal on `POST /installations` and `POST /account/language` with a marker in the query | `TheAntiforgeryRefusal_LeavesTheMarkerNowhere` | 2 | `400`; "page expired" text; the one link leads to `/`; no marker, switcher, form or token | Red, shows the defect |
| AC-001 (b) | Refused language code `de` or a marker of its own, with a marker in the query | `ARefusedLanguageCode_LeavesTheMarkerNowhere` | 2 | As above, and the refused code is not echoed | Red, shows the defect |
| AC-002 | `GET` answered `404`, on an unmatched address and on an unknown installation card, with a marker in the query | `ANotFoundAnswer_LeavesTheMarkerNowhere` | 2 | `404`; "not found" text; no marker, switcher, form or token | Green, guard (spec I-2) |
| AC-003 | An Owner who chose English, refused by antiforgery and by language code | `TheRefusal_StaysInTheOwnersLanguage` | 2 | `400`; `html lang="en"`; English text; no marker, switcher, form or token | Red, shows the defect |
| AC-004 | An ordinary page with a marker in the query | `AnOrdinaryPage_KeepsItsSwitcher_AndItsFallback` | 1 | `200`; switcher; return path is the raw path and query | Green, guard |
| AC-004, FR-002 | A form refused by validation, `400` under a protected endpoint | `AFormRefusedByValidation_KeepsItsSwitcher` | 1 | `400`; switcher stays | Green, guard |
| AC-004 | The Owner's pages and the switcher's whole behaviour | `OwnerUiLanguageTests` (all, file unchanged) | 21 | All green, no change to the file | Green |

Requirements with no test, by the Specification's own decision: FR-005 (a comment, checked by reading at IMPLEMENTATION_VERIFICATION; spec section 13).
