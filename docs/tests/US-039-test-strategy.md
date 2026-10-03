---
artifact_type: test_strategy
story: US-039
version: 1
status: DRAFT
created_at: 2026-10-03T09:20:00Z
updated_at: 2026-10-03T09:20:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-039-choose-ui-language.md
    version: null
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
supersedes: null
---

# US-039 Test Strategy — Choose UI language

## 1. Scope

One operation, `POST /account/language`, on both hosts; the header switcher; the
BR-026 read-only permission; the session re-issue; two installation date formats.
No schema change, so no schema tests (db-design §5). Matrix:
`docs/tests/US-039-ac-test-matrix.md`.

## 2. Test levels

| Level | Where | What it proves |
|---|---|---|
| Unit (Application, TC-1) | `Application/UseCases/ChooseUiLanguageUseCaseTests` | VR-001 validation in Application, own-row-only write, no stamp rotation, the BR-026 declaration at commit, the entity method |
| Integration, Application + PostgreSQL (TC-2, TC-5) | `Application/UseCases/UiLanguageReadOnlyTests`, `PermittedServiceWriteTests` (modified) | AC-003 in each BR-025 cause against the real commit backstop; nothing else opened; the registry entry |
| Integration over HTTP, installation | `Web/Pages/LanguageSwitcherTests`, `Web/Security/UiLanguageChoiceSecurityTests`, `Web/Localization/DateFormatTests`, `Web/Localization/UiLanguageTranslationTests` | AC-001 … AC-010, OD-007 |
| Integration over HTTP, Control Plane | `ControlPlane/Security/OwnerUiLanguageTests`, `ControlPlane/Localization/UiLanguageTranslationTests` | the same for the Owner (OD-004) |

## 3. Scenarios

**Positive:** Admin, Dean and Owner choose from several pages; the same page
returns in the new language; next pages follow; back to Ukrainian; the choice
survives a new sign-in and another browser (Dean, Admin via Google, Owner);
choosing in each read-only cause; a local return path with a query is kept.

**Negative:** anonymous POST (challenged); GET (404/405); POST without token
(400); forged `accountId` / `id` / `userId` fields (ignored); invalid codes;
foreign return paths; Dean forbidden from the Admin screen after a re-issue.

**Boundary:** codes differing only by case or whitespace (`EN`, `Uk`, ` en`),
numeric codes `0`/`1` (enum-binding trap, api-design §2.5), `uk-UA`, a 4096- and
5000-character code, a missing code; a return path of 2049 characters; the
session at 7 h 59 min and 8 h 01 min after the original sign-in with the choice
made at 7 h.

**Validation:** VR-001 in the use case and over HTTP on both hosts; VR-002 eight
foreign shapes (`//host`, absolute http/https, `/\`, `\\`, bare host,
`javascript:`, empty).

**Security:** TC-5 allowed/refused pairs on both hosts; endpoint enumeration —
authenticated, POST only, not antiforgery-exempt; SC-2 cookie attributes of the
re-issued cookie (Lax installation, Strict Control Plane, non-persistent); the
security stamp unchanged and a kept copy of the re-issued cookie dead after
sign-out; the rejected marker absent from the response body and the log files
(SC-10); the temporary-password gate (OD-007) still holding the Dean at the form.

**Persistence:** the stored `ui_language` of the chooser and of another account,
the stored security stamp, the Owner row — all read with raw SQL.

## 4. Fixtures

- `TestInfrastructure/UiLanguageTestData` — path, field names, translation keys
  `Layout.Language.Uk` / `.En` / `.Switcher`, self-names «УКР» / «ENG», and the
  page readers: `<html lang>`, the switcher form (`action="/account/language"`),
  the offered `language` inputs, the rendered `returnPath`.
- `TestInfrastructure/UiLanguageHostExtensions` — `ChooseLanguageAsync` (opens a
  page for its token, posts), `SignInDeanAsync`, `StoredLanguageAsync`,
  `SecurityStampAsync`.
- Existing: `DeanAccountWorld` (in-memory ports), `ReadOnlyModeHost`,
  `DeanAccountHostExtensions.StartSignedInAsync`, `ControlPlaneTestHost`,
  `ManualTimeProvider`/`TestTimeProvider`.

**Contract the implementation inherits from the fixtures** (agreed here so tests
and screens cannot drift): each offered language is its own POST form whose code
travels as `<input name="language" value="…">`, with `<input name="returnPath">`
holding the current path and query; the current language is marked with
`aria-current="true"`; the three translation keys above exist in both files of
each host.

## 5. Compile-only skeleton (OD-008, option 1)

`AppUser.ChooseUiLanguage(UiLanguage)` and `ChooseUiLanguageUseCase` exist with
bodies throwing `NotImplementedException`, not registered in DI. The
`PermittedServiceWrites` entry, the controllers, the layout partial, the
middleware change, the date corrections and the translations are all
IMPLEMENTATION's.

## 6. Excluded scenarios

- Control Plane date formats: already culture-based and covered by the existing
  `InstallationLastCheckTests` and `AllowedAdminListTests`; this Story changes
  nothing there (spec FR-009).
- A concurrency conflict between the choice and a sign-out in another tab
  (db-design §3): not retried by design; the outcome is the generic `500` path
  already covered by the error-page tests. Forcing the race deterministically
  would need a test seam the design does not provide.
- Propagation to other already-open sessions: out of scope (spec I-3, §10).
- Schema: unchanged; the existing migration-count tests keep it so.

## 7. Known limitations

- `GET /account/language` is asserted as "`404` or `405`" (api-design §2.6).
- The date assertions compute the expected string from .NET's culture data for
  `uk` and `en`; a separate fact proves the two formats differ for the dates used.
- Accessibility is asserted only as `aria-current="true"` being present.

## 8. Open Decisions affecting testing

OD-008 — raised here, resolved by the Owner on 2026-10-03 (option 1). None open.
