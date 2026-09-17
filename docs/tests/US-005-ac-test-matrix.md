---
artifact_type: ac_test_matrix
story: US-005
version: 1
status: DRAFT
created_at: 2026-09-17T14:25:00Z
updated_at: 2026-09-17T14:25:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-005-installation-legitimacy-check.md
    version: null
  - path: docs/specifications/US-005-spec.md
    version: 1
  - path: docs/designs/api/US-005-openapi.yaml
    version: 1
  - path: docs/designs/database/US-005-db-design.md
    version: 1
supersedes: null
---

# US-005 Acceptance Criteria → Test Matrix

Status after TEST_WRITING: **RED** = fails for missing implementation (expected),
**GUARD** = passes already and must stay green. Namespaces are relative to
`ClassroomAgent.Tests`. Test strategy: `docs/tests/US-005-test-strategy.md`.

| AC | Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | valid settings start the host | Integration | `Web.Configuration.InstallationConfigurationTests` | `AllRequiredSettingsValid_HostStarts` | host starts | RED |
| AC-001 | only this Story's settings required | Integration | same | `OnlyTheSettingsOfThisStoryAreRequired_NoTimeZoneRetentionOrLanguage` | starts with 5 settings | RED |
| AC-001 | each required setting missing | Integration | same | `MissingRequiredSetting_HostDoesNotStart_NamingTheKey` (4) | start throws; key named | RED |
| AC-001 | invalid id / address / port / connection string | Integration | same | `InvalidSetting_HostDoesNotStart_NamingTheKeyNotTheValue` (20) | start throws; key named; value absent | RED |
| AC-001 | private port equal to a public port | Integration | same | `PrivatePortEqualToAPublicPort_HostDoesNotStart` | start throws | RED |
| AC-001 | boundary-valid values | Integration | same | `BoundaryValidSetting_HostStarts` (5) | host starts | RED |
| AC-001 | refusal logged without value | Integration | same | `InvalidSetting_IsLogged_WithTheKeyAndWithoutTheValue` | key in log, secret absent | RED |
| AC-001 | settings not stored in DB | Integration | same | `ValidSettings_AreNotStoredInTheDatabase` | no id/address in row | RED |
| AC-002 | first check right after start | Integration | `Web.BackgroundServices.LegitimacyCheckScheduleTests` | `FirstCheck_RunsRightAfterStart_WithoutAdvancingTime` | call at start time | RED |
| AC-002 | call arguments | Integration | same | `Call_CarriesConfiguredInstallationId_ContractVersion_AndReleaseVersion` | configured id, 1, semver | RED |
| AC-002 | 6 h after success (−1 s none) | Integration | same | `AfterSuccess_NextCheckIsSixHoursLater` | 2nd call at +6 h | RED |
| AC-002 | 15 min after each failure category | Integration | same | `AfterFailure_NextCheckIsFifteenMinutesLater` (5) | timer at +15 min | RED |
| AC-002 | 15 min after `upgrade_required`; 6 h after suspended | Integration | same | `AfterUpgradeRequiredAnswer_NextCheckIsFifteenMinutesLater`, `AfterSuspendedAnswer_NextCheckIsSixHoursLater` | as named | RED |
| AC-002 | retries until success, then 6 h | Integration | same | `FailuresRetryEveryFifteenMinutes_UntilASuccess_ThenSixHours` | calls at 0, 15, 30, 45 min; then +6 h | RED |
| AC-002 | interval from completion | Integration | same | `IntervalCountsFromCompletion_NotFromStart` | next at completion + 6 h | RED |
| AC-002 | one check at a time | Integration | same | `OnlyOneCheckRunsAtATime_EvenWhenACallHangs` | single call | RED |
| AC-002 | exception does not stop schedule / host | Integration | same | `ExceptionInACheck_DoesNotStopTheSchedule_CountsAsFailure`, `ExceptionInACheck_DoesNotStopTheHost` | +15 min; liveness 200 | RED |
| AC-002 | stop while a call hangs | Integration | same | `HostStop_WhileACallHangs_CompletesAndStartsNoNewCheck` | stop completes, 1 call | RED |
| AC-003 | known active → exactly 4 properties | Integration | `ControlPlane.Controllers.LegitimacyCheckEndpointTests` | `KnownActiveInstallation_Returns200_WithExactlyStatusCompatibilityDomainClientId` | 200 JSON | RED |
| AC-003 | suspended answered the same way | Integration | same | `SuspendedInstallation_IsAnsweredTheSameWay_WithStatusSuspended` | status suspended | RED |
| AC-003 | own installation answered | Integration | same | `AnswersTheCallersOwnInstallation_NotAnother` | other's values | RED |
| AC-003 | unknown request fields ignored; upper-case UUID | Integration | same | `UnknownExtraProperties_AreIgnored`, `InstallationIdInUpperCase_IsTheSameInstallation` | 200 | RED |
| AC-003 | no teaching-data type in contract | Architecture | `Architecture.ProjectReferenceTests` | `ContractsAssembly_HasOnlyWireTypes_NoTeachingDataName` | only wire types | GUARD |
| AC-003 | real client ↔ Control Plane | Contract | `Infrastructure.ControlPlane.LegitimacyChannelContractTests` | `KnownInstallation_IsAnAnswer_AndTheControlPlaneRecordsTheCall`, `UnsupportedContractVersion_IsAnAnswerWithUpgradeRequired` | Answer; record | RED |
| AC-004 | min + recommended, numeric compare | Integration | `ControlPlane.Controllers.LegitimacyCheckCompatibilityTests` | `WithMinimumAndRecommended_VersionsCompareNumerically` (8) | table of FR-005 | RED |
| AC-004 | unsupported contract version | Integration | same | `UnsupportedContractVersion_IsUpgradeRequired_EvenWithANewApplicationVersion` (2), `WithoutVersionSettings_UnsupportedContractVersion_IsUpgradeRequired` | upgrade_required | RED |
| AC-004 | unset settings impose nothing | Integration | same | `WithoutVersionSettings_ContractVersionOne_IsSupported` (3), `OnlyRecommendedSet_ImposesNoMinimum` (2), `OnlyMinimumSet_ImposesNoRecommendation` (2) | as named | RED |
| AC-004 | recommended below minimum | Integration | same | `RecommendedBelowMinimum_IsAccepted_AndNeverApplies` (2) | min rules | RED |
| AC-004 | suspended still computed | Integration | same | `SuspendedInstallation_StillGetsItsCompatibilityComputed` | upgrade_recommended | RED |
| AC-004 | invalid version setting | Integration | same | `InvalidVersionSetting_StopsTheControlPlaneAtStartup_NamingTheKeyNotTheValue` (4) | start throws | RED |
| AC-005 | first call record | Integration | `ControlPlane.Controllers.InstanceLicenseCheckRecordTests` | `FirstCall_CreatesOneRecord_WithAnswerTimeVersionsAndAnswer` | one row, values, stamps | RED |
| AC-005 | replace, created_at kept | Integration | same | `SecondCall_ReplacesTheRecord_CreatedAtKept` | one row, new values | RED |
| AC-005 | upgrade_required recorded; one per installation | Integration | same | `UpgradeRequiredAnswer_IsRecorded`, `EachInstallation_HasItsOwnRecord` | as named | RED |
| AC-005 | concurrent first calls | Integration | same | `ConcurrentFirstCalls_LeaveOneRecord_AllAnswered200` | 1 row, all 200 | RED |
| AC-005 | Information log; unknown Warning; no domain/body | Integration | same | `KnownCall_IsLoggedAtInformation_UnknownAtWarning_WithoutDomainClientIdOrBody` | events per §3 | RED |
| AC-005 | no audit row | Integration | `ControlPlane.Controllers.LegitimacyCheckEndpointTests` | `Checks_WriteNoAuditRow_EvenForUnknownAndInvalidCalls` | audit unchanged | GUARD |
| AC-005 | answer echoes nothing | Integration | `ControlPlane.Controllers.InstanceLicenseCheckRecordTests` | `Answer_DoesNotEchoVersionsOrIdentifier` | no version/id in body | GUARD |
| AC-005 | installation unchanged | Integration | `ControlPlane.Controllers.LegitimacyCheckEndpointTests` | `Checks_DoNotChangeTheInstallation` | row equal | GUARD |
| AC-005 | table, constraints | Persistence | `ControlPlane.Persistence.InstanceLicenseCheckSchemaTests` | `Columns_MatchDesign`, `SecondRowForOneInstallation_ViolatesUniqueConstraint`, `UnknownInstallation_ViolatesForeignKey`, `InstallationWithACheck_CannotBeDeleted`, `InvalidValue_ViolatesNamedCheckConstraint` (9), `BoundaryValues_AreAccepted` (2) | db-design §3 | RED |
| AC-005 | migration order | Persistence | `ControlPlane.Persistence.MigrationTests` (changed) | `Migrations_CreateOwnerAuditEventInstallationAllowedAdminAndInstanceLicenseCheck_InOrder` | 4 migrations | RED |
| AC-006 | unknown id | Integration | `ControlPlane.Controllers.LegitimacyCheckEndpointTests` | `UnknownInstallationId_Returns404_OutcomeOnly_RecordsNothing` | 404 outcome | RED |
| AC-006 | 23 invalid requests | Integration | same | `InvalidRequest_Returns400_InvalidRequestOnly_RecordsNothing` (23) | 400 outcome | RED |
| AC-006 | bounds accepted | Integration | same | `ApplicationVersion_AtTheBounds_IsAccepted` (3), `ContractVersion_AtTheBounds_IsAccepted` (2) | 200 | RED |
| AC-006 | unknown / before setup via real client | Contract | `Infrastructure.ControlPlane.LegitimacyChannelContractTests` | `UnknownInstallation_IsUnknownInstallation`, `ControlPlaneBeforeSetup_RedirectIsAnErrorAnswer` | categories | RED |
| AC-006 | before setup | Integration | `ControlPlane.Controllers.LegitimacyCheckEndpointTests` | `BeforeOwnerSetup_RedirectsToSetup_RecordsNothing` | 302 /setup | RED |
| AC-007 | success creates row | Integration | `Application.UseCases.CheckLegitimacyRecordingTests` | `SuccessfulCheck_WithNoRow_CreatesTheRowWithTheAnswer` | row values | RED |
| AC-007 | suspended / upgrade_recommended success | Integration | same | `SuspendedOrUpgradeRecommendedAnswer_IsASuccessfulCheck` (3) | last success set | RED |
| AC-007 | update in place | Integration | same | `SuccessfulCheck_WithAnExistingRow_UpdatesItInPlace` | same id, new values | RED |
| AC-007 | upgrade_recommended Warning | Integration | `Web.Logging.LegitimacyLoggingTests` | `UpgradeRecommended_IsLoggedAtWarning_AndIsNotAFailure` | Warning event | RED |
| AC-007 | client maps valid answers | Unit | `Infrastructure.ControlPlane.ControlPlaneClientTests` | `PostsContractRequest_ToTheCheckPath_AsJson`, `ValidAnswer_IsReturnedAsAnswer` (3), `AnswerWithUnknownExtraProperties_IsStillAnAnswer` | request shape; Answer | RED |
| AC-008 | upgrade_required keeps last success | Integration | `Application.UseCases.CheckLegitimacyRecordingTests` | `UpgradeRequired_WithNoRow_RecordsAnswerWithoutLastSuccess`, `UpgradeRequired_WithAnExistingRow_KeepsLastSuccess_RecordsTheRest` | as named | RED |
| AC-008 | each category changes nothing | Integration | same | `UnsuccessfulCheck_WithAnExistingRow_ChangesNothing` (5), `UnsuccessfulCheck_WithNoRow_CreatesNoRow` (2) | row equal / none | RED |
| AC-008 | Error log with category | Integration | `Web.Logging.LegitimacyLoggingTests` | `EachFailureCategory_IsLoggedAtError_WithItsName` (5), `UpgradeRequired_IsLoggedAtError_WithCategoryUpgradeRequired` | Error, Category | RED |
| AC-008 | client classification | Unit | `Infrastructure.ControlPlane.ControlPlaneClientTests` | `Status200_WithInvalidBody_IsUnparseableAnswer` (18), `Status404_WithUnknownInstallationOutcome_IsUnknownInstallation`, `OtherStatus_IsErrorAnswer` (14), `ConnectionFailure_IsUnreachable`, `TlsFailure_IsUnreachable`, `NoAnswerWithinThirtySeconds_IsTimeout`, `AnswerJustBeforeThirtySeconds_IsNotATimeout`, `CallerCancellation_IsNotSwallowedAsAFailure` | categories | RED |
| AC-009 | never confirmed | Integration | `Application.UseCases.LegitimacyModeQueryTests` | `NoRow_IsReadOnly_NotYetConfirmed`, `RowWithoutLastSuccess_IsReadOnly_NotYetConfirmed` | NotYetConfirmed | RED |
| AC-009 | active and recent | Integration | same | `ActiveAndRecent_IsNotReadOnly` (3), `CompatibilityAlone_DoesNotMakeItReadOnly_WhileWithinGrace` (2) | not read-only | RED |
| AC-009 | 7-day boundary | Integration | same | `ExactlySevenDays_IsNotYetReadOnly`, `SevenDaysAndOneSecond_IsReadOnly_GracePeriodExpired_WithLastSuccess`, `GracePeriod_ExpiresAsTheClockMoves_WithoutAnyCheck` | strict > 7 d | RED |
| AC-009 | suspended; suspended and expired | Integration | same | `Suspended_IsReadOnly_SuspendedByOwner`, `SuspendedAndExpired_ReasonIsSuspendedByOwner` | SuspendedByOwner | RED |
| AC-009 | first success ends read-only | Integration | same | `FirstSuccess_EndsNotYetConfirmed` | not read-only | RED |
| AC-010 | enter once, leave once, failures each, changes only | Integration | `Web.Logging.LegitimacyLoggingTests` | `Sequence_LogsModeChangesOnce_FailuresEachTime_ResultChangesOnly` | event counts | RED |
| AC-010 | enter on suspension; at start from expired; none when fine | Integration | same | `SuspendedAnswer_EntersReadOnly_WithReasonSuspendedByOwner`, `StartingInReadOnly_FromStoredExpiredState_IsLoggedAsEntering`, `StartingActiveAndRecent_LogsNoReadOnlyEvent` | as named | RED |
| AC-011 | restart: 2 days works, 8 days read-only | Integration | `Application.UseCases.LegitimacyModeQueryTests` | `AfterRestart_WithControlPlaneUnreachable_TwoDaysAgoWorks_EightDaysAgoIsReadOnly` | mode per stored row | RED |
| AC-011 | restart reads and updates the row; one row | Integration | `Application.UseCases.CheckLegitimacyRecordingTests` | `Restart_ReadsTheStoredRow_AndTheNextSuccessUpdatesIt`, `ManyChecks_KeepExactlyOneRow` | one row | RED |
| AC-011 | schema, single row, migration | Persistence | `Infrastructure.Persistence.InstallationDatabaseTests` | all 9 methods | db-design §4, §6.2 | RED |
| AC-012 | not called yet | Integration | `ControlPlane.Controllers.InstallationLastCheckTests` | `NeverCalled_ShowsNotCalledYet_AndNoValueElements` | none element | RED |
| AC-012 | values after a check; stored labels | Integration | same | `AfterARealCheck_ShowsTimeVersionsStatusAndCompatibility`, `StoredAnswer_IsShownAsTranslatedLabels` (3), `AnsweredStatus_IsTheRecordedOne_NotTheCurrentStatus`, `OtherInstallationsCheck_IsNotShown` | elements per api-design §9 | RED |
| AC-012 | Ukrainian default | Localization | same | `Section_IsUkrainianByDefault_EvenWhenTheBrowserAsksForEnglish` | uk labels | RED |
| AC-012 | Owner-only | Security | same | `DetailPage_StaysOwnerOnly` | 302 /sign-in | GUARD |
| AC-012 | keys in uk and en | Localization | `ControlPlane.Localization.LastCheckTranslationTests` | `ContractKey_ExistsInUkrainianAndEnglish_AndDiffers` (10) | both exist, differ | RED |
| AC-013 | no session, no token processed; session ignored | Security | `ControlPlane.Controllers.LegitimacyCheckEndpointTests` | `WithoutSessionAndWithoutAntiforgeryToken_IsProcessed`, `WithOwnerSessionCookie_IsProcessedTheSameWay_NoChallenge` | 200 | RED |
| AC-013 | other methods | Security | same | `OtherMethods_AreNotHandled_RecordNothing` (4) | 404/405 | RED |
| AC-013 | only anonymous POST / only exemption | Security | `ControlPlane.Security.AnonymousEndpointTests` (changed), `ControlPlane.Security.AntiforgeryTests` (changed) | `OnlySc4EndpointsAllowAnonymous`, `OnlyTheLegitimacyCheckIsExemptFromAntiforgery`; `EveryNonGetEndpoint_WithoutToken_Returns400` excludes it | SC-4 lists | RED |
| AC-014 | liveness | Integration | `Web.Security.HealthEndpointTests` | `Liveness_OnPrivatePort_IsHealthy`, `Liveness_IsHealthy_EvenWithoutTheDatabase` | 200 Healthy | RED |
| AC-014 | readiness states | Integration | same | `Readiness_DatabaseUnreachable_IsUnhealthy503`, `Readiness_NeverConfirmed_IsDegraded200`, `Readiness_AfterASuccessfulCheck_ActiveAndRecent_IsHealthy`, `Readiness_Suspended_IsDegraded`, `Readiness_GracePeriodExpired_IsDegraded`, `Readiness_LastCheckFailed_WithinGrace_IsDegraded_UntilASuccess`, `Readiness_BeforeTheFirstCheckCompletes_UsesStoredStateOnly`, `Readiness_BodyIsTheStateWordOnly_NoReasonTimeOrVersion` | per FR-013 | RED |
| AC-014 | public port and forged headers | Security | same | `HealthPaths_OnPublicPort_Return404EmptyBody` (2), `HealthPaths_OnPublicPort_WithForgedHeaders_Return404` (4), `HealthPaths_OtherMethods_AreNotHandled` (3), `PrivatePort_HasNoHstsOrHttpsRedirect` | 404 / no HSTS | RED |
| AC-014 | only health endpoints; public port empty | Security | `Web.Security.InstallationEndpointTests` | `RoutedEndpoints_AreOnlyLivenessAndReadiness_GetAndAnonymous`, `PublicPort_AnyRequest_Returns404EmptyBody_NoCookieNoRedirect` (8), `PrivatePort_UnknownPath_Returns404` | as named | RED |
| AC-015 | installation logs identifiers only | Integration | `Web.Logging.LegitimacyLoggingTests` | `Logs_CarryNoDomainClientIdOrExceptionTextFromTheCall` | markers absent | RED |
| AC-015 | Control Plane logs identifiers only | Integration | `ControlPlane.Controllers.InstanceLicenseCheckRecordTests` | `KnownCall_IsLoggedAtInformation_UnknownAtWarning_WithoutDomainClientIdOrBody` | markers absent | RED |
| FR-014 | project references and framework-free layers | Architecture | `Architecture.ProjectReferenceTests` | `ProjectReferences_AreExactlyThoseAllowed` (6: Control Plane → `Contracts` RED, 5 GUARD), `FrameworkFreeProjects_ReferenceNoPackage` (3), `ApplicationAssembly_ReferencesNoInfrastructureContractsOrEfCore`, `WebAssembly_ExposesNoDbContextSubclass` | package-map | RED / GUARD |
| seam | manual clock and fake client | Unit | `TestInfrastructure.ManualTimeProviderTests` | 5 methods | timers fire on advance | GUARD |

Every Acceptance Criterion AC-001 … AC-015 has at least one RED test that turns
green only with the implementation.
