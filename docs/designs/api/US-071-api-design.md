---
artifact_type: api_design
story: US-071
version: 1
status: DRAFT
created_at: 2026-11-03T16:46:15Z
updated_at: 2026-11-03T16:46:15Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-071-spec.md
    version: 2
  - path: docs/decisions/US-071-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 109
supersedes: null
---

# US-071 API Design — one search field in the header

**Verdict: PASS.** One new server-rendered page and a delta to the page frame; no JSON API. Contract: `docs/designs/api/US-071-openapi.yaml`.

Delegation: none. The designer read the Specification, `US-070-openapi.yaml`, `US-070-api-design.md`, `api-conventions.md`, and directly: `TeachersController`, `MeetCodesController`, `ErrorController`, `GetTeacherListQuery`, `CourseListUrl`, `TeacherListUrl`, `DataMarksTexts`, `_Layout.cshtml`, both `_LanguageSwitcher.cshtml`, the Courses and Meet code views, `site.css`.

## 1. Decisions (items the Specification left to this stage)

- **D-1 Path: `GET /workspace/search`.** It sits with the other teaching-data pages under `/workspace`. A Razor page, so not `/api/v1` (API conventions, opening note).
- **D-2 Three states.** `Prompt` (no text or too short, `200`, nothing read), `Refused` (VR-001, `400`, nothing read), `Results` (`200`). "Nothing found" is `Results` with all four totals 0, so the view has one check for it.
- **D-3 One header rule, by the view model.** `PageHeaderRules.IsErrorPage` tests `ViewData.Model is ErrorPageModel`. The endpoint cannot tell: `ErrorController.Page` answers under the protected endpoint that failed. Every path to the error page uses that model, so no caller has to remember a flag (FR-010).
- **D-4 The field's value through view data**, as the return path already travels (`ReturnPathViewDataKey`): only the results page sets it, so every other page has an empty field without a rule of its own (FR-001).
- **D-5 Links are built by the existing rules.** `CourseListUrl` and `TeacherListUrl` as they are. `MeetCodesController.ListUrl(list, q)` is new, beside `ListText`, without `page` or `size`, so their defaults apply. The model carries ids and the list, not URLs, except the return path (AD-8; as the Meet code page does).
- **D-6 The student's course link reads `name — section`**, the form the Meet code page already uses. FR-003 says a link to the card without saying its text; two sections of one course would otherwise be two identical links. Section is stored data of the course and adds nothing the Dean does not already see.
- **D-7 The teacher row's link text** is the new key `Search.Teacher.Courses`, «Курси викладача» / "Teacher's courses". FR-004 names the link, not its words, and I-1 does not list them. It is a label, like those of I-1.
- **D-8 The email is not repeated** when it is already the label (a person with no stored name). FR-003 lists both; the row would show the same address twice.
- **D-9 Hint names as placeholders.** `PageSearchHint.Ordinary` gets `{0}`, `PageSearchHint.ListField` gets `{1}`, both filled from `Search.Field.Label`, so "Пошук" in the hint cannot differ from the field (FR-011). The keys stay, so the tests change only where they pin the text.
- **D-10 Log lines as on the teacher list.** Shown: the actor and the four totals. Refused: the trace id, `q` and the key. VR-002 and section 7 permit exactly this; a test asserts the text is in no event.
- **D-11 One policy, `ViewCourses`.** Spec section 7. The Meet group is read under it because both roles hold `ViewMeetCodes`; a later role with one and not the other would need this revisited.
- **D-12 Limit constant.** 20 lives once in the query (`GetSearchResultsQuery.GroupSize`) and is passed to `Search.Limit.Shown` as `{0}`, not written into the text.

## 2. Operations

| Operation | Change | Policy | Spec |
|---|---|---|---|
| `GET /workspace/search` | new page | `ViewCourses` | FR-002 … FR-009, FR-014, VR-001 … VR-004 |
| Every page: `_Layout` header | the field, by `PageHeaderRules` | as each page | FR-001 |
| Error page, under any endpoint | no switcher, no field | as today | FR-010 |
| Every hint page | final texts | as each page | FR-011 |
| `site.css`, both hosts | `.btn:disabled` | static | FR-012 |

FR-013 changes no behaviour and needs no contract.

## 3. Checked and unchanged

- **Return path length.** 100 characters of 3-byte UTF-8 encode to 900, well within the 2048 of `UiLanguageController`.
- **Fallback.** The results page always sets the return path. The lists and course-choice already do (US-070).
- **Sign-in redirect.** `OnRedirectToLogin` emits no return URL, so `q` does not reach the sign-in page.
- **Referrer.** `Referrer-Policy: same-origin` already covers the page.
- **Read-only mode.** No guard: the page only reads. Links lead to pages with their own rules.
- **Audit.** None (SC-11 lists no view).

## 4. Auth model

Deny by default, `ViewCourses`. Both v1 roles are allowed, so the forbidden case of TC-5 is the anonymous redirect and the restricted session, as US-022, US-032 and US-070 test it. An Owner has no installation account. Nothing is added to the anonymous or antiforgery lists.

## 5. Error model

No JSON and no API-6 body. A refused `q` is `400` with the page itself (VR-002). Unexpected failures go to the host's handler, whose error page now has neither the switcher nor the field, with the text in no message.

## 6. Compatibility

- US-039 "rendered only on a page whose endpoint requires sign-in" gains "and not the error page", in line with its own I-5. Tests that saw a switcher on an error page under a protected endpoint, if any, change with it.
- The installation header gains the field, against the older §8 v105 and NFR-076 sentence (I-17). Page-frame tests that compare the header's whole content change with it.
- Hint texts change; `PageSearchHintTestData` and `PageSearchHintTranslationTests` change with them (spec section 13).
- `ContractVersion.Current` stays 1.

## 7. Acceptance Criterion → operation

| AC | Operations |
|---|---|
| AC-001 | `x-page-frame.header-rule`, `search-field`, on every page |
| AC-002 | `getSearchResults` |
| AC-003 | `getSearchResults`, `StudentSearchRow` |
| AC-004 | `getSearchResults`, `x-links` |
| AC-005 | `getSearchResults`: `Prompt`, `Refused`, nothing found |
| AC-006 | `getSearchResults`, read shapes in DB_DESIGN |
| AC-007 | `getSearchResults`: policy, `x-calls-google`, `x-log` |
| AC-008 | `x-markup-results-page.warning` |
| AC-009 | `x-page-frame.page-hints` |
| AC-010 | `getSearchResults`, measured per spec section 9 |
| AC-011 | `x-page-frame.disabled-buttons`, `search-field` |
| AC-012 | `x-page-frame.header-rule`, `language-switcher` |
| AC-013 | none: tests of existing operations (FR-013) |

## 8. For DB_DESIGN

No entity change is expected. The read shapes to record: students matching over surname, given name and email with a student membership in at least one course; the courses of up to 20 students in one read, each with what `DataMarkRules.Left` needs; teachers at status "all"; courses of any status; the Meet code matches with the counts of the three lists and the 20 first by code across them, through the read the screen uses. Reuse `SearchMatch` and `ParticipantLabel`. Each group's total and rows, in a fixed number of commands.

## 9. Non-blocking finding

- **m-1** The Control Plane's `_LanguageSwitcher.cshtml` has the same fallback and the same endpoint-only rule, so its error page under a protected endpoint can also copy the raw query. FR-010 and AC-012 are about the installation and this design follows them. The Control Plane has no search text and no student data in a query string, so this is not urgent. It is a candidate for a later Story.

## 10. Open questions

None.
