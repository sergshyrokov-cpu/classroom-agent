---
artifact_type: ac_test_matrix
story: US-043
version: 1
status: DRAFT
created_at: 2026-10-14T06:29:14Z
updated_at: 2026-10-14T06:29:14Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-043-spec.md
    version: 2
  - path: docs/designs/api/US-043-openapi.yaml
    version: 1
  - path: docs/tests/US-043-test-strategy.md
    version: 1
supersedes: null
---

# US-043 Acceptance Criteria → Test Matrix

Classes: **E** `Web.Pages.GroupLessonsBlankExportTests`, **F**
`Infrastructure.Export.GroupLessonsBlankFormTests`, **A**
`Web.Security.GroupLessonsBlankAuthorizationTests`, **P**
`Web.Pages.GroupLessonsBlankPageTests`, **L**
`Web.Logging.GroupLessonsBlankLoggingTests`, **T**
`Web.Localization.GroupLessonsBlankTranslationTests`. Status = red-phase result
(see the test generation report).

| AC | Scenario | Level | Class | Test method | Expected | Status |
|---|---|---|---|---|---|---|
| AC-001 | ≤30 / ≤23, defaults: two sheets, screen values in place, numbers, 24th column empty, form untouched | integration | E | `ASmallReport_FillsOneCopyOfEachSheet_WithTheScreensValues` | `200`; plan, copies, untouched | red |
| AC-001 | English user: sheet names / printed text untranslated | integration | E | `AnEnglishUser_KeepsTheFormsSheetNamesAndPrintedText` | `200`; form names | red |
| AC-002 | 25 × 30 by 10 / 23: eight sheets of the Story table, numbering continues | integration | E | `ALargeReport_IsSplitIntoTheStoryTablesEightSheets` | literal names; all copies | red |
| AC-003 | partial copies 7 × 5 by 5 / 3: cleared rows, columns, topic rows; lines kept | integration | E | `APartlyFilledCopy_LeavesUnusedRowsAndColumnsEmpty_AndKeepsTheLines` | six sheets; cleared areas | red |
| AC-003 | lower boundary 1 / 1 | integration | E | `OnePerCopy_IsTheLowerBoundary` | one copy per student × lesson | red |
| AC-004 | no / `program` / `""` / `null` layout, junk per-copy members ignored | integration | E | `TheProgramLayout_IsExactlyTheUs028File` (5 cases) | same sheets, cells, orientation, file name as US-028 | pass (existing behaviour; see report) |
| AC-004 | program logged as such | logging | L | `AProgramExport_LogsTheProgramLayout` | line names `program` | red |
| AC-005 | invalid per-copy values (13 cases) | integration | E | `AnInvalidPerCopyValue_Is400_NamingTheField_WithoutFileOrAudit` | `400`, field, message, no audit | red |
| AC-005 | value never echoed | integration | E | `ARejectedValue_IsNotEchoed` | `400`; no echo | red |
| AC-005 | unknown layout (4 cases), per-copy not read | integration | E | `AnUnknownLayout_Is400_NamingOnlyTheLayout` | `400`, only `layout` | red |
| AC-005 | non-string layout | integration | E | `ANonStringLayout_IsAMalformedBody` | `400` `RequestMalformed`, no field | red |
| AC-005 | field order and first message | integration | E | `SeveralFailures_AreListedInContractOrder` | `from`, `studentsPerCopy`, `lessonsPerCopy` | red |
| AC-005 | 30 / 23, `null`, `30.0` accepted | integration | E | `TheUpperBoundaries_AndNull_AreAccepted` (3 cases) | `200`; defaults | red |
| AC-005 | refusal logged as Warning, value never logged | logging | L | `ARefusedPerCopyValue_IsAWarningNamingTheRule_AndTheValueIsNeverLogged`, `ARefusedLayout_IsAWarningNamingTheRule_AndTheValueIsNeverLogged` | Warning with rule; no value | red |
| AC-005 | messages in uk / en | localization | T | `EveryNewMessage_ExistsInBothLanguages` (4 keys) | translated, distinct | red |
| AC-006 | template showing materials: none in the form; marks, scale, late as on screen | integration | E | `ATemplateShowingMaterials_LeavesMaterialsOut_AndKeepsTheScreensMarks` | no material title; cells equal screen | red |
| AC-007 | no lesson, no student: one copy each, course and teachers, nothing invented, audited | integration | E | `AnEmptyReport_IsOneCopyOfEach_WithCourseAndTeachers_AndIsAudited` | two sheets; audit row, 0 rows | red |
| AC-007 | no lesson in the period, students on the roster (OD-013 T-3) | integration | E | `NoLessonInThePeriod_IsOneCopyOfEach_WithoutStudents` | one grading + one topics, no student | red |
| AC-007 | lessons without students | integration | E | `LessonsWithoutStudents_FillTheDatesAndTopics` | blocks of 4 | red |
| AC-008 | each BR-025 cause: works, audited, no Google call | integration | E | `InReadOnlyMode_TheFormExportWorks_IsAudited_AndCallsNoGoogleApi` (3 causes) | `200`; audit row; no Google | red |
| AC-009 | Admin, Dean allowed | security | A | `AnAdminAndADean_ExportTheBlankForm` (2 cases) | `200`; form sheets | red |
| AC-009 | anonymous refused | security | A | `Anonymous_Is401_WithTheApiBody` | `401` API-6 | pass (existing rule) |
| AC-009 | forced password change refused | security | A | `ADeanOnTheForcedPasswordChange_Is403_WithTheApiBody` | `403` | pass (existing rule) |
| AC-009 | no antiforgery token | security | A | `WithoutTheToken_Is400_WithTheApiBody` | `400` `Error.PageExpired` | pass (existing rule) |
| AC-010 | formula-like course, name, title quote-prefixed text | integration | E | `FormulaLikeText_IsWrittenAsQuotePrefixedText` | text, quote prefix, no formula | red |
| AC-011 | synthetic PostgreSQL data, workbook read by sheets and cells | all | E, F | every test of E (Testcontainers, `SeedBlankCourseAsync`, ClosedXML from the response bytes) | — | structural |
| AC-012 | `Cache-Control: no-store` | integration | E | `ASmallReport_FillsOneCopyOfEachSheet_WithTheScreensValues` | header contains `no-store` | red (same test) |
| AC-013 | form shipped | file | F | `TheShippedForm_Exists` | file present | red |
| AC-013 | each marker once at its cell | file | F | `EveryMarker_OccursExactlyOnce_AtItsCell` | 10 markers | red |
| AC-013 | same sheets as the reference | file | F | `TheSheets_AreTheReferencesSheets` | names, order | red |
| AC-013 | no other difference (values, formulas, styles; date formats) | file | F | `EveryOtherCell_IsTheReferences` (2 sheets) | equal but markers / two formats | red |
| AC-013 | widths, heights, page setup; footer margin 0.5 cm | file | F | `WidthsHeightsAndPageSetup_AreTheReferences_ExceptTheFooterMargin` (2 sheets) | equal but footer margin | red |
| AC-013 | course / teachers cells do not wrap | file | F | `TheCourseAndTeachersCells_DoNotWrap` | wrap off | red |
| AC-013 | capacity areas inside the lines | file | F | `TheCapacityAreas_LieInsideTheDrawnLines` | 30 rows, 24 columns, 23 rows end on the last line | red |
| AC-013 | embedded resource is the shipped file (I-1, S-05) | file | F | `TheInfrastructureAssembly_EmbedsTheShippedForm` | one resource, same bytes | red |
| AC-014 | 110 sheets refused | integration | E | `MoreThanAHundredSheets_IsRefused_WithoutFileOrAudit` | `400` `TooManySheets`, no field, no audit | red |
| AC-014 | exactly 100 exported | integration | E | `ExactlyAHundredSheets_IsExported` | 100 sheets | red |
| AC-014 | 404 before the limit | integration | E | `AnUnknownCourse_Is404_BeforeTheSheetLimit` | `404` | pass (existing order) |
| AC-014 | refusal logged; message names the limit | logging, localization | L, T | `TooManySheets_IsAWarningNamingTheRule`, `TheTooManySheetsMessage_NamesTheLimit` | Warning; "100" in uk / en | red |
| AC-015 | footers per kind, digits, `1` for a single copy, header empty | integration | E | `AssertFooter` inside every copy assertion — `ASmallReport_…` (1), `ALargeReport_…` (1 … 6, 1 … 2), `APartlyFilledCopy_…` | right / left; others empty | red |

Requirements beyond the ACs:

| Requirement | Class | Test method | Status |
|---|---|---|---|
| FR-004 layout choice, program preselected | P | `TheExportBlock_OffersTheLayoutChoice_ProgramPreselected` (Admin, Dean) | red |
| FR-004, api-design §2.7 limits from `blankForm` | P | `TheBlankInputs_CarryTheLimitsAndDefaults` | red |
| FR-004, OD-007 a nothing remembered | P | `NothingIsRemembered` | red |
| FR-012 labels uk / en | P | `TheLayoutChoice_IsTranslated_InUkrainian`, `TheLayoutChoice_IsTranslated_InEnglish` | red |
| FR-004, I-3 fill method not shown | P | `TheFillMethod_IsNotShown` | pass (vacuous before implementation; meaningful once the choice is rendered) |
| FR-007, I-8 orientation validated, not applied | E | `WithTheForm_AnUnknownOrientation_IsStillRefused` (pass — existing US-028 rule), `WithTheForm_LandscapeIsNotApplied` (red) | mixed |
| FR-010, I-11 audit row | E | `AFormExport_WritesOneAuditRow_WithoutPersonalData` | red |
| FR-011 log line fields | L | `ASuccessfulBlankExport_LogsTheLayoutAndValues_AndNoPersonalData` | red |

Every AC-001 … AC-015 has at least one mapped test.
