---
artifact_type: ac_test_matrix
story: US-077
version: 1
status: DRAFT
created_at: 2026-11-08T13:37:28Z
updated_at: 2026-11-08T13:37:28Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-077-diagnostics-before-pilot.md
    version: null
  - path: docs/specifications/US-077-spec.md
    version: 2
  - path: docs/tests/US-077-test-strategy.md
    version: 1
supersedes: null
---

# US-077 Acceptance Criteria to Test Matrix

Status is the red-phase result on the code before implementation: **RED** fails for the missing behaviour; **GUARD**
passes today and must keep passing (the reason is in the test generation report, section 4).

Classes: **R** = `Infrastructure/Google/GoogleClassroomReaderTokenReuseTests`, **S** = `Web/BackgroundServices/TokenReuseSyncTests`,
**K** = `Web/Logging/GoogleSignInFailureKindTests`, **E** = `Architecture/EventIdCollisionTests`,
**O** = `Infrastructure/Persistence/WorkspaceConnectionRepositoryOrderingTests`, **V** = `Architecture/VersionedStaticFileRuleTests`,
**P** = `Web/Pages/VersionedStaticFilePageTests`.

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Consecutive reads of one pair | Adapter | R | `ConsecutiveReads_ForOnePair_RequestOneToken_AndEveryReadCarriesIt` | One token, carried by all | RED |
| AC-001 | Every page | Adapter | R | `EveryPageOfAPagedRead_CarriesTheRememberedToken` | One token for three pages | GUARD |
| AC-001 | Whole run | Integration | S | `AWholeRun_RequestsOneTokenPerScopeSet` | Two tokens in a run | RED |
| AC-002 | Other account | Adapter | R | `DifferentAccounts_EachGetTheirOwnToken_AndNeverTheOthers` | One each, never crossed | RED |
| AC-002 | Account case (FR-002) | Adapter | R | `AccountsDifferingOnlyByCase_AreDifferentPairs` | Two tokens | GUARD |
| AC-002, AC-011 | Scope sets, exact scopes | Adapter | R | `TheTwoScopeSets_EachGetTheirOwnToken_WithExactlyTheirScopes` | One each with its own scopes | RED |
| AC-003 | Margin 3299 s | Adapter | R | `ATokenAtTheMargin_IsReusedOnlyBeforeIt` (3299) | Reused | RED |
| AC-003 | Margin 3300, 3301 s | Adapter | R | `ATokenAtTheMargin_IsReusedOnlyBeforeIt` (3300, 3301) | New token | GUARD |
| AC-003 | Expired: new token first | Adapter | R | `AnExpiredToken_IsReplacedBeforeTheReadIsSent` | Token before read | GUARD |
| AC-003 | Expiry from before the request | Adapter | R | `Expiry_IsCountedFromBeforeTheTokenRequestWasSent` | New token at the margin | GUARD |
| AC-003 | Paged read crosses margin | Adapter | R | `APagedReadCrossingTheMargin_TakesANewTokenForTheNextPage` | New token for page 2 | RED |
| AC-003 | Retry takes token then | Adapter | R | `ARetriedAttempt_TakesItsTokenAtThatMoment` | Retry has the new token | RED |
| AC-003 | VR-002: not remembered | Adapter | R | `AShortLivedToken_IsUsedOnce_AndNotRemembered` (null, 1, 300) | Used once | GUARD |
| AC-003 | VR-002: remembered at 301 | Adapter | R | `ATokenJustAboveTheMargin_IsRemembered` | Reused | RED |
| AC-003 | VR-002a | Adapter | R | `ATokenAlreadyExpired_IsNotSent_AndTheReadEndsUnexpected` (0, -1) | Unexpected, not sent | RED |
| AC-004 | One pair, N reads | Adapter | R | `ConcurrentReads_ForOnePair_CauseExactlyOneTokenRequest` | Exactly one | RED |
| AC-004 | K pairs | Adapter | R | `ConcurrentReads_ForSeveralPairs_CauseOneTokenRequestPerPair` | Exactly K, none crossed | RED |
| AC-004 | Refusal: one at a time | Adapter | R | `ConcurrentReads_WhenTheEndpointRefuses_EachAskOnce_OneAtATime` | N requests, one in flight | RED |
| AC-004 | m-1: cannot be remembered | Adapter | R | `ConcurrentReads_WithATokenThatCannotBeRemembered_EachUseTheirOwn_OneAtATime` | N, one in flight | RED |
| AC-004 | Cancelled waiter | Adapter | R | `AReadCancelledWhileWaiting_MakesNoTokenRequest` | No request | RED |
| AC-004 | m-2: outside the 100 s bound | Adapter | R | `TheTokenRequest_AndTheWaitForIt_AreNotChargedToTheClassroomAttempt` | One attempt each | RED |
| AC-005 | Refusal, diagnosis, recovery | Adapter | R | `ARefusal_KeepsItsDiagnosis_AndTheNextReadAsksAgain` (3 rows) | Same diagnosis, next asks | GUARD |
| AC-005 | Outage: four attempts | Adapter | R | `ATokenOutage_EndsTheReadAfterFourAttempts_AndIsNotRemembered` (503, network) | Transient, 4 attempts | GUARD |
| AC-005 | VR-001 empty token | Adapter | R | `AnEmptyToken_IsNotSent_AndTheReadEndsUnexpected` | Unexpected, not sent | RED |
| AC-005 | No fallback after expiry | Adapter | R | `ARefusalAfterExpiry_DoesNotFallBackToTheExpiredToken` | Fails, old not sent | GUARD |
| AC-005 | Removed key (I-3) | Adapter | R | `ARemovedKey_IsNoticedWhenATokenIsNeeded` | KeyUnavailable when needed | RED |
| AC-005 | 401: new token | Adapter | R | `A401_ReplacesTheToken_AndTheAttemptIsRepeatedWithTheNewOne` | Repeated; new one remembered | RED |
| AC-005 | 401: attempts as today | Adapter | R | `EveryAttemptAnswering401_StopsAsToday` | At most 3, Unexpected | GUARD |
| AC-005 | 401 for an older token | Adapter | R | `A401ForAnOlderToken_DoesNotDropTheNewerOne` | Newer stays | RED |
| AC-006 | Key per token request | Adapter | R | `TheKey_IsResolvedOncePerTokenRequest_AndNotForAReusedToken` | One resolve per request | RED |
| AC-006 | Reader holds token, not key | Adapter | R | `BetweenRequests_TheReaderHoldsTheToken_ButNothingOfTheKey` | Token yes, key no | RED |
| AC-006 | No `ToString` exposure | Adapter | R | `NothingTheReaderHolds_ExposesTheTokenThroughToString` | Not shown | RED |
| AC-006 | Expired forgotten (I-4) | Adapter | R | `AnExpiredToken_IsForgottenAtTheNextTokenRequestOfAnyPair` | Gone | GUARD |
| AC-006 | Valid token stays | Adapter | R | `AValidToken_StaysWhenAnotherPairRequestsOne` | Reused | RED |
| AC-006 | Not in log or failure | Adapter | R | `NeitherTheTokenNorTheKey_AppearInALogLineOrAFailure` | Absent | GUARD |
| AC-006 | Not in log or database | Integration | S | `AfterARun_TheTokenIsInNoLogLine_AndInNoStoredValue` | Absent | GUARD |
| AC-007 | CorrelationFailed, real handler | Integration | K | `AMissingCorrelationCookie_IsLoggedAsCorrelationFailed`, `AnUnknownState_IsLoggedAsCorrelationFailed` | One Warning; nothing leaked; audit as before | RED |
| AC-007 | TokenExchangeFailed, real handler | Integration | K | `ATokenEndpointFailure_IsLoggedAsTokenExchangeFailed` | Same | RED |
| AC-007 | GoogleError, real handler | Integration | K | `ARefusedConsent_IsLoggedAsGoogleError`, `AnErrorFromGoogle_IsLoggedAsGoogleError` | Same | RED |
| AC-007 | Other, real handler | Integration | K | `ANetworkFailureDuringTheExchange_IsLoggedAsOther`, `AnUnreadableErrorAnswer_IsLoggedAsOther`, `AFailedUserInformationRead_IsLoggedAsOther` | Same | RED |
| AC-007 | FR-006 table | Integration | K | `AFailure_IsClassifiedByTheTable_AndItsMessageIsNotWritten` (16 rows), `NoFailureObject_IsOther`, `AnotherExceptionType_CarryingNoErrorData_IsOther` | Exact kind; message not written | RED |
| AC-007 | Other use of the event | Integration | K | `ARefusedIdentity_CarriesNoKind` | No kind | GUARD |
| AC-008 | New id | Source | E | `ReadOnlyWriteRefused_HasTheIdFrom_FR007`, `TheTwoIds_BelongToOneEventEach` | 5290; one event each | RED |
| AC-008 | Retention id; scan not vacuous | Source | E | `RetentionPurgeStarted_KeepsItsId`, `TheScan_FindsKnownEvents` | Unchanged | GUARD |
| AC-009 | Display, update, no row | Integration | O | `ReadingForDisplay_RaisesNoWarning_AndReturnsTheRowUntracked`, `ReadingForUpdate_RaisesNoWarning_AndReturnsTheRowTracked`, `WithNoRow_BothReadsAnswerNull_WithoutTheWarning` | No warning; same row and tracking | RED |
| AC-010 | The 21 references | Source | V | `TheListedReference_CarriesTheContentVersion` (21 rows) | Attribute present | RED |
| AC-010 | Every own reference | Source | V | `EveryReferenceToTheHostsOwnFiles_CarriesTheContentVersion` (2 hosts) | Attribute present | RED |
| AC-010 | Rendered pages, both hosts, error page | Page | P | `AnInstallationPage_…`, `TheInstallationErrorPage_…`, `ThePageWithAScript_…`, `AControlPlaneSignInPage_…`, `AControlPlaneSetupPage_…`, `AControlPlaneErrorPage_…RefersToItsOwnFilesByContentVersion` | `v=` is the file's SHA-256; no inline code | RED |
| AC-010 | Served under CSP | Page | P | `AVersionedAddress_IsServedUnderThePagesPolicy` | 200, type, CSP | GUARD |
| AC-011 | Only two tag helpers | Source | V | `TheViewImports_EnableOnlyTheTwoTagHelpers` (2 hosts) | Exactly two | RED |
| AC-011 | None in a single view | Source | V | `NoView_AddsTagHelpersOfItsOwn` (2 hosts) | None | GUARD |
| AC-011 | Forms unchanged | Page | P | `AForm_StillCarriesOneAntiforgeryField` | One field | GUARD |
| AC-011 | Scopes unchanged | Adapter | R | `TheTwoScopeSets_EachGetTheirOwnToken_WithExactlyTheirScopes` | Exact sets | RED |
| AC-011 | Translation files, NuGet | Diff | — | IMPLEMENTATION_VERIFICATION reads `git diff`: no `.resx`, no `PackageReference` (strategy §5) | Unchanged | Not automated |
