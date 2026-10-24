---
artifact_type: ac_test_matrix
story: US-054
version: 1
status: DRAFT
created_at: 2026-10-24T21:00:00Z
updated_at: 2026-10-24T21:00:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-054-technical-account-superadmin-texts.md
    version: null
  - path: docs/specifications/US-054-spec.md
    version: 1
  - path: docs/tests/US-054-test-strategy.md
    version: 1
supersedes: null
---

# US-054 Acceptance Criteria → Test Matrix

New class `tests/ClassroomAgent.Tests/Web/Pages/TechnicalAccountSuperAdminTextsTests.cs` (**TAS**).
Existing classes: **CITA** `Web/Pages/ConnectionInstructionTechnicalAccountTests`, **CITR**
`Web/Localization/ConnectionInstructionTranslationTests`, **ACTR**
`Web/Localization/AccessCheckTranslationTests`. All integration (TC-2). Status is the red-phase
result recorded in `docs/evidence/US-054-test-generation-report.md`.

| AC | Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | `SuperAdminRole` has the binding wording, uk/en | Integration | TAS | `EveryRewrittenEntry_HasTheBindingWording` (SuperAdminRole × uk/en) | equals §4.1 | red |
| AC-001 | Instruction says super-admin role + sign-in closed | Integration | CITA | `ThePage_SaysTheAccountGetsTheSuperAdminRoleWithSignInClosed` | both statements on page | red |
| AC-001 | Rendered in English | Integration | CITR | `TheInstruction_RendersInEnglishForAnEnglishAccount` | en `SuperAdminRole` on page | red |
| AC-002 | Lead + five measures have the binding wording, uk/en | Integration | TAS | `EveryRewrittenEntry_HasTheBindingWording` (MeasuresLead, Measure.* × uk/en) | equals §4.1 | red |
| AC-002 | Statements and measures in FR-001 order in the handed-over text | Integration | TAS | `TheHandedOverText_MakesEveryStatementInOrder` (uk, en) | strictly increasing positions | red |
| AC-002 | Measures nested under the lead | Integration | TAS | `TheMeasures_AreNestedUnderTheLeadStatement` (uk, en) | measure indent > statement indent | red |
| AC-002 | Each statement on the page | Integration | CITA | `EveryTechnicalAccountStatement_IsOnThePage` (10 keys) | present | red (new keys) |
| AC-003 | Removed keys absent from both files | Integration | TAS | `TheRemovedStatements_ExistInNeitherLanguageFile` (uk, en) | key absent | red |
| AC-003 | No pre-v97 value on the instruction | Integration | TAS | `TheInstruction_CarriesNoRetiredStatement` (uk, en) | none present | red |
| AC-003 | No pre-v97 value/hint on the connection page | Integration | TAS | `TheConnectionPage_ShowsTheV97Hint` (uk, en) | none present | red |
| AC-003 | No delegated role name in entries / on page | Integration | CITA | `NoWorkspaceAdminRoleName_AppearsInEitherLanguage`, `NoWorkspaceAdminRoleName_ReachesThePage` | absent | red (new keys missing) |
| AC-004 | `NoPersonBehindIt` reworded to binding wording | Integration | TAS | `EveryRewrittenEntry_HasTheBindingWording` (NoPersonBehindIt × uk/en) | equals §4.1 | red |
| AC-004 | CreatedBySuperAdmin, NoPersonBehindIt, EnteredInSettings in order | Integration | TAS | `TheHandedOverText_MakesEveryStatementInOrder` | present, ordered | red |
| AC-004 | No technical-account address printed | Integration | CITA | `ThePage_DoesNotInventATechnicalAccountAddress` | absent | green (unchanged behaviour) |
| AC-005 | Copied text = the page's statements, same order and nesting, rendered once | Integration | TAS | `TheHandedOverText_MakesEveryStatementInOrder`, `TheMeasures_AreNestedUnderTheLeadStatement` | as above | red |
| AC-006 | Hint has the binding wording, uk/en | Integration | TAS | `EveryRewrittenEntry_HasTheBindingWording` (Hint × uk/en) | equals §4.1 | red |
| AC-006 | Connection page shows the v97 hint, uk/en | Integration | TAS | `TheConnectionPage_ShowsTheV97Hint` | present | red |
| AC-007 | `TechnicalAccountCannotRead` binding wording, uk/en | Integration | TAS | `EveryRewrittenEntry_HasTheBindingWording` (CannotRead × uk/en) | equals §4.1 | red |
| AC-007 | Access-check step shows super-admin-role advice, not read-only rights | Integration | TAS | `TheAccessCheck_TellsTheSchoolToGrantTheSuperAdminRole` (uk, en) | new advice; old absent | red |
| AC-007 | "Last synchronization" block shows the same advice | Integration | TAS | `TheLastSynchronizationBlock_TellsTheSchoolToGrantTheSuperAdminRole` (uk, en) | new advice; old absent | red |
| AC-008 | Every instruction key exists, non-empty, translated, ≤600 chars | Integration | CITR | `EveryKey_ExistsInBothLanguages`, `EveryKey_IsReallyTranslated`, `NoSingleEntry_CarriesTheWholeInstruction` | pass for all keys | red (new keys) |
| AC-008 | Existing statement-list tests cover the new keys | Integration | `ConnectionInstructionPageTests`, `ConnectionInstructionHandoverTests` | `WithNoSuccessfulLegitimacyCheck_TheSchoolIndependentPartsAreComplete`, `TheHandedOverText_CarriesTheTechnicalAccountRequirements` | all statements present | red (new keys) |
| AC-008 | Access-check messages name no delegated role | Integration | ACTR | `NoMessage_NamesAWorkspaceAdminRole` | absent | green |
| AC-008 | Other connection-instruction, access-check, connection-page, sync tests stay green | Integration | existing classes | (unchanged) | green | green |
