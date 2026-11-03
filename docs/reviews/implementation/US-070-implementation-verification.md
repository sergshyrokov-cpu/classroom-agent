---
artifact_type: implementation_verification
story: US-070
version: 1
status: APPROVED
created_at: 2026-11-03T08:19:52Z
updated_at: 2026-11-03T08:19:52Z
produced_by: implementation-verifier
inputs:
  - path: docs/stories/US-070-search-in-lists-and-pickers.md
    version: null
  - path: docs/specifications/US-070-spec.md
    version: 4
  - path: docs/decisions/US-070-open-decisions.md
    version: 2
  - path: docs/designs/api/US-070-api-design.md
    version: 1
  - path: docs/designs/api/US-070-openapi.yaml
    version: 1
  - path: docs/designs/database/US-070-db-design.md
    version: 1
  - path: docs/designs/database/US-070-entity-model.md
    version: 1
  - path: docs/tests/US-070-test-strategy.md
    version: 1
  - path: docs/tests/US-070-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-070-test-generation-report.md
    version: 1
  - path: docs/evidence/US-070-implementation-report.md
    version: 1
supersedes: null
verdict: PASS
---

# US-070 Implementation Verification

## 1. Summary

**PASS.** No Critical or Major finding. The code does what Specification v4 and the designs say, the implementation report's change set equals the working tree, and the Story's own tests are green (362 of 362). Three Minor findings, three notes and four disclosed deviations are recorded below.

**AC-009's narrowing has never run in a browser.** Tests assert the markup and the script's text only, as the Specification says. This review read the script against FR-009 line by line and found no defect, but reading is not running: the Owner's walk on a computer and a phone (implementation report §3.1) is still due before `HUMAN_PR_APPROVAL`.

Delegated: `cheap-worker` ran the build and the 14 test filters of §8 and reported them. Kept here: reading the tests against the Acceptance Criteria, the code against the artifacts, the deviations and the findings. The full suite was not rerun; `security-reviewer` does that.

## 2. Reviewed Artifacts

The inputs in the front matter, at the versions shown, and the working-tree diff against `83366fa` (`git status`, `git diff`, and untracked files read directly). The report's `inputs` name the current versions, so nothing is stale. OD-001 is resolved, a); no Open Decision is unresolved.

## 3. AC Verification

Every method named in the `ac_test_matrix` exists (96 of 96, checked by a script over the test tree).

| AC | Tests read | Verdict |
|---|---|---|
| AC-001 | `SearchMatchingSourceTests`: order, parts, case, across fields, typo, six apostrophe pairs on three sources, others literal, `%` `_` `\`, hyphens, hyphen-only word. Real PostgreSQL, each with a row that must not match | Proved |
| AC-002 | `SearchMatchingSourceTests` (name and section, filters), `CourseListSearchPageTests` | Proved |
| AC-003 | `TeacherListSearchPageTests`: later page by each field with a first-page control, total 1, status AND, no-match text | Proved |
| AC-004 | `MeetCodesSearchPageTests`: counters and rows on each list, each field, Organizers column; `SearchMatchingSourceTests`: candidates and acting accounts not searched | Proved |
| AC-005 | Before-erasure control on each list, then not found and not shown, survivor still found. The erasure is simulated by SQL (INFO-2) | Proved at the read |
| AC-006 | `MeetCodeSearchSurvivesActionsTests`, paging and switcher tests on both lists; the search through a real switch round trip; page left empty followed to the last page | Proved, except one outcome (m-1) |
| AC-007 | Refusals on each list with status, message, empty field, return path, no echo; `ARefusedText_ReachesNoRead…` with an accepted-text control through recording sources | Proved |
| AC-008 | `ListSearchAccessTests`: Admin, Dean, anonymous, restricted, read-only, Google count, fingerprint, audit; the log read stops the host first, so no event can be missed | Proved |
| AC-009 | `PickerNarrowMarkupTests` (hooks, rows, no field where nothing narrows); `PickerNarrowScriptTests` (text, m-3). Behaviour: the person's walk | Automated part proved; **manual part pending** (INFO-1) |
| AC-010 | Field tests on the three lists in both languages; `PageSearchHintTests`; `ListSearchTranslationTests`: exact values | Proved |

## 4. Specification Coverage

| Requirement | Where | Result |
|---|---|---|
| FR-001 | `SearchMatch` (one definition; `CourseListSource`, `TeacherListSource`, `MeetCodesReadSource` all use it); `picker-narrow.js` `fold`, `matches`, `wordsOf` for rules 1 to 5, 7, 8 | Implemented |
| FR-002, FR-007 | `CourseListSource.Matching`, `Courses/Index.cshtml`; the erasure screen is untouched | Implemented |
| FR-003, FR-006 | `GetTeacherListQuery`, `TeacherListUrl.Build`, `Teachers/Index.cshtml`; row links unchanged (I-12) | Implemented |
| FR-004, I-6 | `MeetCodesReadSource`, `GetMeetCodesQuery.BuildAsync` (clamp from the counts), `MeetCodes/Index.cshtml` | Implemented |
| FR-005, I-7 | `MeetCodeWrite.ReturnQ`, `Result`; the three use cases; `MeetCodesController`; `CourseChoice.cshtml` | Implemented |
| FR-008 | `_PageSearchHint.cshtml`, `PageSearchHintTextKeys.FieldLabelKey`, resx: one template, `{0}` from the field's own key | Implemented |
| FR-009 | Script read item by item: 1 no `name`, `autocomplete`, `maxLength`; 2 rows removed, re-appended in original order; 3 option text, `data-narrow-text` without mark or percent; 4 empty and selected rows kept; 5 one field, `hidden` on an emptied fieldset, which holds the legend; 6 kept rows do not count as a match, polite live region, Enter stopped; 7 no submitted field added; 8 `disabled` while no row is left. No style use, no inline code | Implemented; not run in a browser |
| FR-010 | No entity, audit event, port to Google or policy change | Holds |
| VR-001 | `CourseListQueryRules.ReadSearch`, the order as written; the course list now calls it | Implemented |
| VR-002 to VR-004 | `GetCourseListQuery`, `GetTeacherListQuery`, `GetMeetCodesQuery.ExecuteAsync` | Implemented; D-3 untested (m-2) |
| Section 7, 9 | Word a bound parameter (`WordHolder`, test on command text), `Uri.EscapeDataString`, Razor encoding, explicit collation on both sides, commands independent of word count (test) | Holds |

Behaviour no artifact asks for: none found.

## 5. Design Conformance

- **OpenAPI.** Parameters, status codes, outcomes (`x-outcomes-with-search`), query order (teachers `status`, `q`, `page`; Meet `list`, `q`, `page`, `size`; choice `returnPage`, `returnQ`), redirect without `size` (D-5), markup ids, `x-picker-contract` and `x-hint-contract` match, apart from D-3 below.
- **Database design.** No migration or entity change. The fold, the `strpos`-based match, the hyphen rule with no term for an empty word, the array parameter on the raw Unassigned read, the shared `LoadOrganizersAsync` and the no-read on an empty match list match §2, §4, §5.
- **Entity model.** Port and row signatures match §1, §2. `SearchMatch` carries the responsibilities of §3.
- **Invariants.** AD-3: no `DbContext` or business rule in `Web`. AD-4: no Google type or port touched. AD-6: searches write nothing; row actions keep the guard first. AD-8: DTOs only in signatures and views.

## 6. Deviations

| Id | Departs from | Reason | Verdict |
|---|---|---|---|
| D-1 | Test edit: an empty `q` in two existing test calls | `TeacherListRequest` gained a required parameter; no assertion changed (diff read) | Sound |
| D-2 | Names no design gives: `SearchRefusalKey`, `CurrentList`, `MeetCodeChoiceResult.ReturnQ` | Each serves a stated contract: `searchMessage`, I-7, the choice page's `404` | Sound |
| D-3 | OpenAPI `getTeacherList` `400` says "the field empty" for any malformed parameter; with an invalid `status` or `page` and a valid `q` the field and return path keep the text | VR-002 outranks the design: "other valid parameters keep their values", and `GetCourseListQuery` does the same (read) | Sound; untested (m-2) |
| D-4 | One more command under a search on the Unassigned read; page reads recount their totals | The first is db-design §4; the second is today's shape. Still independent of the number of words | Sound |

No undisclosed deviation found.

## 7. Report Accuracy

The change set in report §4 equals the 69 modified and untracked source and test files of the working tree, file by file; workflow files are in the closed list. Every code and test file is traced. The AC-to-test locations exist. The test edits outside the test generation report are the two the report names, and the `TeacherListPageTests` and `PageSearchHintTests` edits are the ones api-design §6 and D-10 call for. The claim of 7336 of 7336 was not rerun here.

**Clean change.** Ripgrep over every changed source and test file: no `TODO`, `TBD`, `FIXME` or `NotImplementedException`; no skipped or commented-out test in the Story's classes.

## 8. Story Test Run

Run by `cheap-worker`, one filter term each, `--no-build`. Build: exit 0, 0 warnings, 0 errors. Every run exit 0, 0 skipped, no timeout or Ryuk line, no rerun.

| Filter | Passed / total |
|---|---|
| `SearchMatchingSourceTests` | 45 / 45 |
| `TeacherListSearchPageTests` | 21 / 21 |
| `MeetCodesSearchPageTests` | 38 / 38 |
| `MeetCodeSearchSurvivesActionsTests` | 23 / 23 |
| `CourseListSearchPageTests` | 13 / 13 |
| `ListSearchAccessTests` | 21 / 21 |
| `PickerNarrowMarkupTests` | 15 / 15 |
| `PickerNarrowScriptTests` | 25 / 25 |
| `ListSearchTranslationTests` | 13 / 13 |
| `PageSearchHint` | 67 / 67 |
| `TeacherListPageTests` | 17 / 17 |
| `TeacherListSourceTests` | 10 / 10 |
| `TeacherListQueryTests` | 46 / 46 |
| `TeacherListReadOnlyTests` | 8 / 8 |

## 9. Findings

**Critical, Major:** none.

**Minor**

- **m-1.** `MeetCodesController.CourseChoice`, `GetMeetCodeCourseChoiceQuery`, checked against openapi `getMeetCodeCourseChoice` `404` ("with the search") and FR-005.3. The code carries the search, but no test opens the choice page by `GET` for an unknown code with a `returnQ`; `AnUnknownCode_KeepsTheSearch` covers only the `POST`. Loop-back if fixed: `test_gap`.
- **m-2.** `GetTeacherListQuery`, checked against VR-002 and report D-3. No test sends an invalid `status` or `page` with a valid `q`, so keeping the text there is unproved on the teacher list; `EveryFailingParameter_IsReported` has both failing. Loop-back if fixed: `test_gap`.
- **m-3.** `PickerNarrowScriptTests.TheScript_HidesAnEmptyBlock`, `TheScript_DisablesTheSubmit`, checked against FR-009 items 5, 8. Each looks for one word, and the script's comments at lines 91 and 92 hold both, so deleting the behaviour would leave them green. The code does hold it (lines 123, 127), and the Specification leaves behaviour to the person. Some test summaries also cite the wrong FR-009 items. Loop-back if fixed: `test_gap`.

**Notes**

- **INFO-1.** AC-009's walk on a computer and a phone is pending. Definition of Done item 3 is not met for that criterion until report §3.1 records it, before `HUMAN_PR_APPROVAL`.
- **INFO-2.** The AC-005 tests simulate erasure with `UPDATE meet_session SET organizer_email = NULL`. This is the stored state the design names, and the erasure and exclusion paths that produce it belong to US-038 and US-065; no test here runs them end to end.
- **N-1.** `TeacherListUrl.Build(status, page)` is now called only by `TeacherListUrlTests`. Harmless.

**The Story's OPEN findings.** `US-070/TEST_WRITING/INFO-1` and `INFO-2` are not closed here. The report's clean full run is a claim not rerun at this stage, and neither cause was diagnosed; a stage may close only as `FIXED` or `WITHDRAWN`. All three classes of INFO-2 are outside this Story's filters and were not run here.

## 10. Verdict

**PASS.** Next stage: `SECURITY_REVIEW`.
