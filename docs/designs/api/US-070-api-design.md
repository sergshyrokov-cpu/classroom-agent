---
artifact_type: api_design
story: US-070
version: 1
status: DRAFT
created_at: 2026-11-02T20:22:33Z
updated_at: 2026-11-02T20:22:33Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-070-spec.md
    version: 4
  - path: docs/decisions/US-070-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 109
supersedes: null
---

# US-070 API Design — search in the teacher and Meet code lists, narrowing pickers

**Verdict: PASS.** No new path and no JSON API: a delta to the server-rendered contracts of US-020, US-022 and US-032. Contract:
`docs/designs/api/US-070-openapi.yaml`.

Delegation: none. The designer read the Specification, `US-032-openapi.yaml`, the top of `US-022-openapi.yaml`,
`api-conventions.md`, and directly: `MeetCodesController`, `TeachersController`, `UiLanguageController`, `GetMeetCodesQuery`,
`MeetCodeWrite`, `GetCourseListQuery.ReadSearch`, `PageSearchHintTextKeys`, `TeachersTextKeys`, and the views of the pickers and lists.

## 1. Decisions (items the Specification left to this stage)

- **D-1 `q` on the lists, `returnQ` on the way back.** FR-005.1 lets this stage rename `returnQ`; it stays, because it sits beside `returnPage`
  and has the same meaning. They differ in refusal: a bad `q` is `400`; a bad `returnQ` is no search (FR-005.5).
- **D-2 `returnQ` is read apart from the known-fields check.** `MeetCodeWrite.Fields` returns null on any repeated known field, and null is
  `Malformed`. If `returnQ` joined `KnownFields`, a repeated one would refuse the action, against FR-005.5. So it stays out of that list and is read
  from `form.Fields` by name, all occurrences, through VR-001. `MeetCodeChangeResult` gains the validated text.
- **D-3 Refused search on the Meet codes: a separate `searchMessage`.** `message` holds `MeetCodesMessageKey` and resolves under `MeetCodes.Message.*`;
  VR-001 requires the existing `Courses.Validation.*` texts and no new translation. `searchMessage` set means nothing was read, which is one check for the view
  (VR-003, I-5, I-13). `counts` is null then. When `q` and another parameter are both invalid, only `searchMessage` shows (VR-004).
- **D-4 Page clamp from the counts.** The counts come first and hold the shown list's total, so the effective page is known before the rows are read, and the
  number of reads stays fixed (section 9). A total of 0 gives page 0. The effective page goes into the rows' `returnPage`, the links and the return path.
- **D-5 The redirect after a write stays without `size`.** It has none today, and FR-005.2 names the list, the page and the search. A non-default size is lost as it is
  today; a return page then past the last is clamped by D-4. The redirect order is `list`, `q`, `page`.
- **D-6 A stale or unknown-code refusal keeps its list and page 0 of US-032**, adding only the search. The overload the host calls gains the validated text.
- **D-7 No log line on the Meet codes page.** The teacher list has refusal log lines, and `ParameterOf` gains `q`. `MeetCodesController` has no logger and no refusal log today; adding
  one is not required by AC-007 ("not logged") and would be a new thing to get wrong. A test still asserts the text is in no log event.
- **D-8 Picker hooks are markup rendered by the server.** The views decide whether a hook exists, so "no field where there is nothing to narrow" (FR-009) is testable over HTTP, and the
  translated texts arrive in `data-` attributes, as no inline code is allowed. Automated tests assert these hooks and the script's text; AC-009 leaves the behaviour to the person.
- **D-9 `data-narrow-text` on the course-choice rows only.** There the row shows a mark or a percent that must not match (FR-009.3). The four selects show just the label, so
  the option's own text is the row's text, and the attribute would double the page's size for nothing.
- **D-10 Hint.** One text, `PageSearchHint.ListField`, with `{0}` filled from the field's own label key, so the two cannot differ (FR-008). Variants become `course-list`, `teacher-list`, `meet-codes`; the old two keys and the
  `paged-list` variant go. `ordinary` stays. `PageSearchHintTestData` and `PageSearchHintTranslationTests` change with it (spec section 13).
- **D-11 Translation keys.** New: `Teachers.Form.Search`, `MeetCodes.Form.Search`, `Teachers.Empty.NoMatch`, `MeetCodes.Empty.NoMatch`, `Picker.Narrow.Label`, `Picker.Narrow.NothingFound`,
  `PageSearchHint.ListField`, each in both files. `TeacherListMessageKey` gains the three search values; `TeachersTextKeys.Validation` already resolves under `Courses.Validation.*`, so they need no new text.

## 2. Operations

| Operation | Change | Policy | Spec |
|---|---|---|---|
| `GET /workspace/courses` | matching rules, `autocomplete`, hint | `ViewCourses` | FR-001, 002, 007, 008 |
| `GET /workspace/teachers` | `q`, field, empty text, hint | `ViewCourses` | FR-001, 003, 006, 007, 008 |
| `GET /workspace/meet-codes` | `q`, form, two columns, clamp, empty text, hint | `ViewMeetCodes` | FR-001, 004, 006, 007, 008 |
| `GET …/course-choice` | `returnQ`, Cancel target, picker hooks | `LinkMeetCodes` | FR-005, 009 |
| `POST …/link`, `…/confirmation`, `…/not-a-course-mark` | form field `returnQ`, outcomes carry the search | `LinkMeetCodes` | FR-005 |
| Journal, template report, Meet reports (`GET`) | picker hooks only; no parameter or status changes | as today | FR-009 |
| Course list teacher filter | picker hook only | `ViewCourses` | FR-009 |

## 3. Checked and unchanged

- **Return path length.** `UiLanguageController` drops a return path over 2048 characters and goes to the landing page. The worst case fits: 100 characters of 3-byte UTF-8 encode to 900,
  a 64-character code at most 576, and the rest of the course-choice path under 100. No change is needed.
- **The switcher's fallback** to the raw query is never reached: all three lists and the course-choice page set `ReturnPathViewDataKey` from validated values (spec section 7).
- **Row actions in read-only mode** stay `409` by the guard, which runs before `returnQ` is looked at.
- **Audit.** `returnQ` enters no audit row. The search itself writes none.

## 4. Auth model

Unchanged and deny by default. Both v1 roles are allowed on every operation, so the forbidden case of TC-5 is the anonymous redirect and the restricted session, as US-022 and US-032 test it. An Owner has no installation
account. Nothing is added to the anonymous or antiforgery lists.

## 5. Error model

No JSON and no API-6 body. List refusals answer `400` with the page itself (VR-002, VR-003). A malformed row-action form answers `400` with the error page, and the read-only refusal `409` with the error page, neither returning to the search (FR-005).
Unexpected failures go to the host's handler, with the text in no message.

## 6. Compatibility

Three earlier contract sentences change, each named by the Specification:

- US-022 "any other query parameter, `q` included, is ignored": `q` is now read.
- US-032 "a `page` beyond the last answers `200` with empty `content`": it now shows the last page (I-6). Existing tests that pin the old behaviour change with it.
- US-032 "Cancel" opened the default list: it now opens the code's list (I-7).

US-032 I-14 stands for every system-built address and is reconciled with v108 in spec section 7. `ContractVersion.Current` stays 1.

## 7. Acceptance Criterion → operation

| AC | Operations |
|---|---|
| AC-001 | `getCourseList`, `getTeacherList`, `getMeetCodes` |
| AC-002 | `getCourseList` |
| AC-003 | `getTeacherList` |
| AC-004 | `getMeetCodes` |
| AC-005 | `getMeetCodes` |
| AC-006 | `getTeacherList`, `getMeetCodes`, `getMeetCodeCourseChoice`, the three writes, language switcher's return path |
| AC-007 | `getCourseList`, `getTeacherList`, `getMeetCodes` |
| AC-008 | the three lists; the writes keep their policy and guard |
| AC-009 | `x-picker-contract`, on the five pickers' pages (person's check) |
| AC-010 | the three lists; `x-hint-contract` |

## 8. For DB_DESIGN

Ports that take the search: the course list source, the teacher list source, and in `IMeetCodesReadSource` the counts and the three page reads. The Linked and Marked rows gain the organizers, through the same null-excluding read as the Unassigned rows. Shared matching, with the literal
treatment of `%`, `_` and `\`, the apostrophes and rule 6, belongs to that stage.

## 9. Open questions

None.
