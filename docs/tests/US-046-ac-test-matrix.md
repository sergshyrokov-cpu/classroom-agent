---
artifact_type: ac_test_matrix
story: US-046
version: 1
status: DRAFT
created_at: 2026-10-13T18:03:08Z
updated_at: 2026-10-13T18:03:08Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-046-spec.md
    version: 1
  - path: docs/designs/api/US-046-api-design.md
    version: 1
  - path: docs/designs/database/US-046-db-design.md
    version: 1
  - path: docs/tests/US-046-test-strategy.md
    version: 1
supersedes: null
---

# US-046 Acceptance Criteria → Test Matrix

Classes: **P** = `ClassroomAgent.Tests.Web.Pages.PageSearchHintTests`
(integration), **T** = `ClassroomAgent.Tests.Web.Localization.PageSearchHintTranslationTests`
(source). Status = expected state before IMPLEMENTATION (red phase).

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Ordinary pages (card, journal, report, Meet reports, meeting, database), Admin & Dean, uk | Integration | P | `EveryTeachingDataPage_ShowsItsHintOnce_DirectlyUnderTheTitle` | one `ordinary` hint, FR-003 a text | Red |
| AC-001 | Same pages in English | Integration | P | `InEnglish_EveryTeachingDataPage_ShowsTheEnglishText` | FR-003 a en text | Red |
| AC-002 | Course list shows `course-list` variant (uk, en) | Integration | P | `EveryTeachingDataPage_…`, `InEnglish_…` | FR-003 b text | Red |
| AC-002 | Quoted name = `Courses.Form.Search` in both languages | Integration | P | `TheCourseListHint_QuotesTheSearchFieldLabel` | «label» / "label" in the hint | Red |
| AC-003 | Teacher list and Meet codes show `paged-list` (uk, en) | Integration | P | `EveryTeachingDataPage_…`, `InEnglish_…` | FR-003 c text | Red |
| AC-004 | Both roles | Integration | P | `EveryTeachingDataPage_…` (Admin, Dean) | same hint | Red |
| AC-004 | Read-only mode | Integration | P | `InReadOnlyMode_TheHintIsUnchanged` | same hint | Red |
| AC-004 | No rows at all | Integration | P | `WithNoRowsAtAll_TheHintIsShown` | same hint | Red |
| AC-004 | One page vs many pages | Integration | P | `ThePagedListHint_IsTheSame_OnOnePage_AndOnEveryPageOfMany` | same text on all | Red |
| AC-004 | Page with validation messages | Integration | P | `TheJournalWithAValidationMessage_StillShowsTheHint` | hint on 400/404 journal | Red |
| AC-004 | No device detection | Integration | P | `APhoneBrowser_GetsTheSameHint` | identical hint | Red |
| AC-004 | Writes nothing, no new cookie, not remembered | Integration | P | `ShowingTheHint_WritesNothing_SetsNoNewCookie_AndIsNotRemembered` | fingerprint & audit unchanged; only session/antiforgery cookies; hint on 2nd visit | Red |
| AC-004 | No close control / no script | Integration | P | all presence tests (`AssertHint`: leaf `<p>`) | no child markup | Red |
| AC-005 | Other installation pages incl. course-choice (OD-001 a), error page, sign-in | Integration | P | `NoOtherInstallationPage_HasTheHint` | no hook on any | Green (guard; control presence Red) |
| AC-005 | Forced password change | Integration | P | `TheForcedPasswordChange_HasNoHint` | no hook | Green (guard) |
| AC-005 | Control Plane | Integration | P | `NoControlPlanePage_HasTheHint` | no hook | Green (guard) |
| AC-006 | Texts in the translation files, both languages | Source | T | `EachHintText_IsAnEntryOfTheTranslationFileOfItsLanguage` | six values present | Red |
| AC-006 | No literal in any view | Source | T | `NoView_HoldsTheHintTextAsALiteral` | no fragment in `.cshtml` | Green (guard) |
| AC-006 / S-03 | Not rendered raw | Source | T | `TheHint_IsNeverRenderedRaw` | no `Html.Raw` in a view rendering the hook | Green (guard) |
| AC-006 / S-03 | Encoded text | Integration | P | all presence tests (decoded text equals FR-003) | equal | Red |
| AC-007 | Existing suite stays green; titles unchanged | Integration | whole suite; P placement (both Meet codes titles present) | `dotnet test ClassroomAgent.sln` | all green | Green now; must stay green |
| AC-008 | Exactly one hint of the assigned variant | Integration | P | all presence tests (`Assert.Single`, variant) | one element | Red |
| AC-009 | Hint first after the title (Meet codes: own h1) | Integration | P | all presence tests (`StartsWithHint`) | first element | Red |
| I-3 | Small style via class | Source | T | `TheStylesheet_StylesTheHintClass` | `.page-search-hint { font-size … }` | Red |

Every Acceptance Criterion (AC-001 … AC-009) has at least one mapped test.
