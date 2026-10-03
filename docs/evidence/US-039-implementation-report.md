---
artifact_type: implementation_report
story: US-039
version: 1
status: DRAFT
created_at: 2026-10-03T14:29:03Z
updated_at: 2026-10-03T14:29:03Z
produced_by: dotnet-implementor
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
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-039 Implementation Report — Choose UI language (Admin, Dean and Owner)

## 1. Summary

Both hosts now let a signed-in user choose the UI language: a header switcher «УКР / ENG» (one POST form per
offered language, the current one marked `aria-current="true"`), `POST /account/language` storing the choice on
the session's own account and re-issuing the session cookie with only the language claim replaced (sign-in time,
security stamp, role and temporary-password claim copied), and a validated local return path. The installation
write is registered as BR-026 sign-in bookkeeping and works in read-only mode. Two installation screens now format
dates by the request culture.

Status: complete. Build 0/0, full suite 2545/2545 green, format check clean. No schema change, no migration
(OD-003), no new package, no configuration change.

Security-sensitive: yes — a new authenticated write endpoint on both hosts, a session re-issue, a growth of the
read-only registry and of the temporary-password gate's allowed list. All as the approved designs fix them.

## 2. Source Artifacts

See front matter `inputs` (paths and versions). HUMAN_SPEC_APPROVAL recorded 2026-10-03T08:50:09Z.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (class) | Status |
|---|---|---|---|
| AC-001 | `_LanguageSwitcher.cshtml` (both hosts) rendered from `_Layout`; `UiLanguageController.Choose` (both hosts); `InstallationSession.ReissueWithLanguageAsync`, `OwnerSession.ReissueWithLanguageAsync` | `LanguageSwitcherTests`, `OwnerUiLanguageTests`, `ChooseUiLanguageUseCaseTests` | PASS |
| AC-002 | `ChooseUiLanguageUseCase.ExecuteAsync`, `AppUser.ChooseUiLanguage`; `OwnerSessionService.ChooseLanguageAsync`; account id taken from the session only | `ChooseUiLanguageUseCaseTests`, `LanguageSwitcherTests`, `OwnerUiLanguageTests` | PASS |
| AC-003 | `ServiceWriteScope.Declare(SignInBookkeeping)` around the commit; entry in `PermittedServiceWrites.Declarations` | `UiLanguageReadOnlyTests`, `PermittedServiceWriteTests`, `UiLanguageChoiceSecurityTests`, `ReadOnlyEnforcementTests` | PASS |
| AC-004 | `[HttpPost]` + `AuthenticatedUser` / `Owner` policy; global antiforgery (no exemption); no account field read | `UiLanguageChoiceSecurityTests`, `OwnerUiLanguageTests` | PASS |
| AC-005 | ordinal string match `uk`/`en` in the use case and the Control Plane service (api-design §2.5); refusal → `ErrorController.Page(400)`; value never logged or echoed | `ChooseUiLanguageUseCaseTests`, `UiLanguageChoiceSecurityTests`, `OwnerUiLanguageTests` | PASS |
| AC-006 | switcher rendered only when authenticated **and** the endpoint has no `IAllowAnonymous` (I-5) | `LanguageSwitcherTests`, `OwnerUiLanguageTests` | PASS |
| AC-007 | `Home/Index.cshtml`, `DeanAccounts/Index.cshtml` (see §7 inventory) | `DateFormatTests`; existing Control Plane date tests | PASS |
| AC-008 | `Layout.Language.Uk/.En/.Switcher` in both `.resx` of both hosts | `UiLanguageTranslationTests` (both hosts), `LanguageSwitcherTests` | PASS |
| AC-009 | re-issue copies every claim but the language, `IsPersistent = false`, cookie options unchanged | `UiLanguageChoiceSecurityTests`, `OwnerUiLanguageTests`, `ChooseUiLanguageUseCaseTests` | PASS |
| AC-010 | `LocalReturnPath`: length 1…2048 and `Url.IsLocalUrl`, else `/` | `UiLanguageChoiceSecurityTests`, `OwnerUiLanguageTests` | PASS |
| OD-007 / FR-012 | `TemporaryPasswordMiddleware` admits `SignInRoutes.Language`; claim kept by the re-issue | `UiLanguageChoiceSecurityTests` | PASS |

## 4. Change Set

Production:

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Domain/Entities/AppUser.cs` | `ChooseUiLanguage` body | entity-model §1, FR-004, AC-009 |
| `src/ClassroomAgent.Application/UseCases/ChooseUiLanguageUseCase.cs` | body: VR-001, load by session id, declared commit | FR-004, FR-007, VR-001 |
| `src/ClassroomAgent.Application/UseCases/PermittedServiceWrites.cs` | seventh entry | FR-007, AC-003 |
| `src/ClassroomAgent.Application/Localization/SharedResource.{uk,en}.resx` | 3 keys | FR-010, AC-008 |
| `src/ClassroomAgent.Web/Controllers/UiLanguageController.cs` (new) | `POST /account/language` | openapi, FR-003, FR-006 |
| `src/ClassroomAgent.Web/Security/InstallationSession.cs` | `ReissueWithLanguageAsync` | FR-005, api-design §2.3 |
| `src/ClassroomAgent.Web/Security/SignInRoutes.cs` | `Language` route constant | openapi path |
| `src/ClassroomAgent.Web/Security/TemporaryPasswordMiddleware.cs` | admits the language action | FR-012, api-design §2.7 |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | DI registration of the use case | supporting (DI) |
| `src/ClassroomAgent.Web/Views/Shared/_LanguageSwitcher.cshtml` (new) | switcher partial | FR-002 |
| `src/ClassroomAgent.Web/Views/Shared/_Layout.cshtml` | header with the switcher | FR-002 |
| `src/ClassroomAgent.Web/Views/Home/Index.cshtml` | culture short date + `HH:mm UTC` | FR-009, AC-007 |
| `src/ClassroomAgent.Web/Views/DeanAccounts/Index.cshtml` | culture short date | FR-009, AC-007 |
| `src/ClassroomAgent.Web/wwwroot/css/site.css` | switcher layout | FR-002, NFR (responsive) |
| `src/ClassroomAgent.ControlPlane/Controllers/UiLanguageController.cs` (new) | `POST /account/language` | openapi, FR-003, FR-006 |
| `src/ClassroomAgent.ControlPlane/Services/OwnerSessionService.cs` | `ChooseLanguageAsync` | FR-004, VR-001, entity-model §1 |
| `src/ClassroomAgent.ControlPlane/Security/OwnerSession.cs` | `ReissueWithLanguageAsync` | FR-005, api-design §2.3 |
| `src/ClassroomAgent.ControlPlane/Localization/SharedResource.{uk,en}.resx` | 3 keys | FR-010, AC-008 |
| `src/ClassroomAgent.ControlPlane/Views/Shared/_LanguageSwitcher.cshtml` (new) | switcher partial | FR-002 |
| `src/ClassroomAgent.ControlPlane/Views/Shared/_Layout.cshtml` | header with the switcher | FR-002 |
| `src/ClassroomAgent.ControlPlane/wwwroot/css/site.css` | switcher layout | FR-002 |

Tests changed at this stage (Owner decision, see §7):

| File | Change | Trace |
|---|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/UiLanguageTestData.cs` | `WithoutSwitcher(html)` helper | FR-006, I-4 |
| `tests/ClassroomAgent.Tests/Web/UseCases/WorkspaceConnectionReadOnlyTests.cs` | registry count 6 → 7 | FR-007 |
| `tests/ClassroomAgent.Tests/Web/Security/ConnectionInstructionAuthorizationTests.cs` | `AQueryStringIsIgnored` checks the page without the switcher | FR-006, I-4 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationStatusUnchangedTests.cs` | notice check without the switcher, plus a raw `<script>` check on the whole body | FR-006, I-4 |

The other US-039 test files were written at TEST_WRITING and are unchanged here. No secret, database file,
`.xlsx` or IDE-local file is in the change set.

## 5. Validation Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --no-build --filter` (the 9 US-039 / registry classes) | 0 | 130 total, 130 passed |
| `dotnet test ClassroomAgent.sln --no-build` (before the test corrections) | 1 | 2545 total, 2539 passed, 6 failed — see §7 |
| `dotnet test ClassroomAgent.sln --no-build --filter` (the 3 corrected classes) | 0 | 51 total, 51 passed |
| `dotnet test ClassroomAgent.sln --no-build` | 0 | 2545 total, 2545 passed, 0 failed, 0 skipped |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no diagnostics |

Integration tests ran against PostgreSQL via Testcontainers (Docker 29.8.0). No Google API was called.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

**Six existing tests failed once the switcher existed; both causes decided by the Owner on 2026-10-03.**

1. `WorkspaceConnectionReadOnlyTests.NoNewUseCase_IsRegisteredAsAPermittedServiceWrite` (US-009) pinned the
   registry size at 6; FR-007 makes it 7. TEST_WRITING updated `PermittedServiceWriteTests` but missed this
   count. Owner decision: change 6 → 7 as US-012 did; the test's own point (no US-009 use case exempt) is kept.
2. `ConnectionInstructionAuthorizationTests.AQueryStringIsIgnored` (US-010) and four cases of
   `InstallationStatusUnchangedTests.Detail_NoticeNotMatchingOrUnknown_IsNotShown_NotAnError` (Control Plane)
   asserted a query value appears nowhere in the HTML. The switcher carries the page's path **and query** as its
   return path (FR-006, I-4, matrix test `TheSwitcher_CarriesThePathAndQuery`), HTML-encoded in a hidden input.
   Owner decision: the specification stands; these tests now assert on the page without the switcher block, and
   the notice test additionally asserts no raw `<script>` anywhere in the body. Razor encodes the value, and the
   return path is followed only after `Url.IsLocalUrl` (AC-010).

Both are TEST_WRITING gaps, recorded here; no production behaviour was bent to the tests.

**FR-009 inventory (AC-007)** — every view and translation placeholder was checked for dates, times and numbers:

| Screen | Host | Finding | Correction |
|---|---|---|---|
| Landing, last successful check (`Home/Index`) | installation | invariant `dd.MM.yyyy HH:mm` | culture short date + `HH:mm` + `UTC` |
| Dean list, last sign-in (`DeanAccounts/Index`) | installation | fixed `yyyy-MM-dd` | culture short date (`"d"`) |
| Installations list and card: created, last check, Admin added (`InstallationDisplay.UtcTime`) | Control Plane | already culture short date + `HH:mm UTC` | none |
| All other views (sign-in, setup, access check, workspace connection, connection instruction, password pages, allowed admins, status confirmation, error) | both | no date, time or number output (GUIDs in URLs are identifiers, not numbers) | none |
| Translation placeholders (`{0}`) | both | only `AllowedAdmin.Email.*` — a domain name, not a number | none |

Partial work from an earlier session was found uncommitted at the start of this stage; it was reviewed against the
Specification and designs and kept as is.

## 8. Open Decisions

No Open Decision was raised or touched at this stage. OD-001 … OD-008 are resolved
(`docs/decisions/US-039-open-decisions.md` v2). The two test corrections in §7 were decided by the Owner in the
conversation; they change no requirement or design.
