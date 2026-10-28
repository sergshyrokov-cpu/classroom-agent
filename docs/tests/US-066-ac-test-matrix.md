---
artifact_type: ac_test_matrix
story: US-066
version: 1
status: DRAFT
created_at: 2026-10-28T18:42:40Z
updated_at: 2026-10-28T18:42:40Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-066-spec.md
    version: 1
  - path: docs/decisions/US-066-open-decisions.md
    version: 1
  - path: docs/designs/api/US-066-api-design.md
    version: 1
  - path: docs/tests/US-066-test-strategy.md
    version: 1
supersedes: null
---

# US-066 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`):

| Code | Class |
|---|---|
| **NJ** | `Web.Pages.NamesakeJournalPageTests` |
| **NR** | `Web.Pages.NamesakeReportPageTests` |
| **NX** | `Web.Pages.NamesakeReportExportTests` (program layout) |
| **NB** | `Web.Pages.NamesakeBlankExportTests` |

All tests are integration tests over HTTP against PostgreSQL. Status: red = fails before implementation as
expected; green-by-design = passes before implementation on purpose (a "nothing changes" guard). Final
statuses are in the test generation report.

| AC | Scenario | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| AC-001 | journal: two «Іван Петренко», one «Олена Коваль» | NJ | `TwoStudentsWithTheSameName_ShowTheirEmail_AndTheOtherRowDoesNot` (uk, en) | headings exactly `Іван Петренко (a)`, `Іван Петренко (b)`, `Олена Коваль` | red |
| AC-002 | report, profile names | NR | `TwoStudentsWithTheSameProfileName_ShowTheirEmail_AndTheOtherRowDoesNot` (uk, en) | `Коваль Олена`, `Петренко Іван (a)`, `Петренко Іван (b)` | red |
| AC-003 | program export name cells | NX | `TheNamesakesNameCells_HoldTheNameAndTheEmail` (uk, en) | column A as AC-002 | red |
| AC-003 | layout otherwise unchanged | NX | `OnlyTheNameCellsText_Changes_TheLayoutIsTheSame` | profile vs email export identical except name cell text; ends asserting the email is present | red (final guard) |
| AC-004 | blank name cells = program layout | NB | `TheBlanksNameCells_HoldTheNameAndTheEmail_AsTheProgramLayout` | blank and program column texts as AC-002 | red |
| AC-004 | namesakes in different copies (I-4) | NB | `NamesakesInDifferentCopies_BothShowTheirEmail` | copy 1: Коваль, Петренко (a); copy 2: Петренко (b) | red |
| AC-005 | case-only difference, journal | NJ | `NamesDifferingOnlyInCase_AreNamesakes` (uk, en) | both rows with their email | red |
| AC-005 | case-only difference, report | NR | `NamesDifferingOnlyInCase_AreNamesakes` (uk, en) | both rows with their email | red |
| AC-006 | journal without namesakes | NJ | `NoNamesakes_NoRowShowsAnEmail` | names only | green-by-design |
| AC-006 | report without namesakes | NR | `NoNamesakes_NoRowShowsAnEmail` | names only | green-by-design |
| AC-006 | export without namesakes | NX | `NoNamesakes_NoCellHoldsAnEmail` | names only; no `@` in any cell | green-by-design |
| AC-006 | blank without namesakes | NB | `NoNamesakes_NoCellOfTheBlankHoldsAnEmail` | names only; no `@` in any cell | green-by-design |
| AC-007 | email variant then profile | NR | `TheNameVariantDecides_WhoIsANamesake` | email: local parts only; profile: emails shown | red |
| AC-007 | same part before `@`, two domains (I-3) | NR | `TheSamePartBeforeTheAt_AtTwoDomains_ShowsTheFullAddresses` | email variant: full addresses; profile: none | red |
| AC-008 | namesake without email, journal | NJ | `ANamesakeWithoutAnEmail_IsShownWithTheNameOnly` | `Іван Петренко (a)`, `Іван Петренко` | red |
| AC-008 | namesake without email, report | NR | `ANamesakeWithoutAnEmail_IsShownWithTheNameOnly` | `Петренко Іван (a)`, `Петренко Іван` | red |
| AC-008 | namesake without email, export | NX | `ANamesakeWithoutAnEmail_HasTheNameOnly` | as the report | red |
| AC-009 | leaver namesake, journal | NJ | `ANamesakeWhoLeft_ShowsTheEmail_AndTheLeftMark` (uk, en) | `Іван Петренко (b) вибув(-ла) 10.09.2026` | red |
| AC-009 | leaver namesake, report | NR | `ANamesakeWhoLeft_ShowsTheEmail_AndTheLeftMark` (uk, en) | `Петренко Іван (b) вибув(-ла) 10.09.2026` | red |
| AC-009 | leaver namesake, export | NX | `ALeavingNamesakesCell_HoldsTheEmail_AndKeepsTheLeftNote` (uk, en) | cell `Петренко Іван (b)`, note keeps «вибув(-ла) 10.09.2026» | red |
| AC-009 | leaver namesake, blank | NB | `ALeavingNamesakesCell_HoldsTheEmail_AndKeepsTheLeftNote` | as the export | red |
| FR-001, OD-002 a | two teachers with the same name | NR | `TwoTeachersWithTheSameName_AreShownWithoutAnEmail` | header names them; no teacher email on the page | green-by-design |
| FR-002.5, I-2 | journal rows labelled by email | NJ | `RowsLabelledByTheirEmail_GetNoSecondEmail` | the two emails alone | green-by-design |

Every AC-001 … AC-009 has at least one red test.
