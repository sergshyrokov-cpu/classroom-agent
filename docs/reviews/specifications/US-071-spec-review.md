---
artifact_type: specification_review
story: US-071
version: 2
status: APPROVED
created_at: 2026-11-03T15:33:56Z
updated_at: 2026-11-03T15:44:40Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-071-search-field-in-the-header.md
    version: null
  - path: trebovaniya.md
    version: 109
  - path: docs/specifications/US-071-spec.md
    version: 2
  - path: docs/specifications/US-071-spec-digest.ru.md
    version: 2
  - path: docs/decisions/US-071-open-decisions.md
    version: 1
supersedes: docs/reviews/specifications/US-071-spec-review.md@v1
---

# US-071 Specification Review

## Summary

**PASS.** No Critical or Major findings, one Minor finding.

The two blocking findings of review v1 are fixed: the Specification now follows the off-roster membership of section 3, and it states that a student with no stored name or email is not found. The review ran in a fresh context, re-reading the sources and the code instead of relying on v1's table.

Nothing was delegated: all sources and code were read directly.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-071-search-field-in-the-header.md` | current |
| Requirements | `trebovaniya.md` | 109 |
| Specification | `docs/specifications/US-071-spec.md` | 2 |
| Digest | `docs/specifications/US-071-spec-digest.ru.md` | 2 |
| Open Decisions | `docs/decisions/US-071-open-decisions.md` | 1 |

Inputs are not stale: the Specification names v109 and the digest names Specification v2.

## Findings of Review v1, Rechecked

| v1 finding | Result in v2 |
|---|---|
| F-1 Critical: FR-003 denied the off-roster membership | Fixed. FR-003, I-4 and AC-003 use it. `DataMarkRules.Left(onRoster, seenOnce, lastSeenAt, zone)` gives no date when seen once, and section 3 (v56) creates the membership with equal dates |
| F-2 Major: a student who usually cannot be found was promised | Fixed. FR-003, I-4, AC-003 and digest decision 2 state the limit. `ResolveSubmitterAsync` imports the person unnamed, as they say |
| F-3 Minor: the header sentence of §8 v105 and NFR-076 | Fixed. I-17 and digest decision 15 |
| F-4 Minor: "the same total" for Meet codes | Fixed. FR-007 and AC-004 use the sum of the three counters |
| F-5 Minor: I-8 outside the digest's decisions | Fixed. Digest decision 14 |
| F-6 Minor: "незаметно" about the switcher | Fixed. The digest states the removal apart from the tests |

## Fidelity

Checked against the cited text:

| Item | Source | Result |
|---|---|---|
| Label, count line, reminder, warning | Epic 2 v108, lines 1303-1321 | Word for word; «з» for «из» disclosed (I-10) |
| Both hint texts, both languages | Section 5 v108, lines 2060-2067 | Word for word |
| Groups, order, searchable fields, row content | Epic 2 "Поле «Пошук»", BR-088 | Faithful |
| Departed students and the mark | Epic 2, Epic 3 "Выбывший ученик", BR-068, section 3, US-060 OD-003 a | Faithful; keys `DataMarks.Left.On`, `DataMarks.Left` exist in both languages |
| Matching, limits on text | BR-087, US-070 FR-001, VR-001; `CourseListQueryRules` | Reused, not restated; the rule's current home is named correctly |
| Teacher scope and link | `TeacherListSource` (`Role == Teacher && OnRoster`), `CourseListUrl` | I-7 is accurate |
| Meet code search | US-070 FR-004, I-14 | Faithful; the row link holds only the user's text |
| Error page | `_LanguageSwitcher.cshtml` lines 10, 14-15; `ErrorController.Page` callers: Meet codes, report templates, language, 409 handler, antiforgery; US-039 I-5 | Diagnosis accurate |
| Return paths | Views setting `ReturnPathViewDataKey`: course, teacher and Meet lists and course choice all set one | Section 7's "pages without one hold no search text" holds |
| Rights | §2 row "Просмотр курсов и участников"; `ViewCourses`, `ViewMeetCodes` both Admin and Dean | No invented cell |
| Disabled buttons | `Erasure/Index.cshtml` (2), `Erasure/Confirmation.cshtml` (1, outlined red) | "Three erasure buttons" is accurate |
| Not found | Epic 2 "Что не находится", `RunSynchronizationUseCase` exclusions | Section 7 is accurate |
| Named symbols | `ParticipantLabel`, `SearchMatch`, `InstallationClaimTypes.PasswordIsTemporary`, `TemporaryPasswordMiddleware`, `PageSearchHintTestData`, `PageSearchHintTranslationTests`, `Courses.Status.*`, `MeetCodes.List.*`, `Courses.Validation.Search*` | All exist |
| Reused texts keep their periods | NFR-078, v109 | Correct |

## Story Coverage

All thirteen Acceptance Criteria and all twelve scope items are represented and mapped in section 13. The additions to the Story's AC wording (exactly 20 rows, unnamed student, the temporary-password session, the walk also checking the field) make existing rules testable and add no scope. The Story's Notes are all answered: US-039 INFO-1 in section 7, the erasure search and the folding in section 7 and section 10, the clean Edge profile in section 9.

## Consistency

- **Permission matrix:** no invented cell.
- **Read-only mode (BR-026), audit (SC-11), localization (NFR-073), SC-10, SC-14, SC-15:** consistent.
- **US-039 I-5, US-060 OD-003 a, US-070 FR-001, FR-004, VR-001, VR-002, US-032 I-14:** consistent.
- **§8 v105 and NFR-076 on the header:** a stated conflict, resolved in favour of the later Epic 2 v108 (I-17), with the correction left to the next requirements version as the rule on versions requires.

## Open Items

Items 26 and 30 do not block, as the Specification says. Items 14 and 27 affect how many departed students exist, not how search behaves, so neither needs an Open Decision. No section 7 item is silently resolved.

## Interpretations

I-1 to I-17 are named in Specification section 11 and in the digest's decisions. The review found no further unnamed choice of substance, apart from the wording in m-1. These are the ones with a real alternative for the Owner:

- **I-4** a student with no stored name or email is not found;
- **I-6** teachers and courses of every status are searched;
- **I-7** a departed teacher is not found, a departed student is;
- **I-11** the whole-list link shows only above 20 matches, where Epic 2 can also be read as always;
- **I-13** the error page loses the switcher instead of getting a clean return path;
- **I-16** the speed bound and data proportions are set by the Specification.

## Digest Fidelity

The digest is plain, traces to the Specification and names every interpretation by id. Decisions 1 and 2 are marked for special attention and describe the limits as they are; nothing is softened.

Not a defect: the digest does not say that AC-011 needs a person's walk on a computer and a phone. The orchestrator should tell the Owner at the gate.

## Findings

### m-1 Minor: the Meet group's heading is not among the texts worded here

FR-002 heads the group «Коди зустрічей Meet», from Epic 2's "коды встреч Meet". The rows lead to a page titled «Зустрічі Meet» (`MeetCodes.Title`), and I-1 and digest decision 13 do not list this heading among the worded texts. The wording follows the requirements and is not wrong. The Owner may prefer the page's own title, so the words a user follows match the words they land on.

## Verdict

`PASS`, next stage `HUMAN_SPEC_APPROVAL`. This is not human approval: whether the Story is understood right is the Owner's call at the gate.
