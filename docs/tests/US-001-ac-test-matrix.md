---
artifact_type: ac_test_matrix
story: US-001
version: 2
status: DRAFT
created_at: 2026-09-16T08:35:56Z
updated_at: 2026-09-16T10:37:11Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-001-owner-first-run-setup.md
    version: null
  - path: docs/specifications/US-001-spec.md
    version: 3
  - path: docs/designs/api/US-001-api-design.md
    version: 1
  - path: docs/designs/api/US-001-openapi.yaml
    version: 1
  - path: docs/designs/database/US-001-db-design.md
    version: 1
  - path: docs/designs/database/US-001-entity-model.md
    version: 1
  - path: docs/decisions/US-001-open-decisions.md
    version: 4
supersedes: null
---

# US-001 Acceptance Criteria → Test Matrix

Test classes live under `tests/ClassroomAgent.Tests/ControlPlane/`, namespace
`ClassroomAgent.Tests.ControlPlane.<Namespace>` mirroring the production
namespace (TC-6). Levels: **H** HTTP integration, **S** security enumeration,
**V** service integration, **P** persistence, **U** unit. Every H, S, V and P test
starts its own Control Plane host over its own migrated PostgreSQL database
(Testcontainers).

**Status:** `RED` — written, compiles, fails for missing production behaviour
(evidence: test-generation report v2). `GUARD` — passes already because the
compile-only skeleton (OD-007) satisfies it; it must stay green. Every `RED` row
turns `GREEN` at IMPLEMENTATION.

Revision 2 (attempt 2) records the written tests; changes against the designed v1
rows are listed after the table.

| AC | Scenario | Lvl | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | home before setup | H | `Security.SetupGateTests` | `Home_WithoutOwner_RedirectsToSetup` | `302`, `Location: /setup` | RED |
| AC-001 | sign-in GET/POST before setup | H | `Security.SetupGateTests` | `SignIn_WithoutOwner_RedirectsToSetup` | both `302 /setup`; no audit row | RED |
| AC-001 | sign-out before setup | H | `Security.SetupGateTests` | `SignOut_WithoutOwner_RedirectsToSetup` | `302 /setup` | RED |
| AC-001 | error page not gated | H | `Security.SetupGateTests` | `ErrorPage_WithoutOwner_IsShown` | `/error/404` → `404`, `Error.NotFound` (uk) | RED |
| AC-001 | unknown path not gated | H | `Security.SetupGateTests` | `UnknownPath_WithoutOwner_Returns404` | `404`, no `Location`, `Error.NotFound` | RED |
| AC-001 | static file not gated | H | `Security.SetupGateTests` | `StaticFile_WithoutOwner_IsServed` | a file under `wwwroot/css|js|images` → `200` | RED |
| AC-001 | setup form fields | H | `Controllers.SetupPageTests` | `SetupPage_WithoutOwner_ShowsCodeLoginPasswordAndConfirmation` | `200`; inputs `setupCode`, `login`, `password`, `passwordConfirmation`; token present; secrets empty; code not in page | RED |
| AC-002 | valid setup | H | `Controllers.SetupSubmissionTests` | `ValidSetup_CreatesOwnerSignsInAndRedirectsHome` | `302 /`; session cookie; `GET /` → `200` | RED |
| AC-002 | exactly one account | P | `Controllers.SetupSubmissionTests` | `ValidSetup_StoresExactlyOneOwner` | 1 row; `user_name` as typed; `normalized_user_name` upper-invariant; counter 0; no lockout | RED |
| AC-002 | hash only | P | `Controllers.SetupSubmissionTests` | `ValidSetup_StoresPasswordOnlyAsVerifiableHash` | `password_hash` ≠ and does not contain the plaintext; verifies with Identity `PasswordHasher` | RED |
| AC-002 | language uk | P | `Controllers.SetupSubmissionTests` | `ValidSetup_SetsUiLanguageUkrainian` | `ui_language = 'uk'` | RED |
| AC-002 | no installation reference | U | `Architecture.ControlPlaneReferenceTests` | `ControlPlane_DoesNotReferenceDomainOrApplication` | neither assembly nor project references `ClassroomAgent.Domain`, `.Application`, `.Infrastructure`, `.Web` | GUARD |
| AC-003 | GET setup after setup, anonymous | H | `Controllers.SetupPageTests` | `SetupPage_WithOwner_Anonymous_RedirectsToSignIn` | `302 /sign-in`; body and headers without the login | RED |
| AC-003 | GET setup after setup, signed in | H | `Controllers.SetupPageTests` | `SetupPage_WithOwner_SignedIn_RedirectsHome` | `302 /` | RED |
| AC-003 | POST setup after setup (stale form, wrong code) | H | `Controllers.SetupSubmissionTests` | `Setup_WhenOwnerExists_ReturnsConflictWithSignInLink` | `409`; `Setup.AlreadyCreated`; link to `/sign-in`; neither login shown; 1 owner; no new audit row | RED |
| AC-003 | stale form with the used code | H | `Controllers.SetupSubmissionTests` | `Setup_WithPreviousCodeAfterOwnerCreated_DoesNotCreateSecondOwner` | `409`; no session cookie; 1 owner, the first login | RED |
| AC-004 | concurrent setups (service) | V | `Services.FirstRunSetupConcurrencyTests` | `TwoConcurrentSetups_CreateOneOwner_LoserGetsConflict` | outcomes {`Created`, `AlreadyExists`}; session only on the winner; 1 owner; 1 success audit row for that owner | RED |
| AC-004 | concurrent setups over HTTP | H | `Controllers.SetupConcurrencyTests` | `TwoConcurrentSetupPosts_OneRedirects_OtherReturns409` | statuses {`302`, `409`}, never `500`; 1 owner; 1 audit row, succeeded | RED |
| AC-004 | singleton at DB level | P | `Persistence.OwnerSchemaTests` | `SecondOwnerRowWithDifferentLogin_ViolatesSingleton` | `23505` on `uq_owner_singleton` | RED |
| AC-005 | correct sign-in | H | `Controllers.SignInTests` | `CorrectCredentials_SignInAndRedirectHome` | `302 /`; session cookie; `GET /` → `200` | RED |
| AC-005 | login case-insensitive | H | `Controllers.SignInTests` | `LoginInDifferentCase_SignsIn` | `302 /` | RED |
| AC-005 | no return URL (I-6) | H | `Controllers.SignInTests` | `ReturnUrl_IsIgnored` | `ReturnUrl` in query and body → `302 /` | RED |
| AC-005 | three refusals identical | H | `Controllers.SignInTests` | `UnknownLoginWrongPasswordAndLockout_ProduceIdenticalResponses` | all `401`; bodies equal after blanking tokens and the typed login; headers equal apart from the antiforgery cookie and length | RED |
| AC-005 | refusal message | H | `Controllers.SignInTests` | `WrongPassword_ShowsCommonRefusalMessage_LoginRefilledPasswordEmpty` | `401`; `SignIn.Refused`; `login` refilled; `password` empty; no session cookie | RED |
| AC-005 | empty field (I-2) | H | `Controllers.SignInTests` | `EmptyLoginOrPassword_Returns400_NoAuditNoCounterChange` (theory ×2) | `400`; `SignIn.Login.Required` / `SignIn.Password.Required`; no audit row; counter 0 | RED |
| AC-005 | 4 failures | V | `Services.OwnerSignInLockoutTests` | `FourFailures_ThenCorrectPassword_SignsIn` | signed in; counter 0; no lockout in force | RED |
| AC-005 | 5 failures lock | V | `Services.OwnerSignInLockoutTests` | `FiveFailures_LockSignIn_EvenWithCorrectPassword` | refused; `lockout_end` = time of the 5th failure + 15 min | RED |
| AC-005 | lockout does not check password | V | `Services.OwnerSignInLockoutTests` | `DuringLockout_WrongPassword_DoesNotChangeCounterOrExtendLockout` | wrong and correct refused; counter and `lockout_end` unchanged | RED |
| AC-005 | release after 15 min | V | `Services.OwnerSignInLockoutTests` | `AfterFifteenMinutes_CorrectPassword_SignsIn` | at 14:59 refused, at 15:01 signed in | RED |
| AC-005 | counter restarts after lockout (I-4) | V | `Services.OwnerSignInLockoutTests` | `AfterLockoutEnds_CounterStartsFromZero` | 4 more failures, then the correct password signs in | RED |
| AC-005 | no permanent lockout | V | `Services.OwnerSignInLockoutTests` | `RepeatedLockouts_AlwaysReleaseAfterFifteenMinutes` | 3 lockout cycles, each released | RED |
| AC-005 | session idle 30 min (OD-002) | H | `Security.SessionLifetimeTests` | `ThirtyMinutesWithoutRequest_SessionExpires` | +29 min `200`; +30 min 1 s idle `302 /sign-in` | RED |
| AC-005 | session absolute 8 h (OD-002) | H | `Security.SessionLifetimeTests` | `EightHoursAfterSignIn_SessionExpiresDespiteActivity` | request every 20 min `200`; 7 h 59 min `200`; 8 h 01 min `302 /sign-in` | RED |
| AC-005 | session cookie not persistent | H | `Security.CookieAttributeTests` | `SessionCookie_HasNoExpiresOrMaxAge` | no `Expires`, no `Max-Age` on the setup and the sign-in cookie | RED |
| AC-006 | login rules | H | `Controllers.SetupValidationTests` | `Login_Boundaries` (theory ×9: 4, 64, `a.b-c_9` valid; 3, 65, space, Cyrillic, `@`, empty) | valid → `302 /`; invalid → `400` with `Setup.Login.Length` / `.Characters` / `.Required` | RED |
| AC-006 | password length in code points | H | `Controllers.SetupValidationTests` | `Password_LengthBoundaries_CountedInCodePoints` (theory ×8: 14, 15, 128, 129 ASCII; 15 Cyrillic; 15, 128, 129 emoji) | valid → `302 /`; invalid → `400`, `Setup.Password.Length` | RED |
| AC-006 | no composition rules | H | `Controllers.SetupValidationTests` | `Password_FifteenLowercaseLettersWithSpaces_IsAccepted` | `302 /` | RED |
| AC-006 | equals / contains login | H | `Controllers.SetupValidationTests` | `Password_EqualToOrContainingLoginInAnyCase_IsRejected` (theory ×3) | `400`, `Setup.Password.ContainsLogin`; 0 owners | RED |
| AC-006 | confirmation (VR-007) | H | `Controllers.SetupValidationTests` | `Confirmation_Mismatch_IsRejected` (theory ×4: one char, case only, trailing space, empty) | `400`, `Setup.PasswordConfirmation.Mismatch` / `.Required`; 0 owners | RED |
| AC-006 | all errors at once | H | `Controllers.SetupValidationTests` | `SeveralInvalidFields_AllReportedTogether` | login, password and confirmation messages in one response | RED |
| AC-006 | secrets not echoed | H | `Controllers.SetupValidationTests` | `ValidationFailure_DoesNotEchoPasswordConfirmationOrCode` | none of the three values in the page; `login` refilled; code, password, confirmation inputs empty | RED |
| AC-006 | not created, not audited | H | `Controllers.SetupValidationTests` | `ValidationFailure_CreatesNoOwnerAndNoAuditRow` | `400`; no session cookie; 0 owners; 0 audit rows | RED |
| AC-007 | code printed at startup | H | `Services.SetupCodeStartupTests` | `StartupWithoutOwner_PrintsCodeToOperatorConsole` | generator called once; operator console line contains the code | RED |
| AC-007 | code not in log file | H | `Services.SetupCodeStartupTests` | `StartupWithoutOwner_LogFileDoesNotContainCode` | the log directory has content; no file contains the code (with or without hyphens), a typed login or a typed password | RED |
| AC-007 | no code with Owner | H | `Services.SetupCodeStartupTests` | `StartupWithOwner_GeneratesAndPrintsNoCode` | restarted host: generator not called; console empty | RED |
| AC-007 | restart regenerates | H | `Services.SetupCodeStartupTests` | `RestartWithoutOwner_NewCodeWorks_PreviousCodeRefused` | old code `400` `Setup.SetupCode.Invalid`; new code `302 /` | RED |
| AC-007 | wrong code | H | `Controllers.SetupSubmissionTests` | `WrongCode_Returns400_CreatesNothing_DoesNotRevealCode` | `400`; `Setup.SetupCode.Invalid`; neither code in the body; no session; 0 owners | RED |
| AC-007 | missing code | H | `Controllers.SetupSubmissionTests` | `MissingCode_Returns400_TreatedAsWrongCode` | empty and absent field → `400`, `Setup.SetupCode.Invalid`; 0 owners | RED |
| AC-007 | code normalization | U | `Services.SetupCodeTests` | `Code_IgnoresHyphensSpacesAndCase` (theory ×11) | case, hyphens, spaces ignored; wrong, short, long, empty, null refused | RED |
| AC-007 | code format | U | `Services.SetupCodeTests` | `ProductionGenerator_Produces26CrockfordBase32Characters` | groups of 4–5 split by `-`; 26 Crockford characters; two calls differ | RED |
| AC-008 | success audit | H | `Controllers.SignInAuditTests` | `SuccessfulSignIn_WritesSucceededRowWithOwnerActor` | row per db-design §4.1; `request_id` set; `occurred_at` = clock; `updated_at` = `created_at` | RED |
| AC-008 | wrong password audit | H | `Controllers.SignInAuditTests` | `WrongPassword_WritesRefusedWrongPassword` | actor and target owner/id; `wrong_password` | RED |
| AC-008 | lockout audit | H | `Controllers.SignInAuditTests` | `LockedOut_WritesRefusedLockedOut` | five `wrong_password` rows, then `locked_out` | RED |
| AC-008 | unknown login audit | H | `Controllers.SignInAuditTests` | `UnknownLogin_WritesRefusedAnonymousWithoutTarget` | actor anonymous, no id, no target; `unknown_login` | RED |
| AC-008 | no personal data | P | `Controllers.SignInAuditTests` | `AuditRows_ContainNoTypedLoginOrPassword` | no row's JSON contains a typed login or password | RED |
| AC-008 | immutable: update | P | `Persistence.AuditEventSchemaTests` | `UpdateOfAuditRow_IsRejectedByDatabase` | raw `UPDATE` fails; value unchanged | RED |
| AC-008 | immutable: delete | P | `Persistence.AuditEventSchemaTests` | `DeleteOfAuditRow_IsRejectedByDatabase` | raw `DELETE` fails; row kept | RED |
| AC-008 | immutable: DbContext | P | `Persistence.AuditEventSchemaTests` | `ModifiedOrDeletedAuditEntity_SaveChangesThrows_NothingSaved` | `Modified` and `Deleted` saves throw; the row is unchanged | RED |
| AC-008 | check constraints | P | `Persistence.AuditEventSchemaTests` | `CheckConstraints_RejectInconsistentRows` (theory ×8 over `ck_audit_event_*`) | `23514` with the named constraint | RED |
| AC-008 | consistent rows accepted | P | `Persistence.AuditEventSchemaTests` | `ConsistentRows_AreAccepted` | the three row shapes of §4.1 insert | RED |
| AC-008 | columns, no personal data column, no FK | P | `Persistence.AuditEventSchemaTests` | `AuditEventColumns_MatchDesign_NoPersonalDataColumn` | columns, types, lengths, nullability exactly per §4; `pk_audit_event`; no foreign key | RED |
| AC-009 | Ukrainian for anonymous | H | `Localization.PageLanguageTests` | `SetupSignInAndErrorPages_AreUkrainianForAnonymous` | with `Accept-Language: en`, error, setup and sign-in pages show the `uk` text and not the `en` text | RED |
| AC-009 | signed-in Owner language | H | `Localization.PageLanguageTests` | `SignedInOwner_SeesAccountLanguage` | account `uk` → `uk` text despite `Accept-Language: en`; account set to `en` → `en` text | RED |
| AC-009 | keys in both languages | H | `Localization.TranslationCompletenessTests` | `EveryKey_ExistsInUkrainianAndEnglish` | `uk` and `en` key sets equal and non-empty; no empty value; all contract keys present | RED |
| AC-009 | contract keys translated | H | `Localization.TranslationCompletenessTests` | `ContractMessageKeys_AreResolvedFromLocalization` (theory ×17) | each key resolves in `uk` and `en`, and the two texts differ | RED |
| AC-010 | anonymous closed list | S | `Security.AnonymousEndpointTests` | `OnlySc4EndpointsAllowAnonymous` | fallback policy set; anonymous = setup, sign-in, `error/{statusCode}`, fallback, static files; `/` and `sign-out` exist and are protected | RED |
| AC-010 | protected home | H | `Security.AnonymousEndpointTests` | `Home_AnonymousWithOwner_RedirectsToSignIn_NoReturnUrl` | `302`, path and query exactly `/sign-in` | RED |
| AC-010 | allowed role | H | `Security.AuthorizationTests` | `Home_SignedInOwner_Returns200` | `200` | RED |
| AC-010 | forbidden principal | H | `Security.AuthorizationTests` | `Home_PrincipalWithoutOwnerRole_Returns403ErrorPage` | the Owner's ticket re-protected without the role → `403`, `Error.Forbidden`, no redirect | RED |
| AC-010 | error page codes | H | `Security.ErrorPageTests` | `ErrorPage_KnownAndUnknownCodes` (theory ×5: 400, 403, 404, 500, 418) | status = code, 418 → `404`; translated text; no internals | RED |
| AC-010 | catch-all any method | H | `Security.ErrorPageTests` | `UnmatchedRequest_AnyMethod_Returns404` (theory ×4) | GET/POST/PUT/DELETE, anonymous and signed in → `404`, never a redirect | RED |
| AC-010 | 500 hides internals | H | `Security.ErrorPageTests` | `UnhandledException_ShowsErrorPageWithoutInternals` | schema broken under the host → `500`, `Error.Internal`; no exception, type, namespace, SQL or path text | RED |
| AC-011 | antiforgery enumeration | S | `Security.AntiforgeryTests` | `EveryNonGetEndpoint_WithoutToken_Returns400` | every state-changing endpoint (setup, sign-in anonymous; sign-out as Owner) → `400`, `Error.PageExpired` | RED |
| AC-011 | setup without token | H | `Security.AntiforgeryTests` | `SetupWithoutToken_CreatesNoOwner_ShowsPageExpired` | `400`; `Error.PageExpired`; no session; login not kept; 0 owners; 0 audit rows | RED |
| AC-011 | sign-in without token | H | `Security.AntiforgeryTests` | `SignInWithoutToken_DoesNotSignIn` | `400`; no session cookie; no audit row; `GET /` still redirects | RED |
| AC-011 | no exemption | S | `Security.AntiforgeryTests` | `NoEndpointIsExemptFromAntiforgery` | no endpoint carries a disabling `IAntiforgeryMetadata` or `IgnoreAntiforgeryToken` | RED |
| AC-011 | sign-out POST | H | `Controllers.SignOutTests` | `SignOut_EndsSession_RedirectsToSignIn` | `302 /sign-in`; session cookie deleted; `GET /` → `302 /sign-in` | RED |
| AC-011 | old cookie invalid | H | `Controllers.SignOutTests` | `AfterSignOut_ReplayedOldCookie_IsNotAuthenticated` | old cookie replayed → `302 /sign-in` | RED |
| AC-011 | GET sign-out | H | `Controllers.SignOutTests` | `GetSignOut_Returns404_SessionStillValid` | `404`; `GET /` → `200` | RED |
| AC-011 | sign-out without token | H | `Controllers.SignOutTests` | `SignOutWithoutToken_Returns400_SessionStays` | `400`, `Error.PageExpired`; `GET /` → `200` | RED |
| AC-011 | session cookie attributes | H | `Security.CookieAttributeTests` | `SessionCookie_IsHostPrefixedSecureHttpOnlyStrict` | `__Host-cp-session`; `Secure`; `HttpOnly`; `SameSite=Strict`; `Path=/`; no `Domain` | RED |
| AC-011 | antiforgery cookie attributes | H | `Security.CookieAttributeTests` | `AntiforgeryCookie_IsHostPrefixedSecureHttpOnlyStrict` | `__Host-cp-antiforgery`; same attributes | RED |
| AC-011 | every other cookie Secure | H | `Security.CookieAttributeTests` | `EveryCookieSetByTheStoryFlows_IsSecure` | every `Set-Cookie` over setup, home, sign-out, sign-in, error flows is `Secure`; both named cookies seen | RED |
| AC-011 | Data Protection keys persisted | H | `Security.DataProtectionTests` | `KeyRing_IsWrittenToConfiguredDirectory` | a `key-*.xml` appears in the configured directory | RED |
| AC-012 | setup success audit | H | `Controllers.SetupAuditTests` | `ValidSetup_WritesSucceededRowWithNewOwnerAsActorAndTarget` | `owner_first_run_setup`, `succeeded`, actor = target = new id; `request_id`; clock time | RED |
| AC-012 | wrong code audit | H | `Controllers.SetupAuditTests` | `WrongCode_WritesRefusedAnonymousWrongSetupCode` | actor anonymous, no id, no target, `wrong_setup_code` | RED |
| AC-012 | missing code audit | H | `Controllers.SetupAuditTests` | `MissingCodeWithValidFields_IsAuditedAsWrongSetupCode` | as above | RED |
| AC-012 | field failure not audited (OD-004) | H | `Controllers.SetupAuditTests` | `WrongCodeWithInvalidFields_IsNotAudited` | `400` with the field message and without the code message; 0 audit rows | RED |
| AC-012 | no audit after Owner exists | H | `Controllers.SetupAuditTests` | `WrongCodeAfterOwnerExists_IsNotAudited` | `409`; only the success row | RED |
| AC-012 | no secrets in rows | P | `Controllers.SetupAuditTests` | `SetupAuditRows_ContainNoCodeLoginOrPassword` | 2 rows; no JSON contains either code, the login or the password | RED |
| — (DB design) | migration content | P | `Persistence.MigrationTests` | `InitialMigration_CreatesOnlyOwnerAndAuditEvent` | tables {`__EFMigrationsHistory`, `audit_event`, `owner`}; one migration `*_InitialOwnerAndAudit`; trigger `trg_audit_event_immutable` | RED |
| — (DB design) | model matches migrations | P | `Persistence.MigrationTests` | `Model_HasNoPendingChangesAgainstMigrations` | migrations exist; `HasPendingModelChanges()` false | RED |
| — (DB design) | owner check constraints | P | `Persistence.OwnerSchemaTests` | `OwnerCheckConstraints_RejectViolatingRows` (theory ×3) | `23514` on `ck_owner_ui_language`, `ck_owner_access_failed_count`, `ck_owner_singleton` | RED |
| — (DB design) | owner columns | P | `Persistence.OwnerSchemaTests` | `OwnerColumns_MatchDesign_NoEmailOrPhone` | columns, types, lengths, nullability, defaults exactly per §3; `pk_owner` | RED |
| — (DB design) | owner unique indexes | P | `Persistence.OwnerSchemaTests` | `NormalizedUserNameAndSingleton_HaveUniqueIndexes` | `uq_owner_normalized_user_name`, `uq_owner_singleton` unique on their columns | RED |

### Changes against revision 1

- **Added:** `SignInTests.ReturnUrl_IsIgnored` (spec I-6), `SignOutTests.SignOutWithoutToken_Returns400_SessionStays`
  (openapi `/sign-out` `400`), `AuditEventSchemaTests.ConsistentRows_AreAccepted`,
  `AuditEventSchemaTests.AuditEventColumns_MatchDesign_NoPersonalDataColumn`,
  `MigrationTests.Model_HasNoPendingChangesAgainstMigrations`,
  `OwnerSchemaTests.NormalizedUserNameAndSingleton_HaveUniqueIndexes`.
- **Split:** `OwnerSchemaTests.OwnerConstraints_LengthsChecksAndUniqueness` into
  `OwnerCheckConstraints_RejectViolatingRows` and `OwnerColumns_MatchDesign_NoEmailOrPhone`
  (lengths are read from the catalogue instead of provoked by inserts).
- **Replaced:** `PageLanguageTests.HomePage_UsesOwnerAccountLanguage` by
  `SignedInOwner_SeesAccountLanguage` — the home page carries no contract key, so the
  account language is proven on the error page with the account set to `uk` and then `en`.
- **Removed:** `PageLanguageTests.RequestCulture_IsUkUa_ForAnonymous` — US-001 renders no
  date or number, so the format culture is not observable (test strategy §5).
- **Boundaries made strict:** lockout release asserted at 14:59 / 15:01 and idle expiry
  at 30 min 1 s, because the specification says "after 15 minutes" and "more than 30
  minutes" without fixing the equality instant.
- **Level:** translation tests run in a host (the localizer is resolved from DI), so
  they are H, not U.

## Coverage summary

| AC | Tests (methods) | Levels |
|---|---|---|
| AC-001 | 7 | H |
| AC-002 | 5 | H, P, U |
| AC-003 | 4 | H |
| AC-004 | 3 | V, H, P |
| AC-005 | 15 | H, V |
| AC-006 | 8 | H |
| AC-007 | 8 | H, U |
| AC-008 | 11 | H, P |
| AC-009 | 4 | H |
| AC-010 | 7 | S, H |
| AC-011 | 12 | S, H |
| AC-012 | 6 | H, P |
| DB design | 5 | P |

95 test methods, 158 test cases with theory data. Every Acceptance Criterion has at
least one test. No Acceptance Criterion is untestable.
