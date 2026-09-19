---
artifact_type: ac_test_matrix
story: US-006
version: 2
status: DRAFT
created_at: 2026-09-19T09:40:00Z
updated_at: 2026-09-19T09:45:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-006-control-plane-push.md
    version: null
  - path: docs/specifications/US-006-spec.md
    version: 2
  - path: docs/designs/api/US-006-api-design.md
    version: 1
  - path: docs/designs/api/US-006-openapi.yaml
    version: 1
  - path: docs/designs/database/US-006-db-design.md
    version: 1
  - path: docs/designs/database/US-006-entity-model.md
    version: 1
supersedes: docs/tests/US-006-ac-test-matrix.md@v1
---

# US-006 Acceptance Criteria → Test Matrix

Status column: **RED** = expected to fail until the implementation exists,
**GUARD** = an existing case that must stay green and now also covers this Story.
Namespaces are relative to `ClassroomAgent.Tests`. Strategy:
`docs/tests/US-006-test-strategy.md`.

The red phase was verified on a running Docker daemon
(`docs/evidence/US-006-test-generation-report.md` v2 §4) and the implementation
turned every row green
(`docs/evidence/US-006-implementation-report.md` §5). The Status column keeps the
pre-implementation expectation each row was written with.

**v2** corrects four rows whose expectation contradicted an approved artifact, on
the human's decision of 2026-09-19: three AC-011 rows counted the one-minute
window from the startup check, while `trebovaniya.md` v77 §9, Story AC-011 and
specification FR-010 / I-11 count it from the start of the previous
**push-triggered** check; and the AC-015 row drove the page language with
`Accept-Language`, which the Control Plane ignores by design (US-001 FR-019).

| AC | Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | each valid `Hosting:PrivateAddress` form starts the host | Integration | `Web.Configuration.PrivateAddressConfigurationTests` | `ValidAddress_HostStarts` (7) | host starts | RED |
| AC-001 | the setting is missing | Integration | same | `MissingAddress_HostDoesNotStart_NamingTheKey` | start throws, key named | RED |
| AC-001 | empty, host name, CIDR, `**`, `+`, with port, out of range | Integration | same | `InvalidAddress_HostDoesNotStart_NamingTheKeyWithoutTheValue` (10) | start throws, key named, value absent | RED |
| AC-001 | refusal is logged without the value | Integration | same | `InvalidAddress_IsNotWrittenToTheLog` | key in log, value absent | RED |
| AC-001 | the setting is required — the count of required settings grows by one | Integration | `Web.Configuration.InstallationConfigurationTests` | `OnlyTheSettingsOfThisStoryAreRequired_…` | 6 settings | RED |
| AC-002 | registration without / with an address | Integration | `ControlPlane.Controllers.InstallationPushAddressTests` | `Registration_WithoutAPushAddress_StoresNone`, `Registration_WithAPushAddress_StoresIt` | NULL / canonical value | RED |
| AC-002 | the registration form offers the field | Integration | same | `RegistrationForm_OffersThePushAddressField` | `pushAddress` present | RED |
| AC-002 | the push address page shows the stored value / is empty | Integration | same | `PushAddressForm_ShowsTheStoredValue`, `PushAddressForm_WithoutAStoredValue_IsEmpty` | pre-filled / empty | RED |
| AC-002 | unknown installation, GET and POST, valid and invalid value | Integration | same | `PushAddressForm_ForAnUnknownInstallation_IsNotFound`, `ChangingTheAddressOfAnUnknownInstallation_IsNotFound`, `UnknownInstallation_IsNotFound_EvenWithAnInvalidAddress` | `404` | RED |
| AC-002 | set, change, clear | Integration | same | `SettingTheAddress_StoresItAndRedirectsToTheDetailPage`, `ChangingTheAddress_ReplacesIt_AndLeavesEveryOtherColumn`, `ClearingTheAddress_StoresNone` | stored / replaced / NULL, `302` to the detail page | RED |
| AC-002 | canonical storage (trim, trailing `/`, case, IPv6) | Integration | same | `AddressIsStoredCanonically` (4) | canonical value | RED |
| AC-002 | unchanged submission, and empty while none stored | Integration | same | `SubmittingTheSameCanonicalValue_ChangesNothing`, `SubmittingEmptyWhileNoneIsStored_ChangesNothing` | `302`, `updated_at` unchanged | RED |
| AC-002 | the address is not unique | Integration | same | `TwoInstallations_MayShareOneAddress` | both stored | RED |
| AC-002 | allowed at any status | Integration | same | `SuspendedInstallation_AcceptsAnAddressChange` | stored, status still `suspended` | RED |
| AC-002 | every VR-001 rule on the page and at registration | Integration | `ControlPlane.Controllers.InstallationPushAddressValidationTests` | `InvalidAddress_OnThePushAddressPage_IsRefusedWithItsMessage` (16), `InvalidAddress_AtRegistration_IsRefusedAndRegistersNothing` (16) | `400`, the rule's message key, nothing stored | RED |
| AC-002 | valid boundary values accepted | Integration | same | `ValidAddress_IsAccepted` (7) | `302`, stored | RED |
| AC-002 | the refused form refills the typed value | Integration | same | `RefusedForm_RefillsTheTypedValue` | value refilled | RED |
| AC-002 | a field error joins the other registration errors | Integration | same | `InvalidAddressAtRegistration_IsReportedTogetherWithOtherFieldErrors` | both messages shown | RED |
| AC-003 | one row per set / change / clear, with the Owner as actor | Integration | `ControlPlane.Controllers.InstallationPushAddressAuditTests` | `SettingTheAddress_WritesOneRow`, `ChangingAndClearing_EachWriteOneRow_WithTheSameAction` | `installation_push_address_changed`, actor `owner`, target `installation`, `succeeded` | RED |
| AC-003 | the row carries no address | Integration | same | `RowsCarryNoAddress` | no address in any row | RED |
| AC-003 | registration with an address writes only its own row | Integration | same | `RegistrationWithAnAddress_WritesOnlyTheRegistrationRow` | only `installation_created` | RED |
| AC-003 | unchanged and refused submissions write nothing | Integration | same | `UnchangedSubmission_WritesNoRow`, `RefusedSubmission_WritesNoRow` | no row | RED |
| AC-003 | change and row are written together | Integration | same | `ChangeAndItsRow_AreWrittenTogether` | both or neither | RED |
| AC-003 | audit rows stay non-updatable (SC-11) | Integration | same | `AuditRow_IsNotUpdatable` | `PostgresException` | RED |
| AC-004 | address set → shown, no warning | Integration | `ControlPlane.Controllers.InstallationPushAddressDetailTests` | `WithAnAddress_ThePageShowsIt_WithoutTheWarning` | `installation-push-address`, no warning element | RED |
| AC-004 | not set → "not set" + warning | Integration | same | `WithoutAnAddress_ThePageSaysSo_AndWarns` | `…-none` and `…-warning` with their translations | RED |
| AC-004 | both states link to the push address page | Integration | same | `BothStates_LinkToThePushAddressPage` (2) | link present | RED |
| AC-004 | no push result is shown | Integration | same | `ThePageShowsNothingAboutPushResults` | no such element | RED |
| AC-004 | the address is shown exactly as stored | Integration | same | `TheStoredAddressIsShownExactlyAsStored` | canonical value rendered | RED |
| AC-004 | the page keeps what earlier Stories show | Integration | same | `ThePageKeepsEverythingEarlierStoriesShow` | identifier, domain, client ID, last check | RED |
| AC-005 | suspend sends one push with the installation id only | Integration | `ControlPlane.Push.StatusPushDeliveryTests` | `Suspending_PostsThePushToTheStoredAddress` | `POST` to `{address}/service/v1/status-pushes`, one JSON property, no cookies | RED |
| AC-005 | resume sends a push | Integration | same | `Resuming_PostsThePush` | one attempt | RED |
| AC-005 | an action that changed nothing sends none | Integration | same | `ActionThatChangesNothing_SendsNoPush` | no attempt | RED |
| AC-005 | no address → no push, status still changes | Integration | same | `WithoutAPushAddress_NothingIsSent` | `302`, status `suspended`, no attempt | RED |
| AC-005 | rename, client ID, push address change | Integration | same | `ChangesOtherThanTheStatus_SendNoPush` | no attempt | RED |
| AC-005 | `AllowedAdmin` add and revoke | Integration | same | `AddingAndRevokingAnAllowedAdmin_SendNoPush` | no attempt | RED |
| AC-005 | the Owner does not wait for the push | Integration | same | `TheOwnerIsNotKeptWaitingForThePush` | `302` while the attempt hangs | RED |
| AC-005 | no address is logged at `Warning` with the id | Integration | `ControlPlane.Push.StatusPushLoggingTests` | `StatusChangeWithoutAnAddress_IsLoggedAtWarning` | `StatusPushNoAddress`, `InstallationId` | RED |
| AC-006 | `202` → delivered, one attempt | Integration | `ControlPlane.Push.StatusPushDeliveryTests` | `Accepted_IsDelivered_WithoutARetry` | one attempt after 4 min of clock | RED |
| AC-006 | `404` → not retried | Integration | same | `NotFound_IsNotRetried` | one attempt | RED |
| AC-006 | `500`, `400`, `200`, `302` → 4 attempts at +5 s, +30 s, +2 min | Integration | same | `UnexpectedStatus_IsRetriedThreeTimes_ThenAbandoned` (4) | attempt times exactly `t`, `t+5 s`, `t+35 s`, `t+155 s` | RED |
| AC-006 | connection failure retried | Integration | same | `ConnectionFailure_IsRetried` | second attempt after 5 s | RED |
| AC-006 | no answer within 10 s is a timeout | Integration | same | `NoAnswerWithinTenSeconds_IsATimeout_AndIsRetried` | second attempt after timeout + 5 s | RED |
| AC-006 | delivered / refused / failed / abandoned log lines | Integration | `ControlPlane.Push.StatusPushLoggingTests` | `DeliveredPush_IsLoggedAtInformation_WithTheInstallationId`, `RefusedPush_IsLoggedAtWarning_AndNotRetried`, `EveryFailedAttempt_IsLoggedWithItsNumberAndCategory_ThenTheRetriesAreAbandoned`, `ConnectionFailure_IsLoggedWithItsCategory_AndNoStatusCode` | event name, level, attempt number, category, status code | RED |
| AC-006 | no log line carries the address, domain, client ID or body | Integration | same + `StatusPushDeliveryTests.TheAddressIsNeverLogged` | `NoLogLineCarriesTheAddress_TheDomainOrTheResponseBody` | absent from every log file | RED |
| AC-006 | a failed push writes nothing and is not audited | Integration | `ControlPlane.Push.StatusPushDeliveryTests` | `DeliveryIsNotAudited_AndChangesNothingStored` | only the US-004 audit row; address unchanged | RED |
| AC-007 | a newer push replaces an unfinished one | Integration | same | `NewerPush_ReplacesTheUnfinishedOne` | old attempts stop; new schedule from attempt 1 | RED |
| AC-007 | different schools are independent | Integration | same | `PushesToDifferentSchools_AreIndependent` | one attempt to each address | RED |
| AC-008 | retries do not outlive the host | Integration | same | `PendingRetries_DoNotOutliveTheHost` | no further attempt after stop | RED |
| AC-009 | own id → `202` and one check | Integration | `Web.Security.StatusPushEndpointTests` | `PushForThisInstallation_IsAccepted_AndStartsACheck` | `202`, a second Control Plane call | RED |
| AC-009 | `202` without waiting for the check | Integration | same | `TheReceiverAnswersWithoutWaitingForTheCheck` | `202`, empty body while the check hangs | RED |
| AC-009 | the push itself writes nothing | Integration | same | `ThePushItselfWritesNothing` | `LegitimacyState` unchanged until the check answers | RED |
| AC-009 | unknown JSON properties ignored (DC-12) | Integration | same | `UnknownPropertiesInThePush_AreIgnored` | `202`, check starts | RED |
| AC-009 | schedule after a pushed check: 6 h / 15 min from completion | Integration | `Web.BackgroundServices.PushCheckCoordinationTests` | `AfterAPushedSuccessfulCheck_TheNextScheduledCheckIsSixHoursLater`, `AfterAPushedUnsuccessfulCheck_TheNextScheduledCheckIsFifteenMinutesLater` | next call at the computed instant | RED |
| AC-009 | the accepted push is logged at `Information` | Integration | `Web.Logging.StatusPushLoggingTests` | `AcceptedPush_IsLoggedAtInformation` | `StatusPushAccepted` | RED |
| AC-010 | another installation's id → `404`, no check | Integration | `Web.Security.StatusPushEndpointTests` | `PushForAnotherInstallation_IsNotFound_AndStartsNoCheck` | `404`, one call only | RED |
| AC-010 | logged at `Warning` without the received id | Integration | `Web.Logging.StatusPushLoggingTests` | `PushForAnotherInstallation_IsLoggedAtWarning_WithoutTheReceivedId` | `StatusPushForeignInstallation`, id absent | RED |
| AC-011 | a push inside the minute of the previous push check defers and leaves one pending check | Integration | `Web.BackgroundServices.PushCheckCoordinationTests` | `PushWithinAMinuteOfThePreviousPushCheck_LeavesOnePendingCheck` | `202`, no check now, one check 60 s after the previous push check started | RED |
| AC-011 | many pushes leave exactly one pending check | Integration | same | `ManyPushesWithinAMinute_LeaveOnlyOnePendingCheck` | exactly three calls in total: startup, the accepted push, the one pending check | RED |
| AC-011 | a push after a minute starts a check at once | Integration | same | `PushAfterAMinute_StartsACheckAtOnce` | call at the current instant | RED |
| AC-011 | a push while a check runs starts no second check | Integration | same | `PushWhileACheckIsRunning_StartsNoSecondCheck_ButLeavesAPendingOne`, `PushedChecksNeverRunConcurrently` | second call only after the first completes | RED |
| AC-011 | a completing scheduled check does not clear the flag | Integration | same | `PendingCheckAfterARunningScheduledCheck_StillRuns` | pending check runs | RED |
| AC-011 | the pending check starts its own minute | Integration | same | `ThePendingCheckStartsItsOwnMinute` | third check only after a further minute | RED |
| AC-011 | deferred pushes are not logged; the pending check start is | Integration | `Web.Logging.StatusPushLoggingTests` | `DeferredPush_IsNotLogged_AndThePendingCheckIsLoggedWhenItStarts` | one `StatusPushAccepted` (the push that started a check), none for the two deferred ones; one `PendingPushCheckStarted` | RED |
| AC-012 | missing, empty, non-JSON, malformed, non-UUID, wrong content type | Integration | `Web.Security.StatusPushEndpointTests` | `MalformedPush_IsRefused_AndStartsNoCheck` (7) | `400`, no check | RED |
| AC-012 | body over 4 KB | Integration | same | `OversizedPush_IsRefused_AndStartsNoCheck` | `413`, no check | RED |
| AC-012 | logged at `Error` with the category only | Integration | `Web.Logging.StatusPushLoggingTests` | `MalformedPush_IsLoggedAtError_WithItsCategoryOnly` (2), `OversizedPush_IsLoggedAtError_WithTheTooLargeCategory` | `StatusPushRejected`, `invalid` / `too_large`, body absent | RED |
| AC-013 | the path answers only on the private port | Integration | `Web.Security.StatusPushEndpointTests` | `OnThePublicPort_ThePathIsNotFound_AndStartsNoCheck`, `OnThePublicPort_AForgedHostDoesNotReachTheReceiver` | `404` | RED |
| AC-013 | POST only | Integration | same | `OtherMethods_DoNotReachTheReceiver` (3) | not `202`, no check | RED |
| AC-013 | routed endpoints are liveness, readiness and the receiver, all anonymous | Integration | `Web.Security.InstallationEndpointTests` | `RoutedEndpoints_AreLivenessReadinessAndThePushReceiver_AllAnonymous` | exactly three patterns | RED |
| AC-013 | the receiver is the only unsafe endpoint, POST, antiforgery-exempt | Integration | same | `ThePushReceiver_IsTheOnlyUnsafeEndpoint_PostOnly_AndExemptFromAntiforgery` | single endpoint with the exemption metadata | RED |
| AC-013 | the public port still answers `404` to the receiver path | Integration | same | `PublicPort_AnyRequest_Returns404EmptyBody_NoCookieNoRedirect` (+1 row) | `404`, empty body | RED |
| AC-013 | the push address pages are Owner-only and antiforgery-protected | Integration | `ControlPlane.Security.PushAddressSecurityTests` | all 7 methods | `302 /sign-in`, `403`, `400`, `302 /setup`; nothing changes | RED |
| AC-013 | the new Control Plane routes join the route enumeration | Integration | `ControlPlane.Security.InstallationAuthorizationTests` | `InstallationEndpoints_ExistAndNoneAllowsAnonymous` + theories | route present, not anonymous, no `PUT`/`PATCH`/`DELETE` | RED |
| AC-013 | no other endpoint becomes anonymous or exempt (Control Plane) | Integration | `ControlPlane.Security.AnonymousEndpointTests`, `AntiforgeryTests` | existing methods | unchanged SC-4 list | GUARD |
| AC-014 | a push in read-only mode is accepted and its check writes the state | Integration | `Web.Security.StatusPushEndpointTests` | `InReadOnlyMode_ThePushIsAccepted_AndTheCheckWritesTheState` | `202`; `LegitimacyState` becomes `active` | RED |
| AC-015 | every new key exists in Ukrainian and English | Integration | `ControlPlane.Localization.PushAddressTranslationTests` | `Key_ExistsInUkrainianAndEnglish_AndDiffers` (13) | both present and different | RED |
| AC-015 | the detail page in English; the address is not translated | Integration | same | `DetailPageInEnglish_UsesTheEnglishWarning_AndShowsTheAddressAsStored` | with the Owner account set to English (US-001 FR-019): English warning, no Ukrainian one; address as stored | RED |
| AC-015 | the push address page is translated (Ukrainian default) | Integration | same | `PushAddressPage_IsTranslated` | title and note from the catalogue | RED |
| AC-015 | translation catalogues stay complete | Integration | `ControlPlane.Localization.TranslationCompletenessTests` | existing methods | no missing key in either language | GUARD |
| PC-2, db-design §3 | the column, its constraint, no unique index, triggers | Integration | `ControlPlane.Persistence.InstallationPushAddressSchemaTests` | 5 methods (16 cases) | column `character varying(255)` nullable; constraint accepts canonical, rejects the rest | RED |
| PC-2, db-design §5 | the fifth Control Plane migration | Integration | `ControlPlane.Persistence.MigrationTests` | existing methods | five migrations, applied in order | RED |
| SC-10 | a refused address never reaches the log | Integration | `ControlPlane.Controllers.InstallationPushAddressValidationTests` | `RefusedSubmission_IsNotWrittenToTheLog` | host absent from every log file | RED |
| S-14 | a refused value is echoed HTML-encoded | Integration | same | `RefusedForm_EncodesTheTypedValue` | no raw markup in the body | RED |

Every Acceptance Criterion AC-001 … AC-015 has at least one mapped scenario. No
mandatory Acceptance Criterion is unmapped.
