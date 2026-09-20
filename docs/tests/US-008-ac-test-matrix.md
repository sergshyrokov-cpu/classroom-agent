---
artifact_type: ac_test_matrix
story: US-008
version: 2
status: DRAFT
created_at: 2026-09-20T00:00:00Z
updated_at: 2026-09-20T07:55:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-008-admin-google-sign-in.md
    version: null
  - path: docs/specifications/US-008-spec.md
    version: 2
  - path: docs/decisions/US-008-open-decisions.md
    version: 4
  - path: docs/designs/api/US-008-api-design.md
    version: 1
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
  - path: docs/designs/database/US-008-db-design.md
    version: 1
  - path: docs/designs/database/US-008-entity-model.md
    version: 1
  - path: docs/tests/US-008-test-strategy.md
    version: 2
supersedes: null
---

# US-008 Acceptance Criteria to Test Matrix

Every Acceptance Criterion has at least one mapped scenario. `TEST_WRITING` runs
immediately before `IMPLEMENTATION` and there is no separate verification stage,
so a green suite **is** the evidence that the Acceptance Criteria hold.

Status column: **RED** = fails now for missing production behaviour (the expected
state before implementation); **GREEN** = passes now and must keep passing (a
guarantee that already holds and the Story must not break).

All classes live under `tests/ClassroomAgent.Tests/`. The `Test Method` column
names the method; a `[Theory]` row covers every case of its data set.

## AC-001 The installation starts only with its new mandatory configuration

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| All four new settings valid | integration | `Web/Configuration/InstallationOAuthSettingsTests` | `AllRequiredSettingsValid_HostStarts` | the host starts | GREEN |
| Each new setting missing | integration | same | `MissingRequiredSetting_HostDoesNotStart_NamingTheKey` | no start; the log names the key | RED |
| Public base address malformed (10 shapes) | integration | same | `InvalidPublicBaseAddress_HostDoesNotStart` | no start; VR-001 | RED |
| Public base address at the bounds | integration | same | `BoundaryValidPublicBaseAddress_HostStarts` | the host starts | GREEN |
| Client id / secret reference blank | integration | same | `BlankOAuthSetting_HostDoesNotStart` | no start; VR-002 | RED |
| Credentials not verified against Google | integration | same | `OAuthCredentialsAreNotVerifiedAgainstGoogle_HostStarts` | the host starts (I-5) | GREEN |
| Key directory is a file | integration | same | `KeyDirectoryThatIsAFile_HostDoesNotStart` | no start; VR-003 | RED |
| Key ring written to the directory | integration | same | `KeyRing_IsPersistedToTheConfiguredDirectory` | key files on disk (S-18) | RED |
| Key ring not in the database | integration | same | `KeyRing_IsNotInTheDatabase` | no key table | GREEN |
| Default language optional / valid / invalid | integration | same | `DefaultLanguageIsOptional_HostStartsWithoutIt`, `ValidDefaultLanguage_HostStarts`, `InvalidDefaultLanguage_HostDoesNotStart` | VR-004 | GREEN / GREEN / RED |
| A refusal names the key, never the value | integration | same | `Refusal_NamesTheKeyAndNotTheValue` | SC-10 | RED |
| Client id and secret reference never logged | integration | same | `ClientIdAndSecretReference_AreNeverLogged` | absent from the log (S-08) | GREEN |
| The secret is not a configuration setting | integration | same | `TheSecretItself_IsNotAConfigurationSetting` | only a reference (SC-7) | GREEN |
| Redirect URI built from the base address | integration | `Web/Security/GoogleSignInStartTests` | `TheRedirectUri_ComesFromTheConfiguredPublicBaseAddress` | the configured address | RED |
| The secret is resolved from the named environment variable | integration | `Web/Configuration/InstallationOAuthSettingsTests` | `TheSecret_IsResolvedFromTheNamedEnvironmentVariable` | the handler gets the stored secret (OD-004) | RED |
| An absent or empty secret stops the start | integration | same | `AReferenceNamingAnAbsentOrEmptySecret_HostDoesNotStart` | fail fast; the key named (OD-004) | RED |
| The secret's own value never reaches a log | security | same | `TheSecretValue_IsNeverLogged` | S-08, SC-10 | RED |

**Fully covered.** OD-004 was resolved on 2026-09-20 (option 1 — the reference names
an environment variable), so the clause v1 of this matrix listed as uncovered now
has the three cases above.

## AC-002 The public port denies by default

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| A fallback policy requires an authenticated user | security | `Web/Security/PublicPortAuthorizationTests` | `TheHost_SetsAFallbackPolicyRequiringAnAuthenticatedUser` | the policy exists (API-9) | RED |
| Endpoint enumeration against the SC-4 list | security | same | `OnlySc4EndpointsAllowAnonymous` | no anonymous endpoint outside the list | RED |
| The landing page and sign-out are protected | security | same | `TheLandingPageAndSignOut_RequireAnAuthenticatedUser` | not anonymous | RED |
| An anonymous landing page is challenged, no return URL | contract | same | `AnonymousLandingPage_RedirectsToSignIn_WithNoReturnUrl` | `302 /sign-in` | RED |
| An unmatched address answers 404 to anyone | contract | same | `AnonymousUnknownAddress_Returns404_NotASignInRedirect` | `404`, no redirect | GREEN |
| The private port is unchanged | integration | same | `ThePrivatePort_IsUnchangedByThisStory` | US-005/US-006 behaviour | GREEN |
| Private endpoints invisible on the public port | integration | same | `ThePrivateEndpoints_AnswerNothingOnThePublicPort` | `404` (DC-6) | GREEN |

## AC-003 The Admin starts and completes a Google sign-in

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| POST with the token redirects to Google, sets the correlation cookie | contract | `Web/Security/GoogleSignInStartTests` | `Post_WithToken_RedirectsToGoogle_AndSetsTheCorrelationCookie` | `302` + correlation cookie | RED |
| The authorization endpoint is Google's own | contract | same | `TheAuthorizationRequest_GoesToGoogle` | `accounts.google.com` | RED |
| Identity scopes only | security | same | `TheAuthorizationRequest_AsksForIdentityScopesOnly` | `openid email profile`; no data scope (S-06) | RED |
| Redirect URI from configuration, not the request host | security | same | `TheRedirectUri_ComesFromTheConfiguredPublicBaseAddress`, `TheRedirectUri_IgnoresTheRequestHostAndForwardedHeaders` | the configured address | RED |
| Domain hint when known / absent when never confirmed / suspended | integration | same | `WhenTheDomainIsKnown_TheAccountPickerHintCarriesIt`, `WhenNoCheckHasEverSucceeded_NoAccountPickerHintIsSent`, `WhenSuspended_TheHintIsStillSent` | OD-002 | RED |
| A GET does not start a sign-in | contract | same | `Get_DoesNotStartASignIn` | `405` | RED |
| No antiforgery token starts nothing | security | same | `PostWithoutTheAntiforgeryToken_StartsNothing` | `400` + `Error.PageExpired` | RED |
| State and client id are never logged | security | same | `TheStart_LogsNoStateAndNoClientId` | absent from the log | RED |
| The sign-in page writes nothing | integration | same | `TheSignInPage_WritesNothing` | no row anywhere | RED |
| A valid callback signs in | integration | `Web/Security/GoogleSignInCallbackTests` | `ValidCallback_SignsIn_AndIssuesTheSessionCookie` | `302 /` + session cookie | RED |
| No state / unknown state / no correlation cookie / replay | security | same | `CallbackWithoutState_IsRefused_AndNothingHappens`, `CallbackWithAnUnknownState_IsRefused_AndNothingHappens`, `CallbackWithoutTheCorrelationCookie_IsRefused_AndNothingHappens`, `ReplayingAState_IsRefused` | refused; no Control Plane call; nothing created (S-10, VR-007) | RED |
| Unverified email / no email | security | same | `UnverifiedEmail_IsRefusedAsAFailedCallback_WithoutAskingTheControlPlane`, `NoEmailAtAll_IsRefusedAsAFailedCallback` | refused; category `callback_failed` (VR-005, I-6) | RED |
| The email is lower-cased before everything | integration | same | `MixedCaseEmail_IsLowerCasedBeforeEverything` | BR-079 | RED |
| No Google token or subject id stored | security | same | `NoGoogleTokenAndNoSubjectIdentifier_IsStored` | no such column or row (S-09) | RED |
| The callback is a GET, so no antiforgery applies | security | same | `TheCallback_NeedsNoAntiforgeryToken` | GET/HEAD only | RED |
| Code and state never logged | security | same | `TheCallback_LogsNeitherTheCodeNorTheState` | absent from the log | RED |

## AC-004 AllowedAdmin is asked of the Control Plane at every sign-in

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| A second sign-in asks again | security | `Web/UseCases/AdminLoginCheckEveryTimeTests` | `ASecondSignIn_AsksTheControlPlaneAgain` | two requests (S-02) | RED |
| A revocation between sign-ins takes effect at once | security | same | `AfterASuccessfulSignIn_ARevocationRefusesTheNextOne` | the second is refused | RED |
| Installation id and email in the body of a POST | contract | same | `TheQuestion_IsAPostCarryingTheInstallationIdAndEmailInTheBody` | body only, no query | RED |
| No cookie and no authorization header on the channel | security | same | `TheQuestion_CarriesNoCookieAndNoAuthorizationHeader` | network isolation | RED |
| No installation table holds a copy of AllowedAdmin | security | same | `NoInstallationTable_HoldsACopyOfAllowedAdmin` | three tables only (SC-3) | RED |
| Browsing with a session asks nobody; a new sign-in always asks | integration | same | `AnExistingSession_DoesNotSkipTheCheckForANewSignIn` | two requests | RED |
| A restart cannot resurrect an earlier answer | integration | same | `AfterARestart_TheQuestionIsAskedAgain` | asked again; refusal honoured | RED |

## AC-005 The Control Plane answers the Admin login check

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| Approved email | contract | `ControlPlane/Controllers/AdminLoginCheckEndpointTests` | `ApprovedEmail_Returns200_AllowedTrue_AndNothingElse` | `200`, `allowed` true | RED |
| Not approved | contract | same | `EmailNotApproved_Returns200_AllowedFalse` | `200`, `allowed` false | RED |
| Another installation's entry never matches | contract | same | `AnotherInstallationsEntry_NeverMatches` | per-installation scoping | RED |
| Lower-cased and trimmed comparison | contract | same | `EmailIsComparedLowerCasedAndTrimmed` | BR-079, VR-006 | RED |
| A suspended installation is answered identically | contract | same | `InstallationStatus_DoesNotChangeTheAnswer` | the same answer | RED |
| Exactly one property, nothing else | security | same | `Answer_CarriesNoIdentifierNoListAndNoOtherSchoolData` | SC-12, S-16 | RED |
| The check writes nothing | security | same | `Checks_WriteNothing_NoAuditNoLicenceCheckNoInstallationChange` | no row changes (S-17) | GREEN |
| The log carries the installation id and the outcome, never the email | security | same | `Log_CarriesTheInstallationIdAndOutcome_NeverTheEmail` | SC-10 | RED |
| Both sides of the wire contract agree | contract | same (endpoint) + `Web/UseCases/AdminLoginCheckEveryTimeTests` (client) | `TheQuestion_IsAPostCarryingTheInstallationIdAndEmailInTheBody` | the client's body is what the endpoint accepts | RED |

## AC-006 The Control Plane refuses an unknown or malformed login check

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| Unknown installation id | contract | `ControlPlane/Controllers/AdminLoginCheckEndpointTests` | `UnknownInstallation_Returns404_UnknownInstallationOnly` | `404` `unknown_installation` | RED |
| The refusal reveals nothing about any email | security | same | `UnknownInstallation_AnswerIsIdenticalForAnApprovedAndAnUnknownEmail` | identical answers (S-14) | GREEN |
| 17 malformed request shapes | validation | same | `InvalidRequest_Returns400_InvalidRequestOnly_RecordsNothing` | `400` `invalid_request`; nothing recorded (VR-006) | RED |
| Email length at the 254/255 boundary | validation | same | `EmailLength_254IsAccepted_255IsRejected` | VR-006 | RED |
| Unknown extra properties ignored | contract | same | `UnknownExtraProperties_AreIgnored` | DC-12 | RED |
| POST only | security | same | `OtherMethods_AreNotHandled` | `404`/`405` | GREEN |
| No session, no antiforgery token | security | same | `WithoutSessionAndWithoutAntiforgeryToken_IsProcessed` | processed (SC-4 exemption) | RED |
| The setup gate still applies | contract | same | `BeforeOwnerSetup_RedirectsToSetup_RecordsNothing` | `302 /setup` | RED |
| A rejected body is never logged; an unknown id is a Warning | security | same | `RejectedBody_IsNeverLogged_AndTheUnknownIdIsLoggedAsWarning` | SC-10 | RED |
| The endpoint is on the SC-4 anonymous and exemption lists | security | `ControlPlane/Security/AdminLoginCheckSecurityTests` | `TheLoginCheck_IsAnonymousPostOnly`, `TheLoginCheck_IsExemptFromAntiforgery`, `OnlyTheTwoServiceChannelPosts_AreAnonymousAndExempt` | declared members, nothing more | RED |
| No other Control Plane endpoint becomes anonymous | security | same | `OwnerPages_StayAuthenticated` | unchanged | GREEN |

## AC-007 An approved Admin gets an AppUser at the first sign-in

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| First sign-in creates the account with no password | integration | `Web/UseCases/AdminProvisioningTests` | `FirstSignIn_CreatesTheAdminAppUser_WithNoPassword` | role `admin`, `google`, hash null (S-04, BR-011) | RED |
| The school default language is set | integration | same | `FirstSignIn_SetsTheSchoolDefaultLanguage`, `WithoutTheLanguageSetting_TheAppUserIsUkrainian` | FR-011 | RED |
| The sign-in time is recorded | integration | same | `FirstSignIn_RecordsTheTimeOfTheLastSuccessfulSignIn` | the clock's now (PC-11) | RED |
| A second sign-in reuses the row | integration | same | `SecondSignIn_ReusesTheRow_AndOnlyMovesTheStamp` | one row; only the stamp moves | RED |
| Two Admins get one row each | integration | same | `TwoApprovedAdmins_GetOneRowEach` | two rows, both Admin | RED |
| A second row for an address is impossible | persistence | same | `ASecondRowForTheSameEmail_IsImpossible` | the unique index rejects it | RED |
| A concurrent first sign-in is a re-read | integration | same | `TwoSimultaneousFirstSignIns_CreateOneRow_AndNeitherFails` | one row; neither fails (I-10) | RED |
| Identity fields never reach a response | security | same | `TheStoredStamps_NeverReachAResponse` | AD-8 | RED |
| Every `app_user` column, constraint and index | persistence | `Infrastructure/Persistence/AppUserSchemaTests` | 13 methods (see file) | db-design 3 exactly | RED (1 GREEN) |
| An Admin row with any password hash is rejected | persistence | same | `AnAdminRowWithAPasswordHash_IsRejected` | the database refuses it (S-04) | RED |

## AC-008 A revoked Admin is refused and keeps their AppUser

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| Refused, row unchanged | integration | `Web/UseCases/RevokedAdminTests` | `RevokedAdmin_IsRefused_AndKeepsTheirRowUnchanged` | refusal; the row byte-identical | RED |
| The row survives repeated attempts | integration | same | `RevokedAdmin_RowIsNotDeleted_EvenAfterSeveralAttempts` | one row; three calls | RED |
| Audited against the existing account | integration | same | `RevokedAdmin_IsAuditedAgainstTheirExistingAccount` | actor is the account (FR-012) | RED |
| A stranger with no account is anonymous | integration | same | `AnUnapprovedStrangerWithNoAccount_IsAuditedAnonymously` | `anonymous`, no identifier | RED |
| The refusal reveals nothing | security | same | `TheRefusalIsIdentical_ForAKnownRevokedAdminAndAnUnknownStranger` | identical pages (S-14) | RED |
| The refusal is not in the address | security | same | `TheRefusal_IsNotCarriedInTheAddress`, `ACraftedQueryParameter_RendersNoMessage` | TempData only (api-design 2.2) | RED |
| Logged as a Warning without the email | security | same | `TheRefusal_IsLoggedAsAWarningWithoutTheEmail` | SC-10 | RED |

## AC-009 A Control Plane that does not answer refuses the Admin sign-in

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| Nine unusable answers | integration | `Web/UseCases/ControlPlaneUnavailableSignInTests` | `AnUnusableAnswer_RefusesTheSignIn_WithTheUnavailableCategory` | refusal; `control_plane_unavailable` | RED |
| A 404 with the outcome body | integration | same | `A404WithTheOutcomeBody_IsTheUnknownInstallationCategory` | `unknown_installation` | RED |
| A bare 404 is never "not approved" | integration | same | `ABare404_IsNeverReadAsNotApproved` | api-design 2.4 | RED |
| An earlier success grants nothing | security | same | `AnEarlierSuccess_DoesNotAdmitTheAdminWhenTheControlPlaneIsSilent` | refusal; the stamp unchanged (S-03) | RED |
| A translated message, not a raw error | contract | same | `TheRefusalMessage_SaysTheApprovalCouldNotBeConfirmed` | `SignIn.Refused.CouldNotConfirm` | RED |
| Logged at Error, without the response body | security | same | `TheCause_IsLoggedAsAnErrorCategory_WithoutTheResponseBody` | SC-10, DC-10 | RED |
| The legitimacy state is untouched | integration | same | `TheLegitimacyState_IsUntouchedByARefusedSignIn` | BR-012 | RED |
| The log level per cause | security | `Web/Logging/SignInLoggingTests` | `AnUnavailableControlPlane_IsLoggedAtError` | FR-020 | RED |

## AC-010 Sign-in and refused sign-in are audited without personal data

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| A success writes one `succeeded` row | integration | `Web/UseCases/AdminSignInAuditTests` | `ASuccessfulSignIn_WritesOneSucceededRow` | every column as FR-012 fixes it | RED |
| One row per attempt | integration | same | `EveryAttempt_WritesExactlyOneRow` | three rows for three attempts | RED |
| The request id ties the row to its log line | security | same | `TheRequestId_TiesTheRowToItsLogLine` | SC-11 | RED |
| No row carries a personal datum | security | same | `NoRow_CarriesAPersonalDatum` | no email, name or subject id (S-13) | RED |
| A row cannot be updated | persistence | same | `ARow_CannotBeUpdatedThroughEfCore_ThoughRawSqlIsNotDefendedAgainst` | the constraint rejects it | RED |
| The timestamps are equal | persistence | same | `ARowsTimestamps_AreEqual` | PC-6 | RED |
| The account and its row land together | integration | same | `TheAccountAndItsRow_AreWrittenTogether` | one transaction | RED |
| Only the one action of this Story | integration | same | `OnlyTheAdminSignInAction_IsWritten` | `admin_sign_in` only (I-11) | RED |
| Sign-out writes no row | integration | same | `SignOut_WritesNoRow` | I-13 | RED |
| Every `audit_event` column and constraint | persistence | `Infrastructure/Persistence/InstallationAuditEventSchemaTests` | 14 methods (see file) | db-design 4 exactly | RED (1 GREEN) |
| Refusal categories per cause | integration | `RevokedAdminTests`, `ControlPlaneUnavailableSignInTests`, `GoogleSignInCallbackTests` | as listed above | the five categories | RED |

## AC-011 Admin sign-in works in read-only mode

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| Every read-only cause admits an approved Admin | integration | `Web/UseCases/SignInInReadOnlyModeTests` | `InEveryReadOnlyCause_AnApprovedAdminSignsIn` | the sign-in succeeds | RED |
| The account is created and stamped | integration | same | `InReadOnlyMode_TheAccountIsCreatedAndStamped` | `SignInBookkeeping` runs | RED |
| The audit row is written | integration | same | `InReadOnlyMode_TheAuditRowIsWritten` | `AuditEvent` runs | RED |
| A refusal is audited too | integration | same | `InReadOnlyMode_ARefusalIsStillAudited` | `refused` row | RED |
| The BR-026 list keeps four members | unit | same | `TheBr026List_StillHasExactlyFourMembers` | not widened (FR-013) | RED |
| The sign-in write is declared, not exempt | unit | same | `TheSignInWrite_IsDeclaredAgainstSignInBookkeepingOrAuditEvent` | US-007 FR-005 | RED |
| Nothing else writes in read-only mode | integration | same | `InReadOnlyMode_NothingElseIsWritten` | the legitimacy state untouched | RED |

## AC-012 A read-only refusal that reaches HTTP answers 409

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| Each of the three reasons under `/api/v1` | contract | `Web/Security/ReadOnlyConflictTests` | `UnderApiV1_TheRefusalIs409WithTheApi6Body` | `409` + the API-6 body naming the reason | RED |
| The message is in the user's language | contract | same | `TheRefusalMessage_IsInTheRequestingUsersLanguage` | both `uk` and `en` (TC-3) | RED |
| The two languages differ | contract | same | `TheUkrainianAndEnglishMessages_Differ` | NFR-073 | RED |
| Outside `/api/v1` the refusal is also 409 | contract | same | `OutsideApiV1_TheRefusalIsAlso409` | the error page path | RED |
| No internals leak | security | same | `TheRefusalBody_LeaksNoInternals` | S-19 | RED |
| The body is JSON under `/api/v1` | contract | same | `UnderApiV1_TheBodyIsJson` | API-6 | RED |

**How it is proven:** by invoking the host's registered `IExceptionHandler`
directly rather than through the test-only probe endpoint api-design 2.5
suggested — see the test-generation report, section 5.

## AC-013 Session, cookies and HTTPS

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| The session cookie's attributes | security | `Web/Security/InstallationCookieTests` | `TheSessionCookie_IsHostPrefixedSecureHttpOnlyLax` | `Secure`, `HttpOnly`, `Lax` | RED |
| Not persistent | security | same | `TheSessionCookie_HasNoExpiresAndNoMaxAge` | no `Expires`/`Max-Age` (NFR-072) | RED |
| The antiforgery cookie's attributes | security | same | `TheAntiforgeryCookie_IsHostPrefixedSecureHttpOnlyStrict` | `Secure`, `HttpOnly`, `Strict` | RED |
| Every cookie of the flow is Secure | security | same | `EveryCookieOfTheSignInFlow_IsSecure` | correlation cookie included | RED |
| The public-port exemption list is empty | security | same | `NoPublicPortEndpoint_IsExemptFromAntiforgery` | only the private push receiver | GREEN |
| Every state-changing endpoint refuses a tokenless request | security | same | `EveryStateChangingPublicEndpoint_WithoutToken_Returns400` | `400` + `Error.PageExpired` (TC-5) | RED |
| HTTP redirects to HTTPS | security | same | `ThePublicPort_RedirectsHttpToHttps` | a redirect to `https://` | RED |
| HSTS on the public port only | security | same | `ThePublicPortSendsHsts_AndThePrivatePortDoesNot` | DC-6 | RED |
| 60-minute idle limit | integration | `Web/Security/InstallationSessionLifetimeTests` | `SixtyMinutesWithoutARequest_TheSessionExpires` | 59 min works, 60 min + 1 s does not | RED |
| 8-hour absolute limit | integration | same | `EightHoursAfterSignIn_TheSessionExpiresDespiteActivity` | expires despite activity | RED |
| The idle limit slides | integration | same | `ActivityWithinTheIdleLimit_KeepsTheSessionAlive` | stays alive | RED |

## AC-014 Sign-out ends the session

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| Sign-out clears the session | contract | `Web/Security/InstallationSignOutTests` | `SignOut_ClearsTheSession_AndLandsOnTheSignInPage` | `302 /sign-in`; no session | RED |
| The previous cookie no longer authenticates | security | same | `ThePreviousCookieReplayed_NoLongerAuthenticates` | challenged | RED |
| A GET does not sign anyone out | security | same | `Get_DoesNotSignAnyoneOut` | `405`; still signed in (API-4) | RED |
| No token leaves the session untouched | security | same | `WithoutTheAntiforgeryToken_TheSessionIsUntouched` | `400`; still signed in | RED |
| An anonymous sign-out is challenged | security | same | `AnonymousSignOut_IsChallenged` | `302 /sign-in` | RED |
| Sign-out writes no audit row | integration | `Web/UseCases/AdminSignInAuditTests` | `SignOut_WritesNoRow` | I-13 | RED |

## AC-015 The UI is Ukrainian and English

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| The two resource sets have the same keys, none empty | unit | `Web/Localization/InstallationUiTranslationTests` | `EveryKey_ExistsInUkrainianAndEnglish` | TC-8 | RED |
| Every contract key resolves in both languages | unit | same | `EveryContractKey_IsResolvedInBothLanguages` | 24 keys of `PageTextKeys` | RED |
| Ukrainian when the setting is absent | integration | same | `WithoutTheLanguageSetting_ThePagesAreUkrainian` | NFR-073 | RED |
| The school default decides the language | integration | same | `TheSchoolDefault_DecidesTheLanguage` | `en` renders English | RED |
| `Accept-Language` does not select the culture | integration | same | `AcceptLanguage_DoesNotSelectTheCulture` | I-14 | RED |
| Neither a query string nor a cookie selects it | integration | same | `NeitherAQueryStringNorACookie_SelectsTheCulture` | I-14 | RED |
| The email from Google is shown as it came | integration | same | `TheEmailFromGoogle_IsShownAsItIs` | never translated | RED |
| The sign-in page carries only translated text | integration | same | `TheSignInPage_CarriesOnlyTranslatedText` | no hard-coded string | RED |

## AC-016 One error page serves every failure

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| Each of 400, 403, 404, 500 has its own text | contract | `Web/Security/InstallationErrorPageTests` | `EachStatus_GetsItsOwnTranslatedText` | the four text keys | RED |
| The page is anonymous and writes nothing | security | same | `TheErrorPage_IsAnonymousAndWritesNothing` | SC-4 | RED |
| No internals leak | security | same | `TheErrorPage_LeaksNoInternals` | S-19, NFR-023 | GREEN |
| An unmatched address answers the page with 404 | contract | same | `AnUnmatchedAddress_AnswersTheErrorPageWith404` | v66 | RED |
| The antiforgery text offers the page again | contract | same | `TheAntiforgeryRefusal_OffersThePageAgain` | `Error.PageExpired` + a link | RED |
| A forbidden request gets 403 with the page | security | same | `AForbiddenRequest_Gets403WithTheErrorPage` | not a redirect, not 404 | RED |
| The developer exception page is not in play | security | same | `TheDeveloperExceptionPage_IsNotInPlay` | SC-6 | GREEN |
| The page renders in the school's language | integration | same | `TheErrorPage_RendersInTheSchoolsLanguage` | both languages | RED |
| Static files come only from the app's own directory | security | same | `StaticFiles_ComeOnlyFromTheApplicationsOwnDirectory` | SC-4 | GREEN |

## AC-017 The signed-in user sees who they are and the installation's status

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| The email as stored and the role | contract | `Web/Pages/LandingPageTests` | `ThePage_ShowsTheEmailAsStoredAndTheRole` | both shown | RED |
| Outside read-only mode the page says so | contract | same | `WhenNotInReadOnlyMode_ThePageSaysSo` | `Landing.Legitimacy.Ok` | RED |
| Each read-only reason is named | contract | same | `InReadOnlyMode_ThePageNamesTheModeAndTheReason` | the three reasons | RED |
| The time of the last successful check | contract | same | `InReadOnlyMode_TheTimeOfTheLastSuccessfulCheckIsShown` | shown when there is one | RED |
| No time when no check ever succeeded | contract | same | `WhenNoCheckHasEverSucceeded_NoTimeIsShown` | absent | RED |
| No Identity field and no teaching data | security | same | `ThePage_ShowsNoIdentityFieldAndNoTeachingData` | AD-8 | RED |
| Sign-out is a POST form with the token | contract | same | `ThePage_OffersSignOutAsAPostFormWithTheToken` | FR-016 | RED |
| The policy admits Admin and Dean | security | same | `ThePolicy_AdmitsAdminAndDean` | not anonymous | RED |
| Opening the page writes nothing | integration | same | `OpeningThePage_WritesNothing` | AD-6 | RED |

## AC-018 Logs carry identifiers only

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| A success is Information with the account id | security | `Web/Logging/SignInLoggingTests` | `ASuccessfulSignIn_IsLoggedAtInformationWithTheAccountId` | FR-020 | RED |
| A not-approved refusal is a Warning | security | same | `ANotApprovedRefusal_IsLoggedAtWarning` | FR-020 | RED |
| An unavailable Control Plane is an Error | security | same | `AnUnavailableControlPlane_IsLoggedAtError` | FR-020 | RED |
| A failed callback is a Warning | security | same | `AFailedCallback_IsLoggedAtWarning` | FR-020 | RED |
| Every audit row's request id appears in the log | security | same | `EveryAuditRowsRequestId_AppearsInTheLog` | SC-11 | RED |
| Across the whole flow nothing sensitive is logged | security | same | `AcrossTheWholeFlow_TheLogCarriesNoSecretAndNoPersonalDatum` | S-15 | RED |
| No Google profile name | security | same | `TheLog_CarriesNoGoogleProfileName` | SC-10 | RED |
| The Control Plane side | security | `ControlPlane/Controllers/AdminLoginCheckEndpointTests` | `Log_CarriesTheInstallationIdAndOutcome_NeverTheEmail`, `RejectedBody_IsNeverLogged_AndTheUnknownIdIsLoggedAsWarning` | SC-10 | RED |

## FR-021 — the dependency change and the two carried US-007 findings

Not a Story Acceptance Criterion; tracked here because the Definition of Done
requires it.

| Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| F-1: the port yields the decorated chain | unit | `Architecture/SignInWiringTests` | `ResolvingTheUnitOfWork_YieldsTheDecoratedChain` | the logging decorator | GREEN |
| F-1: the undecorated unit of work is unreachable | unit | same | `TheUndecoratedUnitOfWork_IsUnreachableFromDi` | no bare registration (S-20) | RED |
| F-1: the undecorated guard is unreachable | unit | same | `TheUndecoratedReadOnlyGuard_IsUnreachableFromDi` | no bare registration | RED |
| F-2: a read-only refusal is not downgraded | unit | same | `AReadOnlyRefusal_IsNotDowngradedIntoASaveFailure` | rethrown, not `SaveFailed` | RED |
| F-2: an ordinary save failure is still an outcome | unit | same | `AnOrdinarySaveFailure_IsStillAnOutcome` | AD-9 | GREEN |
| One port for one external system | unit | same | `TheControlPlaneChannel_IsOnePort` | AD-4, I-7 | GREEN |
| The migration list, the model, and the Control Plane's schema | persistence | `Infrastructure/Persistence/AppUserMigrationTests` | 6 methods | PC-2, db-design 7 | RED (4 GREEN) |

## Coverage summary

| | Count |
|---|---|
| Acceptance Criteria | 18 |
| Acceptance Criteria with at least one mapped test | **18** |
| Test classes added | 24 |
| Test cases added | 334 |
| Failing before implementation (red) | 290 |
| Passing before implementation (guarantees that already hold) | 44 |
| Existing test cases still passing | 1165 |

No mandatory Acceptance Criterion is without a mapped test, and no clause of one is
left unexercised: OD-004's resolution closed the last gap.
