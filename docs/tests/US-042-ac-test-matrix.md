---
artifact_type: ac_test_matrix
story: US-042
version: 1
status: DRAFT
created_at: 2026-10-05T12:49:19Z
updated_at: 2026-10-05T12:49:19Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-042-names-in-report.md
    version: null
  - path: docs/specifications/US-042-spec.md
    version: 1
  - path: docs/designs/api/US-042-openapi.yaml
    version: 1
  - path: docs/designs/database/US-042-db-design.md
    version: 1
  - path: docs/tests/US-042-test-strategy.md
    version: 1
supersedes: null
---

# US-042 Acceptance Criteria → Tests

Class paths are under `tests/ClassroomAgent.Tests/`. Levels: U = unit
(Application/Domain, ports in memory), I = integration on PostgreSQL, A =
adapter (scripted HTTP), H = HTTP host, S = security, L = logging/localization.
Status is the red-phase state at TEST_WRITING (R = red, awaits implementation;
G = green by design, see the report §4).

| AC | Scenario | Lvl | Test class | Test method | Expected | St |
|---|---|---|---|---|---|---|
| AC-001 | Parts normalised (trim, blank → none) | U | Application/UseCases/ParticipantNamePartsTests | ImportNormalisesBothParts_AsTheFullName (5) | as full name | R |
| AC-001 | 750 bound kept / cut | U | ParticipantNamePartsTests | BothParts_AreCutToTheirBound | cut at 750 | R |
| AC-001 | Update replaces all values | U | ParticipantNamePartsTests | UpdateFrom_ReplacesEveryValue | removed part → null | R |
| AC-001 | Sync imports parts, none when absent | U | ParticipantNamePartsTests | ARosterEntry_IsImportedWithItsNameParts | stored | R |
| AC-001 | Next run replaces / clears | U | ParticipantNamePartsTests | TheNextRun_ReplacesChangedAndRemovedParts | replaced | R |
| AC-001 | Google profile → parts | A | Infrastructure/Google/GoogleClassroomReaderTests | AProfileName_IsCarriedThroughAsSurnameAndGivenName | mapped | R |
| AC-001 | Columns exist, bounds | I | Web/Persistence/ClassroomParticipantSchemaTests | TheMigration_CreatesTheTable | 8 columns | R |
| AC-001 | EF round trip | I | Infrastructure/Persistence/NamePartsPersistenceTests | AParticipantsNameParts_RoundTrip | stored, cleared | R |
| AC-001 | Report query returns parts | I | Infrastructure/Persistence/JournalFieldSourceTests | Members_ComeWithTheirRole | parts read | R |
| AC-002 | FR-003 table, students | U | Application/UseCases/ReportPersonNameTests | AStudent_IsNamedByTheFr003Table (8) | per row | R |
| AC-002 | FR-003 table, teachers | U | ReportPersonNameTests | ATeacher_IsNamedByTheSameTable (8) | per row | R |
| AC-002 | Teachers ordered, header | U | ReportPersonNameTests | Teachers_AreNamedAndOrderedByTheSameRule | ordered | R |
| AC-002 | Report rows and header | U | Application/UseCases/ReportContentTests | Rows_AreTheStudentsOfThePeriod; TheHeader_NamesTheCourseThePeriodAndTheTeachersOfThePeriod | "Surname Name" | R |
| AC-002 | Over HTTP | H | Web/Pages/ReportNameSourcePageTests | TheQuerySource_DecidesWhichNameIsShown | profile shown | R |
| AC-002 | Seeded journal shows email part | H | Web/Pages/ReportPageTests | TheBuiltInReport_ShowsGradingAndLessonTopics | "student.one" | R |
| AC-003 | Email part exactly as stored | U | ReportPersonNameTests | TheEmailPart_IsTheStoredAddressUpToItsFirstAt (5) | up to first @ | R |
| AC-003 | Email source over profile | U | ReportPersonNameTests | AStudent_IsNamedByTheFr003Table (email rows) | email part | R |
| AC-004 | By surname, uk collation, unnamed last | U | ReportPersonNameTests | WithTheProfile_StudentsAreOrderedBySurname_ThenTheUnnamed | ordered | R |
| AC-004 | Ties by id | U | ReportPersonNameTests | EqualShownNames_FollowTheInternalId | id order | R |
| AC-004 | Email source orders by email part | U | ReportPersonNameTests | WithTheEmail_StudentsAreOrderedByTheEmailPart | ordered | R |
| AC-005 | Built-in uses profile | U | Application/UseCases/ReportTemplateNameSourceTests | TheBuiltIn_UsesTheProfile | Profile | G |
| AC-005 | Built-in report → profile | U | Application/UseCases/ReportNameSourceTests | WithoutTheParameter_TheBuiltInUsesTheProfile | profile, Template | R |
| AC-005 | Built-in unchangeable | U | ReportTemplateNameSourceTests | TheBuiltInsSetting_CannotBeChanged | refused | G |
| AC-005 | Aggregate stores / changes | U | ReportTemplateNameSourceTests | ATemplate_StoresTheSetting_AndChangeReplacesIt; AnUndefinedSetting_IsRejectedByTheAggregate | stored | R |
| AC-005 | Forms: new / copy / change | U | ReportTemplateNameSourceTests | TheNewForm_StartsWithTheProfile; ACopy_TakesTheSourcesSetting; TheChangeForm_ShowsTheStoredSetting (2) | values | R |
| AC-005 | Create stores; copy switches both ways | U | ReportTemplateNameSourceTests | Create_StoresTheChosenSource (2); Change_SwitchesTheSource_AndAuditsTheTemplateIdOnly (2) | stored | R |
| AC-005 | Existing templates → profile (migration) | I | NamePartsPersistenceTests | TheMigration_GivesExistingTemplatesTheProfile_AndInventsNoNameParts | 'profile' | R |
| AC-005 | Column, check, no default, no index | I | NamePartsPersistenceTests | NameSource_IsANonNullCodeWithACheckAndNoDefault; NoIndex_CoversTheNewColumns; TheMigration_ExistsUnderTheDesignedName | schema | R |
| AC-005 | Repository round trip | I | NamePartsPersistenceTests | ATemplatesNameSource_RoundTripsThroughTheRepository | 'email' → 'profile' | R |
| AC-005 | Template table columns | I | Web/Persistence/ReportTemplateSchemaTests | TheTemplateTables_HaveTheDesignedColumns | + name_source | R |
| AC-005 | Form round trip over HTTP | H | ReportNameSourcePageTests | ATemplate_IsSavedWithItsNameSource_AndTheChangeFormChecksIt | stored, checked | R |
| AC-006 | Parameter overrides template both ways, nothing written | U | ReportNameSourceTests | TheParameter_OverridesTheTemplate_AndChangesNothing (2) | Page origin | R |
| AC-006 | Template setting applies without parameter | U | ReportNameSourceTests | WithoutTheParameter_ACreatedTemplatesSettingApplies | Template origin | R |
| AC-006 | Switch links and current mark | U | ReportNameSourceTests | TheSwitch_LinksBothSources_WithTheEffectiveOneCurrent; TheSwitch_MarksThePageParameter; WithoutAReport_ThereIsNoSwitch | links | R |
| AC-006 | Switch over HTTP | H | ReportNameSourcePageTests | TheSwitch_OffersTheOtherSource | links in body | R |
| AC-007 | Read-only: switch works, guard not asked; save refused | U/S | ReportNameSourceTests | InReadOnlyMode_TheSwitchWorks_WhileASaveIsRefused | as described | R |
| AC-007 | Read-only over HTTP | H | ReportNameSourcePageTests | InReadOnlyMode_TheSourceStillApplies | 200 | R |
| AC-007 | Read-only report content | H | ReportPageTests | InReadOnlyMode_TheReportIsStillShown_WithTheSameContent | 200 | R |
| AC-008 | One change row, template id only | U | ReportTemplateNameSourceTests | Change_SwitchesTheSource_AndAuditsTheTemplateIdOnly; Change_WithTheSettingUnchanged_IsAuditedTheSameWay | 1 row | R |
| AC-009 | Admin, Dean allowed (3 endpoints) | S | Web/Security/ReportNameSourceAuthorizationTests | AnAdminAndADean_CanUseTheNameSource (6) | 200 / 302 list | G |
| AC-009 | Anonymous → sign-in | S | ReportNameSourceAuthorizationTests | Anonymous_IsSentToSignIn (3) | 302 | G |
| AC-009 | Restricted Dean → change password | S | ReportNameSourceAuthorizationTests | ARestrictedDean_IsSentToTheForcedChange (3) | 302 | G |
| AC-010 | Malformed `names` in address (10 shapes) | U | ReportNameSourceTests | AMalformedParameter_IsRefused_BeforeAnythingIsRead (10); AMalformedParameter_IsReported_WithoutACourse; TheNameSourceKey_ComesAfterThePeriodKeys | Invalid, last key | R |
| AC-010 | Invalid `names` in form (8 shapes) | U | ReportTemplateNameSourceTests | Create_WithAnInvalidSource_ReRendersTheForm (8); Change_WithAnInvalidSource_ReRendersTheForm (8); AnInvalidSource_IsReportedWithTheOtherFieldErrors | field error | R |
| AC-010 | Over HTTP, not echoed | H | ReportNameSourcePageTests | AMalformedSource_Is400_WithTheMessage_AndIsNotEchoed; AnInvalidSource_ReRendersTheForm_AndStoresNothing | 400 | R |
| AC-010 | Value never logged | L | Web/Logging/ReportNameSourceLoggingTests | AMalformedSource_IsAWarningNamingTheRule_AndTheValueIsNeverLogged; ARejectedFormValue_IsNeverLogged | warning, no value | R |
| AC-011 | Translated keys both languages | L | Web/Localization/ReportNameSourceTranslationTests; ReportTemplateTranslationTests | EveryNewKey_ExistsInBothLanguages | uk ≠ en | R |
| AC-011 | Names as Google holds them | U | ReportPersonNameTests | AProfileName_IsShownAsGoogleHoldsIt | not corrected | R |
| AC-012 | Synthetic data, PostgreSQL, ports substituted | I | every class above | — | TC-2, TC-4 | — |
| AC-013 | One profile part; nameless label | U | ReportPersonNameTests | AStudent_IsNamedByTheFr003Table (rows 2, 3, 7, 8) | per FR-003 | R |
| AC-014 | Return path keeps a valid source | U | ReportNameSourceTests | TheReturnPath_KeepsAValidParameter; TheReturnPath_CarriesNoSourceWhenTheAddressHadNone | per §2.4 | R |
| AC-014 | Over HTTP | H | ReportNameSourcePageTests | TheLanguageSwitcher_KeepsTheSourceInItsReturnPath | names=email | R |
| FR-011 | Built line records source and origin; no names in logs | L | ReportNameSourceLoggingTests | TheBuiltReport_RecordsThePageSourceAndItsOrigin; TheBuiltReport_RecordsTheTemplateSourceAndItsOrigin_WhenThePageHasNone; NoLogLine_CarriesAProfileNameOrAnEmailPart | SC-10 | R |
| FR-006 §9 | Round trips unchanged | U | ReportPersonNameTests | NamingReadsNothingMore | 4 calls | R |

Every Acceptance Criterion AC-001 … AC-014 maps to at least one test.
