---
artifact_type: ac_test_matrix
story: US-060
version: 1
status: DRAFT
created_at: 2026-10-27T14:04:03Z
updated_at: 2026-10-27T14:04:03Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-060-spec.md
    version: 1
  - path: docs/decisions/US-060-open-decisions.md
    version: 1
  - path: docs/tests/US-060-test-strategy.md
    version: 1
supersedes: null
---

# US-060 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`):

| Code | Class |
|---|---|
| **CL** | `Web.Pages.CourseListMarksPageTests` |
| **JM** | `Web.Pages.JournalMarksPageTests` |
| **RM** | `Web.Pages.ReportMarksPageTests` |
| **RX** | `Web.Pages.ReportMarksExportTests` (program layout) |
| **BX** | `Web.Pages.BlankFormMarksExportTests` |
| **RO** | `Web.UseCases.DataMarksReadOnlyTests` |

All tests are integration tests over HTTP against PostgreSQL. Status: red = fails before implementation as
expected; green-by-design = passes before implementation on purpose.

| AC | Scenario | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| AC-001 | list: mark after the status, before the teacher; plain only status | CL | `TheCourseList_ShowsEachCoursesMark_NextToItsStatus` (uk, en) | «немає в Classroom з 15.09.2026», «не видно технічному акаунту з 16.09.2026» | red |
| AC-001 | card: mark in the status field | CL | `TheCourseCard_ShowsTheCoursesMark_InItsStatusField` (uk, en) | status + mark; plain = status only | red |
| AC-002 | teacherless course | CL | `ACourseWithoutATeacherOnTheRoster_SaysSo_InPlaceOfTheTeachers` (uk, en) | «викладача немає в Classroom», no former teacher; plain shows its teacher | red |
| AC-003 | full journal 15/12, draft 13/12, 12/12 | JM | `TheFullJournal_LabelsAndHighlightsGradeAndDraftAboveMaximum` (uk, en) | label + class on the first two; the third ordinary | red |
| AC-003 | short journal ignores the draft | JM | `TheShortJournal_LabelsTheGradeAboveMaximum_ButNotTheDraft` | 15 labelled, draft not | red |
| AC-003 | report: scale value 100 % kept | RM | `TheReport_LabelsGradeAndDraftAboveMaximum_AndKeepsTheScaleValue` (uk, en) | `TSFULL` + label; 12/12 `TSFULL` without label | red |
| AC-004 | program export: value, fill, note | RX | `AnAboveMaximumCell_KeepsItsValue_IsColoured_AndCarriesTheRawPoints` (uk, en) | `TSFULL`, filled, «Бали: 15 з 12» / «Чернетка: 13 з 12»; 12/12 neither | red |
| AC-004 | blank form: same fill as the program, note | BX | `AnAboveMaximumGradeCell_IsColouredLikeTheProgramLayout_AndCarriesTheNote` (uk, en) | same as above, the colour equal to the program layout | red |
| AC-005 | journal: column kept, mark in heading | JM | `TheDeletedItem_KeepsItsColumnAndGrades_WithTheMarkInItsHeading` (uk, en) | «немає в Classroom» with no date; grades 7/10, 8/10; ordinary unmarked | red |
| AC-005 | report: heading + «Теми занять» | RM | `TheDeletedItem_IsMarkedInItsColumnAndOnTheLessonTopics` (uk, en) | mark in heading and after the topic | red |
| AC-005 | program export: heading + topic after `\n` | RX | `TheDeletedItem_IsMarkedInItsHeadingAndTopic` (uk, en) | `title\n…\nнемає в Classroom`; topic `title\nнемає в Classroom` | red |
| AC-005 | blank form: note on topic title | BX | `TheDeletedItemsTopic_CarriesTheMarkAsANote_AndThePrintedTextIsUnchanged` (uk, en) | note «немає в Classroom»; printed text without marks | red |
| AC-006, AC-007, AC-011, AC-012 | journal header line above the table, both views | JM | `TheCourseHeaderLine_StandsAboveTheTable` (7 variants, full/short) | the line of FR-003.1 once, above `#journal-table`; none for None / Archived | red; None, Archived green-by-design |
| AC-006, AC-007, AC-011 | journal header in English | JM | `TheCourseHeaderLine_IsInEnglish_ForAnEnglishUser` | the English line, not the Ukrainian one | red |
| AC-006, AC-007, AC-011, AC-012 | report header line in `#report-header` | RM | `TheCourseHeaderLine_IsInTheReportHeader` (variants × uk/en) | the line once; none for None / Archived | red; None, Archived green-by-design |
| AC-006, AC-007, AC-011, AC-012 | program export header: cell C2 | RX | `TheCourseHeaderLine_IsTheThirdCellOfTheCourseRow` | C2 = the line, B2 = course; the table still on row 6 | red; None, Archived green-by-design |
| AC-006, AC-007, AC-011, AC-012 | blank form: note on `{{course}}` | BX | `TheCourseCell_CarriesTheHeaderLineAsANote` | the note = the line; course text unchanged | red; None, Archived green-by-design |
| AC-008 | journal / report: "left" next to the name | JM, RM | `AStudentOffTheRoster_IsMarkedLeft_NextToTheName` (uk, en) | «вибув(-ла) 10.09.2026»; submitter «вибув(-ла)» with no date; on-roster none | red |
| AC-008 | program export: note on the name cell | RX | `ALeaversNameCell_KeepsTheName_AndCarriesTheLeftNote` (uk, en) | name unchanged; notes as above; on-roster no note | red |
| AC-008 | blank form: note on `{{student.name}}` | BX | `ALeaversNameCell_KeepsTheName_AndCarriesTheLeftNote` (uk, en) | name as in the program layout; notes | red |
| AC-009 | English everywhere; names untranslated | CL, JM, RM, RX, BX | every `(…, "en")` case above | FR-009 English texts, `M/d/yyyy` dates; Google names as stored | red |
| AC-010 | read-only mode, three causes | RO | `InReadOnlyMode_TheListAndTheExportShowTheMarks_AndWriteNoTeachingData` | list mark, export C2 line and note; teaching fingerprint unchanged; 0 Google calls | red |

Every AC-001 … AC-012 has at least one red test.
