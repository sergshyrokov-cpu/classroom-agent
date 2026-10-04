---
artifact_type: ac_test_matrix
story: US-027
version: 1
status: DRAFT
created_at: 2026-10-04T20:00:00Z
updated_at: 2026-10-04T20:00:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-027-spec.md
    version: 2
  - path: docs/designs/api/US-027-openapi.yaml
    version: 1
  - path: docs/designs/database/US-027-db-design.md
    version: 1
  - path: docs/tests/US-027-test-strategy.md
    version: 1
supersedes: null
---

# US-027 Acceptance Criteria → Test Matrix

Test classes live under `tests/ClassroomAgent.Tests/`; short names below. Status
is the state **before IMPLEMENTATION** (red = fails for missing behaviour).
Levels: U unit, I PostgreSQL integration, H HTTP integration, S security.

## 1. Classes

| Short | Class | Level |
|---|---|---|
| INV | `Application/UseCases/ReportTemplateInvariantTests` | U |
| VAL | `Application/UseCases/ReportTemplateFormValidationTests` | U |
| SAVE | `Application/UseCases/ReportTemplateSaveTests` | U |
| DEL | `Application/UseCases/ReportTemplateDeleteTests` | U |
| FORM | `Application/UseCases/ReportTemplateFormQueryTests` | U |
| LIST | `Application/UseCases/ListReportTemplatesQueryTests` | U |
| RQV | `Application/UseCases/ReportRequestValidationTests` | U |
| RCON | `Application/UseCases/ReportContentTests` | U |
| RCELL | `Application/UseCases/ReportCellTests` | U |
| SCHED | `Application/UseCases/CourseWorkScheduledTimeTests` | U |
| GCR | `Infrastructure/Google/GoogleClassroomReaderTests` (one method added) | U (scripted HTTP) |
| JFS | `Infrastructure/Persistence/JournalFieldSourceTests` | I |
| REPO | `Infrastructure/Persistence/ReportTemplateRepositoryTests` | I |
| SCHEMA | `Web/Persistence/ReportTemplateSchemaTests` | I |
| RO | `Web/UseCases/ReportTemplateReadOnlyTests` | S (host DI, Application layer) |
| TPAGE | `Web/Pages/ReportTemplatePagesTests` | H |
| RPAGE | `Web/Pages/ReportPageTests` | H |
| AUTH | `Web/Security/ReportTemplateAuthorizationTests` | S |
| L10N | `Web/Localization/ReportTemplateTranslationTests` | H |
| LOG | `Web/Logging/ReportTemplateLoggingTests` | H |

## 2. Matrix

| AC | Scenario | Lvl | Class | Test method | Expected | Status |
|---|---|---|---|---|---|---|
| AC-001 | list starts with the built-in, marked, no author, `CanChange` false | U | LIST | `TheBuiltInTemplate_IsFirst_MarkedBuiltIn_WithoutAuthorOrDate` | first item `academic-journal`, `IsBuiltIn`, no change | red |
| AC-001 | edit form of the built-in refused | U | FORM | `TheEditFormOfTheBuiltIn_IsRefused` | `BuiltInNotChangeable` | red |
| AC-001 | change of the built-in refused, nothing written, not audited | U | SAVE | `ChangingTheBuiltIn_IsRefused_AndNotAudited` | `BuiltInNotChangeable`, 0 commits, 0 audit | red |
| AC-001 | delete of the built-in refused, not audited | U | DEL | `DeletingTheBuiltIn_IsRefused_AndNotAudited` | as above | red |
| AC-001 | built-in settings are exactly FR-006 | U | INV | `TheAcademicJournal_HasTheSettingsOfFr006` | short, hidden, preset, 2 h, NotAssigned "—", rest empty, late hidden | red |
| AC-001 | HTTP: edit / change / delete of the built-in → `400` list with message | H | TPAGE | `TheBuiltIn_CannotBeEditedChangedOrDeleted_OverHttp` | `400`, list, translated message | red |
| AC-002 | create stores settings, author = actor | U | SAVE | `AValidForm_CreatesTheTemplate_WithTheActorAsAuthor` | one added, settings match, `AuthorId` = actor | red |
| AC-002 | copy of the built-in saves as an ordinary template | U | SAVE | `ACopyOfTheBuiltIn_IsSavedAsAnOrdinaryTemplate` | created, preset rows stored | red |
| AC-002 | change replaces settings, author unchanged, `MarkChanged` called | U | SAVE | `AChange_ReplacesTheSettings_KeepsTheAuthor` | settings new, author old | red |
| AC-002 | any Dean changes a template another account created | U | SAVE | `AnotherAccount_CanChangeTheTemplate` | succeeded | red |
| AC-002 | change of an unknown / deleted template → not found | U | SAVE | `ChangingAMissingTemplate_IsNotFound` | `NotFound`, no audit | red |
| AC-002 | delete removes; second delete not found | U | DEL | `ATemplate_IsDeleted_AndASecondDeleteIsNotFound` | removed; `NotFound` | red |
| AC-002 | new form defaults FR-007 | U | FORM | `TheNewForm_HasTheDefaultsOfFr007` | full, false, none, 2, all program | red |
| AC-002 | copy form of built-in: name + suffix, settings of FR-006 | U | FORM | `TheCopyFormOfTheBuiltIn_IsPrefilled_WithTheTranslatedNameAndSuffix` | name `"<built-in> (copy)"`, short, true, ranges 12 rows | red |
| AC-002 | copy form of a created template | U | FORM | `TheCopyFormOfACreatedTemplate_CarriesItsSettings` | values = source | red |
| AC-002 | edit form, delete page | U | FORM | `TheEditForm_AndTheDeletePage_ShowTheTemplate` | values; name as written | red |
| AC-002 | created templates ordered by name in UI collation, then id; author email | U | LIST | `CreatedTemplates_AreOrderedByName_ThenId_WithTheirAuthor` | order; email; `ChangedAt` in Kyiv | red |
| AC-002 | HTTP create → `302` list with confirmation; row stored with author | H | TPAGE | `ATemplate_IsCreatedOverHttp_AndListedWithItsAuthor` | `302 /reports/templates`; row; confirmation | red |
| AC-002 | HTTP copy, change, delete with confirmation page | H | TPAGE | `ATemplate_IsCopiedChangedAndDeletedOverHttp` | `302`s; rows; confirmation page names it | red |
| AC-002 | Admin and Dean both create; shared list | H | AUTH | `AnAdminAndADean_CanUseEveryTemplatePage` | `200`/`302` | red |
| AC-003 | `ScaleOverlap`, `ScaleGap` (inner gap, not from 0, not to 100) with row number | U | VAL | `AnOverlappingScale_IsRefused_NamingTheRow`, `AScaleWithAGap_IsRefused_NamingTheRow` (theory) | key + `RowNumber` | red |
| AC-003 | other scale rules: no rows, 102 rows, bounds, inverted, label | U | VAL | `ScaleRows_AreValidated` (theory), `OneRowCoveringEverything_AndOneHundredAndOneRows_AreValid` | keys; valid | red |
| AC-003 | `none` ignores rows | U | SAVE | `NoConversion_IgnoresAnyScaleRowsSent` | no rows stored | red |
| AC-003 | label shown for a range; raw points for no conversion | U | RCELL | `AGrade_IsShownThroughTheScale`, `NoConversion_ShowsRawPoints` | `ScaleLabel` / `RawPoints` | red |
| AC-003 | HTTP: overlapping table → `400` form with values and message | H | TPAGE | `AnInvalidScale_ReRendersTheForm_WithTheValuesAndTheMessage` | `400`; message; nothing stored | red |
| AC-004 | built-in report: columns by lesson date then title then id, materials excluded | U | RCON | `TheBuiltInReport_HasNoMaterialColumns_AndOrdersByLessonDateThenTitle` | order; no material | red |
| AC-004 | rows: students of the period (BR-051), submitter-only leaver, teachers never rows | U | RCON | `Rows_AreTheStudentsOfThePeriod` | as US-025 FR-005 | red |
| AC-004 | header: course, section, period, teachers of the period (I-6), unnamed teacher | U | RCON | `TheHeader_NamesTheCourseThePeriodAndTheTeachersOfThePeriod` | teachers; ex-teacher absent | red |
| AC-004 | lesson topics: non-material items incl. ungraded, date, title, hours | U | RCON | `LessonTopics_ListEveryNonMaterialItem_WithTheTemplatesHours` | rows; hours | red |
| AC-004 | empty states: nothing published; no students | U | RCON | `NothingPublished_ReplacesBothParts`, `NoStudents_KeepsLessonTopics` | keys | red |
| AC-004 | HTTP: built-in report of the seeded journal | H | RPAGE | `TheBuiltInReport_ShowsGradingAndLessonTopics` | `200`; titles, "11" for 8.5/10, no material, teacher in header | red |
| AC-005 | copy with own marks / late / full view / hours / materials shown changes the report; built-in report unchanged | U | RCON | `ACopysSettings_ShowInItsReport_AndTheBuiltInIsUnchanged` | differing cells | red |
| AC-005 | every state mark kind | U | RCELL | `EveryStateMark_IsShownAsTheTemplateSays` (theory) | program key / own text / empty | red |
| AC-005 | full view: draft through scale, turn-in date; short: neither | U | RCELL | `TheFullView_AddsDraftAndTurnInDate_TheShortViewDoesNot` | fields set / null | red |
| AC-005 | late mark program / own / hidden | U | RCELL | `TheLateMark_FollowsTheTemplate` (theory) | late set / null | red |
| AC-005 | unrecognised state with program → raw value, never grade | U | RCELL | `AnUnrecognisedState_ShowsTheRawValue_WithTheProgramsChoice` | `RawState` | red |
| AC-005 | material cell empty | U | RCELL | `AMaterialCell_IsAlwaysEmpty` | `Empty` | red |
| AC-006 | create / change / delete refused for all three causes, guard first, nothing changed, refused audit row | S | RO | `WritesAreRefusedInApplication_InEveryReadOnlyCause` (theory ×3) | `ReadOnlyModeException`; fingerprint unchanged; 3 refused rows | red |
| AC-006 | list, forms, report work in every cause; no Google call | S | RO | `ViewingWorksInEveryReadOnlyCause_WithoutGoogle` (theory ×3) | results; `FakeClassroomReader` 0 calls | red |
| AC-006 | guard runs before any repository call (unit) | U | SAVE / DEL | `InReadOnlyMode_TheGuardRunsFirst_AndOnlyTheRefusalIsWritten` | calls empty; one audit row | red |
| AC-006 | HTTP: `409` page naming the reason | H | TPAGE | `InReadOnlyMode_ASaveIs409_AndNothingIsStored` | `409` | red |
| AC-007 | create/change/delete rows: action, actor, role, target type/id, no text | U | SAVE / DEL | `TheCreate_IsAuditedWithTheTemplateIdOnly`, `TheChange_IsAudited`, `TheDelete_IsAudited` | one row each | red |
| AC-007 | refused audit target id rule (null create; id for numeric ref; null for built-in / malformed) | U | SAVE / DEL | `TheReadOnlyRefusal_TargetsTheReferenceOnlyWhenItIsAnId` (theory) | target id | red |
| AC-007 | viewing, forms, validation failure, not found write no audit | U | SAVE / RQV | `AValidationFailure_WritesNothing`, `TheReport_WritesNoAuditRow` | 0 rows | red |
| AC-007 | the shape check accepts the rows the use cases write and refuses others | I | SCHEMA | `TheAuditShapeCheck_AcceptsTemplateRows_AndRefusesMisshapenOnes` | inserts ok / `23514` | red |
| AC-007 | HTTP: audit row after create | H | TPAGE | `ATemplate_IsCreatedOverHttp_AndListedWithItsAuthor` | one row `report_template_created` | red |
| AC-008 | every endpoint: anonymous → `/sign-in`, temporary-password Dean → change-password | S | AUTH | `Anonymous_IsSentToSignIn_OnEveryEndpoint` (theory), `ARestrictedDean_IsSentToTheForcedChange_OnEveryEndpoint` (theory) | `302` | red* |
| AC-008 | Admin and Dean allowed on every endpoint | S | AUTH | `AnAdminAndADean_CanUseEveryTemplatePage` (theory) | `200` / `302` | red |
| AC-008 | missing antiforgery token → `400` on the three `POST`s | S | AUTH | `APostWithoutTheAntiforgeryToken_Is400` (theory) | `400`; nothing stored | red |
| AC-008 | new endpoints classified protected | S | (existing) `PublicPortAuthorizationTests.OnlySc4EndpointsAllowAnonymous` | — | stays green with the pages added | green |
| AC-009 | every `ReportTemplateFieldErrorKey` with field name | U | VAL | `TheName_IsValidated` (theory), `MarkTexts_AreValidated` (theory), `Hours_AreValidated` (theory), `ScaleRows_AreValidated` (theory), `ControlCharacters_AreRefusedInEveryText` (theory) | key + field | red |
| AC-009 | tampered forms → `FormMalformed` | U | VAL | `ATamperedForm_IsMalformed` (theory) | outcome | red |
| AC-009 | duplicate name, trimmed and case-insensitive; equal to built-in name allowed; self excluded on change; commit-time violation → `NameNotUnique` | U | VAL / SAVE | `ADuplicateName_IsRefused_IgnoringCaseAndSpaces`, `TheBuiltInsName_MayBeUsed`, `AChange_MayKeepItsOwnName`, `ADuplicateAtCommit_IsReportedAsNameNotUnique` | key | red |
| AC-009 | report query: every `ReportMessageKey`, order, both unknown, no read before validation, malformed not echoed, return path from validated values | U | RQV | `AMalformedTemplateReference_IsRefused` (theory), `TheQueryRulesOfUs025_Apply` (theory), `AnUnknownTemplateOrCourse_IsNotFound`, `NothingIsReadForTheReport_WhenTheQueryIsInvalid`, `TheReturnPath_CarriesOnlyValidatedValues` | keys; calls | red |
| AC-009 | HTTP: invalid form `400`, tampered `400` error page, malformed report query `400` not echoed | H | TPAGE / RPAGE | `AnInvalidScale_ReRendersTheForm_WithTheValuesAndTheMessage`, `ATamperedForm_IsTheErrorPage`, `AMalformedQuery_Is400_AndIsNotEchoed`, `AnUnknownTemplate_Is404` | status, body | red |
| AC-009 | validation failure logged as field + rule, never the value | H | LOG | `AValidationFailure_IsLoggedWithoutTheValue` | warning; value absent | red |
| AC-010 | new keys in both resource files | H | L10N | `EveryNewKey_ExistsInBothLanguages` | none missing | red |
| AC-010 | English rendering; template text and Google text as written | H | RPAGE / TPAGE | `TheReport_RendersInEnglish_AndLeavesSchoolAndGoogleTextAsWritten`, `ATemplateName_IsHtmlEncoded` | English headings; `<script>` encoded | red |
| AC-011 | no use case reaches Google; data synthetic in PostgreSQL | S | RO | `ViewingWorksInEveryReadOnlyCause_WithoutGoogle` | 0 Google calls | red |
| AC-011 | no Google port in any US-027 constructor | U | INV | `NoUseCaseOfThisStory_TakesAGooglePort` | reflection over constructors | green† |
| AC-012 | lesson date = scheduled, else creation, else item date; half-open period; F4 follows F2 | I | JFS | `TheLessonDate_IsScheduledThenCreationThenItemDate`, `ThePeriod_IsHalfOpen_ByLessonDate`, `Submissions_FollowTheLessonsOfThePeriod`, `Members_ComeWithTheirRole` | sets | red |
| AC-012 | the three examples of AC-012 in Kyiv | I | JFS | `TheExamplesOfAc012_HoldInKyiv` | Sept/Oct membership | red |
| AC-012 | report columns dated by lesson date in the school zone; interval from Kyiv dates | U | RCON | `ColumnsAreDatedByTheLessonDate_InTheSchoolsZone` | dates; interval | red |
| AC-012 | `ScheduledTime` stored on import and update; null when absent | U | SCHED | `TheScheduledTime_IsStoredOnImportAndUpdate` | property | red |
| AC-012 | the adapter passes `scheduledTime` through | U | GCR | `TheScheduledTime_IsPassedThroughTheAdapter` | details value | red |
| AC-012 | `course_work.scheduled_time` exists, `timestamptz`, nullable | I | SCHEMA | `CourseWork_HasANullableScheduledTime` | column | red |
| AC-013 | 0.85/10 → "2"; 0.84/10 → "1"; 10.5/10 → "12"; 0/10 → raw `0 / 10`; maximum 0 → raw | U | RCELL | `RoundingAtARangeBoundary_FollowsAc013` (theory), `AZeroMaximum_ShowsRawPoints` | labels | red |
| AC-014 | template survives author deletion, listed as `AccountDeleted` | U | LIST | `ATemplateWhoseAuthorWasDeleted_IsListedAsAccountDeleted` | state | red |
| AC-014 | PostgreSQL: delete the `app_user` row → template stays, list record email null, report still usable | I | REPO | `ATemplate_SurvivesTheDeletionOfItsAuthor` | row; null email | red |

\* The restricted-session redirect happens before routing (US-025 report §4), so
that theory is green before implementation for any path; it stays meaningful
after. † Holds from the skeleton on; a regression guard.

## 3. Persistence and schema (db-design §7)

| db-design | Class | Test method | Status |
|---|---|---|---|
| round trip, ordered rows, `none` without rows | REPO | `ATemplate_RoundTrips_WithItsMarksAndOrderedScale` | red |
| normalized-name uniqueness → exception | REPO | `TwoNamesDifferingOnlyInCaseOrSpaces_ViolateTheUniqueIndex` | red |
| `NameExists` excludes self | REPO | `NameExists_ExcludesTheTemplateItself` | red |
| mark-only change advances `updated_at` | REPO | `ChangingOnlyOneMark_AdvancesTheLastChangeTime` | red |
| scale rows replaced with same `from` values | REPO | `ReplacingScaleRows_WithTheSameBounds_Saves` | red |
| delete cascades to children only | REPO | `Deleting_RemovesTheMarksAndScaleRows_AndNothingElse` | red |
| tables, columns, types, nullability, indexes | SCHEMA | `TheTemplateTables_HaveTheDesignedColumns` | red |
| checks refuse bad rows | SCHEMA | `TheCheckConstraints_RefuseInvalidRows` (theory) | red |
| model has no pending changes (migration shipped) | SCHEMA | `TheModelHasNoPendingChanges` | green‡ |

‡ Green now (the skeleton maps nothing); turns red if IMPLEMENTATION maps the
entities without a migration.

## 4. Coverage

Every AC-001 … AC-014 has at least one test above. Every endpoint of the openapi
(`GET /reports/templates`, `GET …/new`, `GET …/copy`, `GET …/edit`,
`POST /reports/templates`, `POST …/{templateRef}`, `GET`/`POST …/deletion`,
`GET /reports`, the home page link) has status-code tests in TPAGE / RPAGE / AUTH.
