---
artifact_type: specification
story: US-064
version: 1
status: APPROVED
created_at: 2026-10-29T06:26:20Z
updated_at: 2026-10-29T07:07:33Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-064-disabled-dean-session-refused-on-next-request.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/decisions/US-064-open-decisions.md
    version: 1
supersedes: null
---

# US-064 Specification — A disabled Dean's open session is refused on the next request

Delegation: two read-only lookups (quick-look) ran in parallel — one quoted `trebovaniya.md` §2
lines 684–687 and 737–740, the v103/v104 changelog, §7, BR-012, BR-014, SC-2, SC-3, API-5, API-10,
TC-2, TC-3, TC-5; the other collected the code facts in §1.1. The spec writer checked the facts
it relies on against the quoted lines.

## 1. Overview

`trebovaniya.md` §2 (v103, lines 737–740): «Отключение действует сразу: оно меняет метку
безопасности учётной записи, а сессионная cookie сверяется с ней при каждом запросе (v79), поэтому
Декан, который в этот момент работает, не может открыть следующую страницу и выгрузить ни одного
файла — он попадает на страницу входа. Запрос, который уже выполняется, завершается.» The behaviour
is built; no test replays the cookie of a disabled Dean over HTTP. This Story adds that end-to-end
proof. It is expected to change tests only; a failing test is a production defect against BR-014
and its fix is in scope (FR-007).

### 1.1 Code facts the Specification must not contradict

- `AppUser.Disable` sets `IsDisabled` and rotates `SecurityStamp` (`AppUser.cs:132-136`).
  `AppUser.ReEnable` clears `IsDisabled` and changes only `ConcurrencyStamp` — it does **not**
  rotate `SecurityStamp` again (`AppUser.cs:142-146`). The old cookie stays refused after
  re-enabling because it carries the stamp from before the disabling, which `Disable` already
  replaced.
- Every request runs `InstallationSession.ValidatePrincipalAsync` (wired as `OnValidatePrincipal`,
  `InstallationSecurityServices.cs:241`): it rejects the principal and signs the cookie out when
  `AccountSessionService.IsCurrentAsync` returns false — the account is disabled or the stamp
  differs (`AccountSessionService.cs:25-29`, `InstallationSession.cs:95-111`). No
  `SecurityStampValidator` and no `ValidationInterval` exists in `src`.
- An unauthenticated request is redirected to `/sign-in` (`SignInRoutes.SignInPage`), except under
  `/api/v1`, which answers `401` with the API-6 body: `timestamp`, `status: 401`,
  `error: "unauthorized"`, `message` (key `Api.Error.SignInRequired`), `path`
  (`InstallationSecurityServices.cs:212-224`, `ApiErrorResponse.cs`).
- The journal page is `GET /workspace/journal` (`JournalController`, policy `ViewJournal`). The
  journal export is `POST /api/v1/exports/journal-xlsx` (`JournalExportController`, policy
  `UseReportTemplates`), antiforgery-checked by the global filter; a successful export writes an
  audit event with action `journal_exported` (`ExportJournalCommand.cs:136`).
- Disabling and re-enabling go through `POST /settings/deans/{deanId}/state` with form field
  `desiredState` = `Disabled` | `Active` (`DeanAccountsController.SetState`, policy
  `ManageDeanAccounts`). Test helper: `SetDeanStateAsync`
  (`tests/.../TestInfrastructure/DeanAccountHostExtensions.cs:58-72`). The replay model is
  `InstallationSignOutTests.ThePreviousCookieReplayed_NoLongerAuthenticates`.

## 2. Business Goal

Disabling a Dean account is a real stop: from the next request the Dean opens no page and downloads
no file, with no wait for the session to expire (BR-014, SC-2). A regression — for example a
periodic stamp validation interval brought back, which SC-2 calls a finding — must fail the suite.

## 3. Business Flow

1. A Dean signs in with login and password and works in the journal.
2. An Admin opens the Dean-accounts page and disables that Dean.
3. The Dean's next request with the same session cookie is refused: a page sends them to sign-in,
   an export answers "sign-in required" and gives no file.
4. If the Admin enables the account again, the old session stays dead; the Dean signs in again
   with their password and works.
5. Other Deans' sessions are untouched throughout.

## 4. Functional Requirements

- **FR-001** All tests of this Story run through the real HTTP pipeline (`WebApplicationFactory`)
  against real PostgreSQL via Testcontainers (TC-2). The Dean signs in through the login/password
  sign-in, the Admin disables and re-enables through `POST /settings/deans/{deanId}/state`; no
  test changes `AppUser` or the database directly to disable or enable. The installation is in
  normal mode — licensed, not read-only (BR-026) — throughout every test (review m-1/m-2, Owner at
  HUMAN_SPEC_APPROVAL).
- **FR-002** The Dean's session cookies are captured after sign-in and **before** the disabling,
  and replayed from a separate client after the disabling (the `ReplaceCookies` model of
  `InstallationSignOutTests`).
- **FR-003 (proof the test can fail)** Each test asserts, with the captured cookies and before the
  disabling, that the same request it later replays succeeds: the journal page answers `200`; for
  the export, the identical request (same cookies, antiforgery token and body) answers `200` with
  an `.xlsx` file. A refusal after disabling is therefore caused by the disabling, not by a broken
  fixture (memory note "vacuous tests in the red phase").
- **FR-004 (page)** After the disabling, `GET /workspace/journal` with the captured cookies answers
  `302` with `Location` `/sign-in`, and the response body contains no journal content.
- **FR-005 (export)** After the disabling, `POST /api/v1/exports/journal-xlsx` with the captured
  cookies and the antiforgery token obtained before the disabling answers `401` with the API-6 body
  (`timestamp`, `status` 401, `error` `"unauthorized"`, `message`, `path`), content type JSON, no
  file; the number of `journal_exported` audit events is the same as before the replayed request.
- **FR-006 (re-enabling)** After the Admin disables and then enables the account again, the
  captured cookies are still refused on `GET /workspace/journal` (`302` to `/sign-in`). The Dean
  then signs in again with the same password and `GET /workspace/journal` answers `200`. The
  Dean used here has already completed the first-login password change (BR-014, v64), so the
  sign-in lands on the work area.
- **FR-007 (another Dean)** With two Deans signed in and their cookies captured, disabling one
  leaves the other's cookies working: `GET /workspace/journal` answers `200` for the other. In the
  same test the disabled Dean's captured cookies are refused on `GET /workspace/journal` (`302` to
  `/sign-in`), so the test cannot pass when the disabling did not happen (review m-1, Owner at
  HUMAN_SPEC_APPROVAL).
- **FR-008 (defect fix in scope)** If a test of this Story fails against the current code, the
  minimal fix in production code that makes BR-014 hold is in scope, and the implementation report
  states it as a defect fix with the failing test as evidence. No production change is made
  otherwise.

## 5. Acceptance Criteria

| AC | Story text (short) | Requirements |
|---|---|---|
| AC-001 | The old cookie of a disabled Dean opens no page | FR-001, FR-002, FR-003, FR-004 |
| AC-002 | The old cookie downloads no export | FR-001, FR-002, FR-003, FR-005 |
| AC-003 | Re-enabling does not revive the old cookie | FR-001, FR-002, FR-003, FR-006 |
| AC-004 | Another Dean is not affected | FR-001, FR-002, FR-003, FR-007 |

## 6. Validation Rules

No new input. Existing validation applies unchanged: `desiredState` accepts only `Disabled` or
`Active` (anything else `400`); the export body is validated as today. **VR-001** the replayed
export request carries a body the export accepted before the disabling (FR-003), so a `401` cannot
be a validation or antiforgery refusal in disguise.

## 7. Security Requirements

- Session validation on every request against the stored stamp and the disabled state (SC-2,
  security-conventions.md lines 123–127); no periodic validation interval.
- Disabling is Admin-only (`ManageDeanAccounts`); this Story adds no endpoint and changes no
  policy. The existing allowed/forbidden-role tests of the state endpoint (TC-5) stay as they are.
- The refusal of an API request is the `401` API-6 body and reveals nothing about the account
  (no "disabled" text in the API body; the disabled-account message exists only on the sign-in
  form, SC-2 lines 52–57, and is not changed here).
- Tests use synthetic accounts only; no Google API is called (TC-4).

## 8. Error Handling

| Situation | Response |
|---|---|
| Disabled Dean's cookie on a page | `302` → `/sign-in` (cookie signed out) |
| Disabled Dean's cookie on `/api/v1/...` | `401`, API-6 body, `error: "unauthorized"` |
| Re-enabled account, old cookie | same as disabled — refused |
| Request already in progress when disabled | completes (v103); not tested (§9) |

## 9. Non-Functional Requirements

- No new NuGet package, no migration, no new requirements version; API_DESIGN and DB_DESIGN are
  expected `NOT_APPLICABLE`.
- Tests follow TC-2 (Testcontainers) and TC-4 (no live Google).

## 10. Out of Scope

- A request already in progress at the moment of disabling — v103 lets it complete; a timing test
  has no stable assertion.
- Revoking an Admin in `AllowedAdmin` — a known limitation (§2 lines 684–687, BR-012, SC-3); no
  test pins it (OD-001 a).
- Any UI change or new message on the sign-in page.
- Login change of a Dean (v104, US-067), which also rotates the stamp — its own Story.

## 11. Open Decisions

- **OD-001** Pin the Admin-revocation limitation with a test — **resolved a) by the Owner
  2026-10-27**: no test. Impact: none on FR-001…FR-008.
- No new Open Decision. `trebovaniya.md` §7 holds no open item about sessions, cookies or Dean
  accounts (items 26, 27, 14, 28, 29).

Interpretations for confirmation at HUMAN_SPEC_APPROVAL:

1. FR-003 — the export proof makes one real export before the disabling (writes one audit event);
   FR-005 then compares the audit count before and after the replay, rather than requiring zero.
2. FR-004 — "no journal content" is checked as a `302` with no journal page body, the redirect
   being the whole response.
3. FR-006 — "signs in again with the password" uses a Dean past the first-login password change.

## 12. Traceability

| AC | FR | VR |
|---|---|---|
| AC-001 | FR-001, FR-002, FR-003, FR-004, FR-008 | — |
| AC-002 | FR-001, FR-002, FR-003, FR-005, FR-008 | VR-001 |
| AC-003 | FR-001, FR-002, FR-003, FR-006, FR-008 | — |
| AC-004 | FR-001, FR-002, FR-003, FR-007, FR-008 | — |
