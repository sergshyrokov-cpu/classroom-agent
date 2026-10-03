---
artifact_type: security_review
story: US-039
version: 1
status: APPROVED
created_at: 2026-10-03T14:31:23Z
updated_at: 2026-10-03T14:31:23Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-039-choose-ui-language.md
    version: null
  - path: trebovaniya.md
    version: 79
  - path: docs/specifications/US-039-spec.md
    version: 1
  - path: docs/decisions/US-039-open-decisions.md
    version: 2
  - path: docs/designs/api/US-039-api-design.md
    version: 1
  - path: docs/designs/api/US-039-openapi.yaml
    version: 1
  - path: docs/designs/database/US-039-db-design.md
    version: 1
  - path: docs/designs/database/US-039-entity-model.md
    version: 1
  - path: docs/tests/US-039-test-strategy.md
    version: 1
  - path: docs/tests/US-039-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-039-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-039 Security Review — Choose UI language

## 1. Executive Summary

**Result: PASS.** One new authenticated POST endpoint per host (`/account/language`) that writes one column of the
session's own account and re-issues the session cookie. Principal controls verified in code and by passing tests:
declared policy on both hosts, global antiforgery with no new exemption, no account identifier read from the
request, ordinal `uk`/`en` validation in Application/Services, a re-issue that copies the sign-in time and security
stamp (absolute limit and sign-out invalidation intact), a validated local return path, a read-only registry entry
that opens only this commit. No Critical, Major or Minor findings; three Informational notes. Next: HUMAN_PR_APPROVAL.

## 2. Reviewed Artifacts

See front matter `inputs`. HUMAN_SPEC_APPROVAL recorded 2026-10-03T08:50:09Z. No input is SUPERSEDED.

## 3. Security-Relevant Scope

- Installation (public HTTPS): `POST /account/language` (Admin, Dean), header switcher on every signed-in page,
  `TemporaryPasswordMiddleware` allowed list, two views' date format.
- Control Plane (private network): `POST /account/language` (Owner), header switcher.
- Assets: session cookies (`__Host-ca-session`, `__Host-cp-session`), security stamps, `app_user.ui_language`,
  `owner.ui_language`. No student data, no credential, no key, no Google access touched.
- Boundaries: browser → host; controller → `ChooseUiLanguageUseCase` / `OwnerSessionService`; → PostgreSQL.

## 4. Environment and Tools

.NET SDK 10.0.401; Docker 29.8.0 (Testcontainers available). Commands and results:

- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors (implementation report §5, re-observed in this session).
- `dotnet test ClassroomAgent.sln --no-build` — 2545 total, 2545 passed, 0 skipped (observed in this session).
- `dotnet list ClassroomAgent.sln package --vulnerable` — no vulnerable packages in any of the 7 projects
  (nuget.org source).
- Static review of every changed file in the working tree.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role added; existing policies `AuthenticatedUser` / `OwnerPolicy` only (api-design §2.8). |
| SC-2 Authentication | PASS | Re-issue (`InstallationSession.ReissueWithLanguageAsync`, `OwnerSession.ReissueWithLanguageAsync`) uses the same scheme and `AuthenticationProperties { IsPersistent = false, AllowRefresh = true }` as the original sign-in; cookie attributes come from host cookie options, unchanged. `SignedInAt` and `SecurityStamp` claims copied, so `ValidatePrincipalAsync` still enforces the 8 h limit and stamp check. Tests: `TheReissuedSession_StillEndsEightHoursAfterTheOriginalSignIn`, `TheReissuedCookie_StillDiesAtSignOut_AndTheStampIsNotRotated`, `TheReissuedCookie_KeepsTheSc2Attributes` (both hosts). No `IClaimsTransformation` exists, so `context.User` holds only cookie claims — nothing foreign is persisted by the copy. |
| SC-4 Authorization | PASS | Installation action `[Authorize(Policy = AuthenticatedUser)]`; Control Plane class `[Authorize(Policy = OwnerPolicy)]`; neither `[AllowAnonymous]`. `GlobalAntiforgeryFilter` registered on both hosts (`Program.cs`); the only `[IgnoreAntiforgeryToken]` are the pre-existing service-channel controllers. POST only; GET 404/405 changes nothing. Switcher hidden on `IAllowAnonymous` endpoints. Tests: enumeration `TheAction_IsAuthenticated_PostOnly_AndNotExemptFromAntiforgery`, anonymous, no-token, GET (both hosts). |
| SC-5 Read-only | PASS | `ChooseUiLanguageUseCase` declares `SignInBookkeeping` only around its `SaveChangesAsync`; one registry entry; enum unchanged. `UiLanguageReadOnlyTests` (3 causes, through the real backstop; undeclared write after the choice still refused). No Google port in the use case. |
| SC-10 Hygiene | PASS | Refusal is the generic error page (`Error.PageExpired`), value never echoed; no log line written by the new code; `AnInvalidCode_IsRefused_NotStored_NotLogged` asserts the marker absent from logs (both hosts). |
| SC-11 Audit | PASS | No audit row by decision OD-005; `AuditAction` unchanged. |
| SC-3, SC-6, SC-7, SC-8, SC-9, SC-12, SC-13 | NOT_APPLICABLE | No AllowedAdmin, DB UI, key, Google, channel, Contracts or outbound change in this Story. |

## 6. Authentication and Authorization

| Operation | Allowed | Refused | Tests |
|---|---|---|---|
| installation `POST /account/language` | Admin, Dean (incl. temporary-password Dean, FR-012) | anonymous → 302 `/sign-in`; no token → 400; GET → 404/405 | `UiLanguageChoiceSecurityTests` |
| Control Plane `POST /account/language` | Owner | anonymous → 302 `/sign-in`; no token → 400; GET → 404/405 | `OwnerUiLanguageTests` |

Target account is always `InstallationSession.AccountId(User)` / `OwnerSession.OwnerId(User)`; a forged `accountId`
field is ignored (`AnAccountIdInTheRequest_IsIgnored`). SC-4's anonymous list is unchanged.

`TemporaryPasswordMiddleware` admits `/account/language` (OD-007). The re-issue keeps `PasswordIsTemporary`, so the
redirect is held at the forced form again; the action opens nothing else
(`ADeanWithATemporaryPassword_ChoosesOnTheForcedChangePage_AndStaysHeldThere`). `StartsWithSegments` also admits
sub-paths of `/account/language`; none is routed, so they reach the 404 catch-all — no exposure.

## 7. Credentials, Key and Google Access

No password, hash, key or Google call involved. The security stamp is deliberately not rotated (I-3).

## 8. Sensitive Data Exposure

- Views: the switcher renders the current path and query HTML-encoded in a hidden `returnPath` input (FR-006, I-4).
  The value comes from the requesting user's own URL and is served back only to them; Razor attribute encoding
  prevents markup injection (the `<script>` notice case passes with a raw-body check). See INFO-1.
- DTO isolation: the use case returns `UiLanguage?`, the service `string?`; no entity reaches a controller or view.
- Logs, audit, exports, telemetry: nothing new.

## 9. Input Validation

- `language`: bound as string, ordinal match to `uk`/`en` in `ChooseUiLanguageUseCase.LanguageOf` and
  `OwnerSessionService.ChooseLanguageAsync`; numbers, case variants, whitespace, oversized, empty → 400, nothing
  committed (`AnyOtherValue_IsRefused_AndNothingIsCommitted`, 15 values; HTTP 10 + 5 values).
- `returnPath`: 1…2048 chars and `Url.IsLocalUrl`, else `/` (8 + 4 foreign shapes, over-long). See INFO-2.
- Extra fields ignored. The 400-with-error-page shape is the approved design (API-design §2.4), not the API-6 body:
  no route is under `/api/v1`.

## 10. API Security

Exactly the one approved operation per host; no GET, no `/api/v1`, no response body on success (302). Error
responses: generic page, no internals.

## 11. Persistence and Configuration

No schema change, no migration (OD-003); existing check constraints `IN ('uk','en')` remain as a second layer.
`AppUser.ChooseUiLanguage` rejects undefined enum values and renews only `ConcurrencyStamp`. Control Plane renews
`ConcurrencyStamp` via `OwnerStamps.New()`. No configuration file changed.

## 12. Logging, Audit and Telemetry

No new log statements; no audit by OD-005; hook telemetry untouched.

## 13. Dependencies

No `.csproj` changed; no package or project reference added. Vulnerability scan: none found (§4).

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| allowed / refused per host (TC-5) | `UiLanguageChoiceSecurityTests`, `OwnerUiLanguageTests` |
| anonymous enumeration (TC-5) | existing anonymous-endpoint enumeration tests (green) + the new enumeration tests |
| read-only in Application (TC-5) | `UiLanguageReadOnlyTests`, `ChooseUiLanguageUseCaseTests` |
| session limits after re-issue | AC-009 tests, both hosts |
| open redirect | AC-010 tests, both hosts |
| no internals / no logged rejected value | AC-005 tests |

The three pre-existing tests adjusted at IMPLEMENTATION (Owner decision, implementation report §7) keep their
security intent: the query-reflection tests still assert the value is absent from everything outside the switcher,
and the notice test now also asserts no raw `<script>` anywhere in the body.

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| Change another user's language via a forged field | ignored | `AnAccountIdInTheRequest_IsIgnored` | PASS |
| Cross-site POST to switch a victim's language | refused (antiforgery; SameSite) | no-token tests | PASS |
| Extend a session past 8 h by repeated switching | still ends at 8 h | AC-009 tests | PASS |
| Replay a re-issued cookie after sign-out | rejected | stamp test | PASS |
| Open redirect via `returnPath` | lands on `/` | AC-010 tests | PASS |
| Temporary-password Dean escapes the forced form via `returnPath` | held at the form | OD-007 test | PASS |
| Use the choice as a write path in read-only mode beyond itself | refused | `AfterTheChoice_AnUndeclaredWriteInTheSameScope_IsStillRefused` | PASS |
| Crafted link with markup in the query reflected by the switcher | encoded | `<script>` case | PASS |

## 16. Repository Hygiene

Working tree holds only Story source, tests, the implementation report and workflow state. No secret-like file,
database file or `.xlsx`. Credential files were not opened.

## 17. Deviations

None from the approved security requirements. The Owner-approved test adjustments are recorded in the
implementation report and change no requirement.

## 18. Findings

- **INFO-1** (DATA_EXPOSURE, SC-10). `_LanguageSwitcher.cshtml` (both hosts) echoes the full query string into the
  page. Safe today (encoded, same user, no personal data in current query strings). If a future screen puts personal
  data in its query string, it will also appear in the page markup; that Story should note it. No correction.
- **INFO-2** (INPUT_VALIDATION). A self-crafted `returnPath` containing CR/LF passes `Url.IsLocalUrl` and makes
  Kestrel refuse the `Location` header (500) after the choice is stored. Header injection is not possible; only the
  attacker's own session is affected (antiforgery requires their token). No correction required.
- **INFO-3** (OTHER). The re-issue rebuilds claims as `new Claim(type, value)`, dropping `ValueType`/`Issuer`.
  Nothing reads those properties; the original sign-in sets none. No correction.

## 19. Positive Controls

Declared policies; global antiforgery without new exemptions; session-scoped target account; string-ordinal
validation in Application/Services; re-issue preserving sign-in time, stamp, role and temporary-password claim with
identical cookie properties; `Url.IsLocalUrl` + length cap on the return path; declared-and-registered BR-026 write;
generic refusal page with no echo; no new package; DB check constraints as a second layer.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

Code, configuration and test review plus the full automated suite; no penetration testing; no manual browser
session.

## 22. Verdict Rationale

The implementation report records a green build and suite, re-observed here. Every touched SC item is PASS with
evidence, all security-sensitive Acceptance Criteria (AC-003, AC-004, AC-005, AC-009, AC-010, FR-012) are covered
by passing tests, and no Critical, Major or Minor finding exists. Verdict PASS.
