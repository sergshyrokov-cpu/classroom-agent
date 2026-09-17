---
artifact_type: ac_test_matrix
story: US-004
version: 1
status: DRAFT
created_at: 2026-09-17T11:07:03Z
updated_at: 2026-09-17T11:07:03Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-004-suspend-resume-installation.md
    version: null
  - path: docs/specifications/US-004-spec.md
    version: 1
  - path: docs/designs/api/US-004-openapi.yaml
    version: 1
  - path: docs/designs/database/US-004-db-design.md
    version: 1
supersedes: null
---

# US-004 Acceptance Criteria → Test Matrix

Namespaces: `ClassroomAgent.Tests.ControlPlane.{Controllers,Security,Localization}`.
Status is the red-phase result of 2026-09-17 (see
`docs/evidence/US-004-test-generation-report.md`): **red** = fails for missing
behaviour, **guard** = passes before implementation by design.

| AC | Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | active offers suspend only | integration | InstallationStatusChangeTests | Detail_ActiveInstallation_OffersSuspendOnly | `installation-suspend` link to `/suspension`, no resume, no notice | red |
| AC-001 | suspended offers resume only | integration | InstallationStatusChangeTests | Detail_SuspendedInstallation_OffersResumeOnly | `installation-resume` link to `/resumption`, no suspend | red |
| AC-002 | suspend confirmation content, no change | integration | InstallationStatusChangeTests | SuspendConfirmation_ShowsNameDomainExplanationFormAndCancel_ChangesNothing | `200`; name, domain, explanation, POST form + token, cancel link; row and audit unchanged | red |
| AC-002, AC-004 | cancel is a link, status unchanged | integration | InstallationStatusChangeTests | Cancel_IsALinkToTheDetailPage_StatusUnchanged | status still active, suspend still offered | red |
| AC-003 | suspend changes only status | integration | InstallationStatusChangeTests | Suspend_ChangesOnlyStatus_RedirectsToDetail_WhichOffersResume | `302 /installations/{id}`; row = before with status suspended and updated_at = clock; admins unchanged; list shows suspended | red |
| AC-003 | over-posting ignored | integration | InstallationStatusChangeTests | PostedFields_AreIgnored_OnlyStatusChanges | only status and updated_at change | red |
| AC-003 | other installation untouched | integration | InstallationStatusChangeTests | OtherInstallation_IsNotAffected | other row unchanged | red |
| AC-004 | resume confirmation content, no change | integration | InstallationStatusChangeTests | ResumeConfirmation_ShowsNameDomainExplanationFormAndCancel_ChangesNothing | `200`; as AC-002 with Resume keys | red |
| AC-005 | resume changes only status | integration | InstallationStatusChangeTests | Resume_ChangesOnlyStatus_RedirectsToDetail_WhichOffersSuspend | `302`; status active; rest unchanged | red |
| AC-005 | repeatable | integration | InstallationStatusChangeTests | SuspendAndResume_CanRepeat_EachChangeAudited | six `302`, six alternating audit rows | red |
| AC-006 | suspend when suspended | integration | InstallationStatusUnchangedTests | Suspend_AlreadySuspended_RedirectsWithNotice_WritesNothing | `302 …?notice=already-suspended`; nothing written | red |
| AC-006 | resume when active | integration | InstallationStatusUnchangedTests | Resume_AlreadyActive_RedirectsWithNotice_WritesNothing | `302 …?notice=already-active`; nothing written | red |
| AC-006 | same confirmation twice | integration | InstallationStatusUnchangedTests | SameConfirmationSubmittedTwice_SecondIsUnchanged_OneAuditRow | first `302` detail, second `302` notice; one row | red |
| AC-006 | stale tab | integration | InstallationStatusUnchangedTests | StaleTab_SuspendedElsewhere_ConfirmingIsUnchanged | notice redirect; no row | red |
| AC-006 | stale resume never suspends | integration | InstallationStatusUnchangedTests | StaleResumeAfterResumeElsewhere_DoesNotSuspend | notice redirect; still active | red |
| AC-006 | confirmation page for target status | integration | InstallationStatusUnchangedTests | ConfirmationPage_ForTargetStatus_RedirectsWithNotice (×2) | `302` with notice; nothing written | red |
| AC-006 | matching notice shown | integration | InstallationStatusUnchangedTests | Detail_NoticeMatchingCurrentStatus_IsShown (×2) | `installation-status-notice` with key text | red |
| AC-006 | mismatching / unknown notice hidden | integration | InstallationStatusUnchangedTests | Detail_NoticeNotMatchingOrUnknown_IsNotShown_NotAnError (×5) | `200`, no notice, value not echoed | red |
| AC-006 | repeated notice parameter hidden | integration | InstallationStatusUnchangedTests | Detail_RepeatedNoticeParameter_IsNotShown | `200`, no notice | guard |
| AC-006 | concurrent identical suspends | integration | InstallationStatusUnchangedTests | ConcurrentSuspends_ExactlyOneChangesAndIsAudited_OthersUnchanged | one detail redirect, four notice redirects, one row, never `500` | red |
| AC-006 | concurrent suspend and resume | integration | InstallationStatusUnchangedTests | ConcurrentSuspendAndResume_AuditRowsMatchTheChangesMade | rows = changes, consistent final status, both `302` | red |
| AC-007 | unknown GUID | integration | InstallationStatusChangeTests | UnknownInstallation_AllFourOperations_Return404_ChangeNothing | `404` error page ×4; nothing written | guard |
| AC-007 | non-UUID identifier | integration | InstallationStatusChangeTests | NonUuidIdentifier_Returns404ErrorPage (×5) | `404` error page | guard |
| AC-008 | suspend row | integration | InstallationStatusAuditTests | Suspend_WritesInstallationSuspendedRow | owner actor, `installation_suspended`, target installation + internal id, succeeded, request id, time | red |
| AC-008 | resume row | integration | InstallationStatusAuditTests | Resume_WritesInstallationResumedRow | as above with `installation_resumed` | red |
| AC-008 | no row for GET, refusals, unchanged | integration | InstallationStatusAuditTests | GetCancelRefusedAndUnchangedRequests_WriteNoAuditRow | statuses 200,200,200,302,302,404,404,400,400; no row | red |
| AC-008 | no name/domain/UUID in rows | security | InstallationStatusAuditTests | AuditRows_CarryNoNameDomainOrIdentifier | rows exist and contain none of the values | red |
| AC-008 | rows immutable | security | InstallationStatusAuditTests | StatusChangeAuditRow_CannotBeUpdatedOrDeleted | UPDATE/DELETE raise | red |
| AC-008 (FR-012) | no name/domain in logs | security | InstallationStatusAuditTests | InstallationNameAndDomain_NeverReachTheLogFile | log files contain neither | guard |
| AC-009 | anonymous → sign-in | security | InstallationStatusAuthorizationTests | Anonymous_WithOwnerAccount_RedirectsToSignIn_ChangesNothing (×4) | `302 /sign-in`; nothing written | red |
| AC-009 | before setup → setup | security | InstallationStatusAuthorizationTests | BeforeSetup_RedirectsToSetup (×4) | `302 /setup` | red |
| AC-009 | Owner allowed | security | InstallationStatusAuthorizationTests | SignedInOwner_ConfirmationReturns200 (×2) | `200` | red |
| AC-009 | forbidden role | security | InstallationStatusAuthorizationTests | PrincipalWithoutOwnerRole_Returns403_ChangesNothing (×4) | `403` error page; nothing written | red |
| AC-009 | routes exist, none anonymous, GET/POST only | security | InstallationStatusAuthorizationTests | StatusEndpoints_ExistAndNoneAllowsAnonymous | both patterns, GET and POST | red |
| AC-009 | US-002 route list unchanged | security | InstallationAuthorizationTests | InstallationEndpoints_ExistAndNoneAllowsAnonymous (modified) | new routes excluded, US-002 list stays | guard |
| AC-010 | POST without token | security | InstallationStatusAntiforgeryTests | Post_WithoutToken_Returns400PageExpired_StatusUnchanged (×2) | `400` page expired; nothing written | red |
| AC-010 | token of another session | security | InstallationStatusAntiforgeryTests | Suspend_WithTokenFromAnotherSession_Returns400_StatusUnchanged | `400`; status active | red |
| AC-010 | GET with query values | security | InstallationStatusAntiforgeryTests | GetRequests_WithQueryValues_ChangeNothing | `200` ×3; nothing written | red |
| AC-010 | other methods | security | InstallationStatusAntiforgeryTests | OtherMethods_DoNotChangeStatus (×3) | ≥400; nothing written | guard |
| AC-011 | contract keys in uk and en | localization | InstallationStatusTranslationTests | ContractKey_ExistsInUkrainianAndEnglish_AndDiffers (×10) | key present in both, texts differ | red |
| AC-011 | Ukrainian default, values untranslated | localization | InstallationStatusTranslationTests | StatusPages_AreUkrainianByDefault_NameAndDomainUntranslated | `lang="uk"` with `Accept-Language: en`; uk texts; name and domain as stored | red |

Every Acceptance Criterion AC-001 … AC-011 has at least one red test.
