---
artifact_type: test_strategy
story: US-064
version: 1
status: DRAFT
created_at: 2026-10-29T07:20:30Z
updated_at: 2026-10-29T07:20:30Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-064-disabled-dean-session-refused-on-next-request.md
    version: null
  - path: docs/specifications/US-064-spec.md
    version: 1
  - path: docs/decisions/US-064-open-decisions.md
    version: 1
  - path: docs/designs/api/US-064-api-design.md
    version: 1
  - path: docs/designs/database/US-064-db-design.md
    version: 1
supersedes: null
---

# US-064 Test Strategy — A disabled Dean's open session is refused on the next request

## 1. Scope

End-to-end proof that disabling a Dean takes effect on the next request (BR-014, SC-2, `trebovaniya.md`
§2 v103): the Dean's cookies captured before the disabling are refused on a page and on the journal
export, re-enabling does not revive them, and another Dean is unaffected. API_DESIGN and DB_DESIGN are
`NOT_APPLICABLE`; no `openapi` or `entity_model` exists. The Story is expected to be test-only (spec
§1, FR-008): the tests are expected **green** against current code, and a red test would be a
production defect, not a red phase.

## 2. Test levels

One integration/security level only: `WebApplicationFactory` host over real PostgreSQL via
Testcontainers (TC-2), synthetic accounts, no Google call (TC-4). No unit test: the unit-level proof
(`AppUser.Disable` rotates the stamp, `AccountSessionService`) already exists, and the gap the Story
closes is the HTTP pipeline. One class: `ClassroomAgent.Tests.Web.Security.DisabledDeanSessionTests`.

## 3. Fixtures and setup

- `JournalHostExtensions.StartAsync(..., Actor.Admin, ...)` — installation in normal mode
  (`ReadOnlyModeHost.Cause.NotReadOnly`: licensed, not read-only, BR-026; spec FR-001) with an Admin
  signed in by Google (Control Plane channel substituted).
- `SeedJournalAsync` — one synthetic September course; the journal page with this course shows
  `SeededJournal.CourseName`, used as the "journal content" marker.
- Deans seeded with `InsertDeanAsync(passwordIsTemporary: false)` — past the first-login change
  (spec FR-006) — and signed in through the real `POST /sign-in` on their own client.
- Disabling/enabling only through `POST /settings/deans/{id}/state` by the Admin (`SetDeanStateAsync`);
  nothing changes `app_user` directly (FR-001).
- Replay: a separate `FormClient` with `ReplaceCookies(dean.Cookies)` captured before the disabling
  (FR-002, the `InstallationSignOutTests` model).

## 4. Scenarios

| # | Kind | Scenario |
|---|---|---|
| S-1 | positive (proof) | Each test first replays the same request with the captured cookies before disabling and gets success: journal `200` with the course name; export `200` with an `.xlsx` (FR-003). |
| S-2 | negative / security | After disabling, journal `GET` → `302` to `/sign-in`, no course name in the body (AC-001, FR-004). |
| S-3 | negative / security | After disabling, export with the same cookies, same antiforgery token, same body → `401`, API-6 body (`timestamp`, `status` 401, `error` "unauthorized", `message` = `Api.Error.SignInRequired`, `path`), JSON, no `Content-Disposition`; `journal_exported` audit count unchanged (AC-002, FR-005, VR-001). |
| S-4 | negative + positive | Disable → enable → old cookie still `302` to `/sign-in`; fresh password sign-in → journal `200` (AC-003, FR-006). |
| S-5 | isolation | Two Deans; disabling the first: first's cookie `302` to `/sign-in`, second's cookie `200` with content (AC-004, FR-007). |

Boundary / validation scenarios: none new — the Story adds no input (spec §6). VR-001 is covered by
S-3 reusing a body the export accepted. Persistence scenarios: none (no schema change; db-design N/A).
Audit: S-3 asserts no extra `journal_exported` event; disable/re-enable audit events are already
covered by US-012 tests and not re-asserted here.

## 5. Excluded scenarios

- A request already in progress at the moment of disabling — v103 lets it complete; no stable
  assertion (spec §10).
- Admin revoked in `AllowedAdmin` — known limitation, no test by OD-001 a).
- Allowed/forbidden-role tests of the state endpoint — unchanged and already exist (spec §7, TC-5).
- Translation keys / time-zone boundaries (TC-8) — the Story touches no text and no period logic.

## 6. Known limitations

- The tests prove that the stamp/disabled check runs on the requests made; they do not enumerate every
  page. The journal page and the export represent "pages" and "files" as the Story scopes them.
- A `SecurityStampValidator` with an interval reintroduced later would make S-2…S-5 fail, because the
  replay happens within the same second as the disabling — the regression the Story guards against.

## 7. Open Decisions affecting testing

None. OD-001 resolved a) (no test of the Admin-revocation limitation).
