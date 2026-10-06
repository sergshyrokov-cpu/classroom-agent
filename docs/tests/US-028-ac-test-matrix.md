---
artifact_type: ac_test_matrix
story: US-028
version: 1
status: DRAFT
created_at: 2026-10-06T06:02:00Z
updated_at: 2026-10-06T06:02:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-028-spec.md
    version: 1
  - path: docs/designs/api/US-028-openapi.yaml
    version: 1
  - path: docs/designs/database/US-028-db-design.md
    version: 1
  - path: docs/designs/database/US-028-entity-model.md
    version: 1
  - path: docs/tests/US-028-test-strategy.md
    version: 1
supersedes: null
---

# US-028 Acceptance Criteria → Test Matrix

Status: **Red** = compiles, fails for the missing implementation (red phase);
**Green** = passes already (explained in the test-generation report).
Classes are under `tests/ClassroomAgent.Tests/`: `App/UC` = `Application/UseCases`,
`Infra` = `Infrastructure`.

| AC | Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | built-in journal exported as xlsx attachment, two sheets | HTTP | Web/Pages/JournalExportTests | AnAdmin_ExportsTheBuiltInJournal_AsAnXlsxAttachment | 200, xlsx, attachment, sheet names from the text port | Red |
| AC-001 | file shows only what the page shows | HTTP | Web/Pages/JournalExportTests | TheFile_ShowsWhatTheReportPageShows | every cell line is on the page; October item absent | Red |
| AC-001 | workbook = the page's report, mapped (FR-003) | Unit | App/UC/ExportJournalCommandTests | TheWorkbook_IsTheMappedReportOfThePage_ForTheSameInputs | structurally equal | Red |
| AC-001 | sheets, header block, Grading header, rows | Unit | App/UC/ReportWorkbookMapperTests | TheWorkbook_HasGradingThenLessonTopics…; BothSheets_BeginWithTheLabelledHeaderBlock…; TheGradingHeaderRow…; StudentRows_FollowTheReportOrder… | FR-004.1–4.3 layout | Red |
| AC-001 | Lesson topics: date, title, hours, teachers, two empty | Unit | App/UC/ReportWorkbookMapperTests | LessonTopics_HoldADateTheTitleTheHoursTheTeachers_AndTwoEmptyColumns | FR-004.4 | Red |
| AC-001 | empty states | Unit | App/UC/ReportWorkbookMapperTests; ExportJournalCommandTests | NothingPublished_…; NoStudents_…; AnEmptyReport_IsExported_AndAuditedWithZeroRows | FR-004.6 | Red |
| AC-001 | file written by ClosedXML: sheets, kinds, print setup | Unit | Infra/Export/ClosedXmlReportRendererTests | 18 methods (19 cases) | model written as is | Red |
| AC-002 | created template hiding materials | HTTP | Web/Pages/JournalExportTests | ACreatedTemplateHidingMaterials_LeavesTheMaterialOut | material column absent | Red |
| AC-002 | own and program marks, raw state, own late text | Unit | App/UC/ReportWorkbookMapperTests | Marks_AndRawStates_AreText; AnOwnLateMark_IsWrittenAsTheSchoolWroteIt | as on screen | Red |
| AC-002 | full view: draft and turn-in date | Unit | App/UC/ReportWorkbookMapperTests | TheFullView_AddsDraftAndTurnInDate_InTheScreenOrder | screen order | Red |
| AC-003 | whole-number grade alone is a number | Unit | App/UC/ReportWorkbookMapperTests | AWholeNumberGradeAlone_IsANumber (3) | Number | Red |
| AC-003 | other labels, raw points, late mark → text | Unit | App/UC/ReportWorkbookMapperTests | AnyOtherLabel_IsText_AsWritten (6); RawPoints_AreText…; AGradeWithALateMark_IsText_OnTwoLines | Text as on screen | Red |
| AC-003 | empty cell empty | Unit | App/UC/ReportWorkbookMapperTests | AnEmptyReportCell_IsAnEmptyCell | Empty | Red |
| AC-003 | number / date / empty kinds in the file | Unit | Infra/Export/ClosedXmlReportRendererTests | NumberCell_IsWrittenAsNumber; DateCell_…; EmptyCell_StaysEmpty | Excel types | Red |
| AC-004 | English user: English program text, data as written | HTTP | Web/Pages/JournalExportTests | AnEnglishUser_GetsEnglishProgramText_AndUntranslatedData | en sheet name, en built-in name, course/student as written | Red |
| AC-004 | program text from the text port only | Unit | App/UC/ExportJournalCommandTests; ReportWorkbookMapperTests | ProgramText_IsTakenFromTheTextPort; ACreatedTemplateName_AndACourseWithoutSection_AreWrittenAsTheyAre; DatesInText_FollowTheUiLanguage; TheDateFormat_IsTheCulturesShortDate_InExcelLetters (2) | markers / data verbatim | Red |
| AC-004 | every file text and message in uk and en | Localization | Web/Localization/JournalExportTranslationTests | EveryFileText_ResolvesInBothLanguages (22); SheetNames_FitExcelsLimit (2); AProgramMark_ResolvesInBothLanguages; EveryNewMessage_ExistsInBothLanguages (5) | present, differ | Red |
| AC-005 | name = course + period; invalid chars; no person | HTTP | Web/Pages/JournalExportTests | TheFileName_IsTheCourseAndPeriod_AndCarriesNoPerson | `filename*` = `Алгебра 7_А 2026-09-01–2026-09-30.xlsx` | Red |
| AC-005 | name rules | Unit | App/UC/JournalExportFileNameTests | 6 methods (9 cases) | FR-006.1 | Red |
| AC-005 | name built from the course | Unit | App/UC/ExportJournalCommandTests | AValidRequest_ExportsTheRenderedFile_UnderTheCourseAndPeriodName | name and bytes | Red |
| AC-006 | read-only: works, audited, no Google | HTTP | Web/Pages/JournalExportTests | InReadOnlyMode_TheExportWorks_IsAudited_AndCallsNoGoogleApi (3 causes) | 200, one row, no impersonation | Red |
| AC-006 | read-only: guard never asked, audit declared | Unit | App/UC/ExportJournalCommandTests | InReadOnlyMode_TheExportWorks_AndIsAudited_WithoutAskingTheGuard | Exported; declared AuditEvent | Red |
| AC-007 | one audit row, all fields, no personal data | HTTP | Web/Pages/JournalExportTests | AnExport_WritesOneAuditRow_WithoutPersonalData | db-design §3.2 values | Red |
| AC-007 | viewing writes none | HTTP | Web/Pages/JournalExportTests | ViewingTheReport_WritesNoAuditRow | count unchanged | Green (regression guard) |
| AC-007 | row fields, created template, commit | Unit | App/UC/ExportJournalCommandTests | ASuccessfulExport_WritesOneAuditRow…; TheAuditRow_IsCommittedOnce…; ACreatedTemplate_IsRecordedByItsId; AFailedRender_LeavesNoAuditRow | FR-010 | Red |
| AC-007 | factory fields and refusals | Unit | App/UC/JournalExportedAuditEventTests | 4 methods | entity model §1.1 | Red |
| AC-007 | schema: columns and constraints | PostgreSQL | Infra/Persistence/JournalExportAuditSchemaTests | 14 methods | db-design §3 | Red |
| AC-008 | protected, POST only, token required | Security | Web/Security/JournalExportAuthorizationTests | TheExport_IsAProtectedPostOnlyEndpoint | not anonymous, not exempt, POST | Red |
| AC-008 | allowed roles | HTTP | Web/Pages/JournalExportTests | AnAdmin_ExportsTheBuiltInJournal…; ADean_ExportsTheJournal | 200 | Red |
| AC-008 | forbidden: anonymous, restricted Dean, no token, GET | Security | Web/Security/JournalExportAuthorizationTests | Anonymous_Is401_WithTheApiBody; ADeanOnTheForcedPasswordChange_Is403_WithTheApiBody; WithoutTheToken_Is400_WithTheApiBody (2); AGet_Is405_WithTheApiBody | API-6 JSON, no audit | Red |
| AC-008 | `/api/v1` rules; outside unchanged | Security | Web/Security/JournalExportAuthorizationTests | AnUnknownApiPath_Is404_WithTheApiBody; TheApiMessage_IsInTheUsersLanguage; OutsideTheApi_AnonymousStillGoesToSignIn | JSON under /api/v1; redirect elsewhere | Red / Green (last, regression guard) |
| AC-009 | malformed value: 400 naming field, no echo, no audit | HTTP | Web/Pages/JournalExportTests | AMalformedValue_Is400_NamingTheField_WithoutEchoOrAudit; AnUnknownOrientation_Is400_WithItsOwnMessage | API-6 `fieldErrors` | Red |
| AC-009 | unknown course / template: 404 | HTTP | Web/Pages/JournalExportTests | AnUnknownCourse_Is404_NamingIt_WithoutAudit; AnUnknownTemplate_Is404_NamingIt | API-6 | Red |
| AC-009 | malformed body, form body | HTTP | Web/Pages/JournalExportTests | AMalformedBody_Is400_WithoutFieldErrors (4); AFormBody_Is415 | 400 / 415 | Red |
| AC-009 | validation before any read; order; not found | Unit | App/UC/ExportJournalCommandTests | AMalformedInput_IsRefused_BeforeAnythingIsRead (14); SeveralMalformedInputs_AreAllListed_InTheContractOrder; AnUnknownCourseOrTemplate_IsNotFound… (3) | Invalid / NotFound, nothing read | Red |
| AC-009 | value never logged; rule named | Logging | Web/Logging/JournalExportLoggingTests | ARefusedValue_IsAWarningNamingTheRule_AndTheValueIsNeverLogged | Warning, no value | Red |
| AC-010 | `Cache-Control: no-store` | HTTP | Web/Pages/JournalExportTests | AnAdmin_ExportsTheBuiltInJournal_AsAnXlsxAttachment | header contains no-store | Red |
| AC-011 | synthetic data, PostgreSQL, no Google, workbook read from bytes | all HTTP / schema classes | — | — | Testcontainers; `JournalExportHostExtensions.Workbook` reads in memory | Red |
| FR-002 | page action, meta tag, orientation, data attributes; none without a report | HTTP | Web/Pages/JournalExportTests | TheReportPage_OffersTheExport_WithTheTokenAndOrientation; TheReportPage_OffersNoExport_WithoutAReport | markup present / absent | Red / Green (second, regression guard) |
| FR-004.7 | formula guard | Unit | App/UC/ReportWorkbookMapperTests; Infra/Export/ClosedXmlReportRendererTests | FormulaLikeText_GetsTheQuotePrefix_AndKeepsItsValue (6); OrdinaryText_HasNoQuotePrefix; TextStartingWithEquals_IsNeverAFormula; QuotePrefix… (2) | never a formula | Red |
| FR-004.8 | orientation, fit width, repeat, freeze | Unit / HTTP | ReportWorkbookMapperTests; ExportJournalCommandTests; ClosedXmlReportRendererTests; JournalExportTests | TheOrientation_IsCarried (2); TheOrientation_IsTheRequestedOne_PortraitByDefault (4); Orientation_IsAppliedToEverySheet (2); TheChosenOrientation_IsTheFilesPageSetup (2); … | page setup | Red |
| FR-004.9 | 32 767 cut | Unit | App/UC/ReportWorkbookMapperTests | ALongText_IsCutToTheExcelLimit | cut | Red |
| FR-012 | success log line | Logging | Web/Logging/JournalExportLoggingTests | ASuccessfulExport_LogsOneInformationLine_WithIdsAndCounts_AndNoPersonalData | ids, size, no names | Red |
