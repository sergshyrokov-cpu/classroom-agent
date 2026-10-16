---
artifact_type: specification
story: US-052
version: 1
status: APPROVED
created_at: 2026-10-16T13:42:23Z
updated_at: 2026-10-16T13:51:39Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-052-forbidden-role-tests.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/decisions/US-052-open-decisions.md
    version: 1
supersedes: null
---

# US-052 Specification — Every Admin-only and Dean-only write has a forbidden-role test

## 1. Overview

This Story is **test-only**. The installation already refuses the wrong role on
every one-role endpoint, but four state-changing endpoints have no test proving
it (pre-pilot audit 2, finding F-1), and no single test holds the whole
`trebovaniya.md` §2 permission matrix (audit 2, note I-1). TC-5 treats an
endpoint with no authorization test as an endpoint with no policy.

The four endpoints and how they are protected today:

| Endpoint | Protection today | Allowed | Forbidden |
|---|---|---|---|
| `POST /settings/deans` (create a Dean account) | class-level `[Authorize(Policy = ManageDeanAccounts)]`, `DeanAccountsController.cs:23` | Admin | Dean |
| `POST /settings/deans/{deanId}/state` (disable / re-enable) | same class attribute | Admin | Dean |
| `POST /settings/deans/{deanId}/password` (reset password) | same class attribute | Admin | Dean |
| `POST /account/password` (own password change) | method-level `[Authorize(Policy = ChangeOwnPassword)]`, `DeanPasswordController.cs:70` | Dean | Admin |

The Story adds (a) HTTP tests that send each of these POSTs with a real session
of the forbidden role and a valid antiforgery token, and the same requests from
the allowed role; (b) one permission-matrix test over every endpoint of the
installation host. No file under `src/` changes.

## 2. Business Goal

A moved or deleted authorization attribute must fail the test suite instead of
silently opening an Admin action to a Dean, or a Dean action to an Admin. The
fallback policy (`InstallationSecurityServices.cs:167`, any authenticated user)
would otherwise admit any signed-in user to an endpoint that lost its attribute,
and no existing test would notice. Creating or resetting a Dean account is the
most sensitive write a Dean must not reach.

## 3. Business Flow

None changes. The tests prove the existing flow of `trebovaniya.md` §2
("Матрица прав") and SC-4:

1. A signed-in user posts a form with a valid antiforgery token.
2. Authorization evaluates the endpoint's policy before the action runs.
3. A user whose role the policy does not admit gets `403` and the host's
   translated error page (SC-4 v66: `AccessDenied` answers `403`, not a
   redirect); the action, its use case and its audit write never run.
4. A user whose role the policy admits reaches the action, which does its work
   and answers with Post-Redirect-Get (`302`).

## 4. Functional Requirements

### Forbidden-role HTTP tests (F-1)

- **FR-001** A test signs in a **Dean** over HTTP the way a real Dean signs in —
  password sign-in, then the forced temporary-password change completed — so
  the session is a full Dean session and not one still restricted to the
  temporary-password policy. Reuse: `DeanSessionBoundaryTests.SignInAsDeanAsync`
  or `DeanAccountHostExtensions.SignInAsDeanAsync` + `CompleteForcedChangeAsync`.
- **FR-002** With that session the test sends each of the three Dean-account
  POSTs with a **valid antiforgery token** taken from a page the Dean may open,
  and a complete, valid form:
  - `POST /settings/deans` with a fresh valid email and a temporary password that
    satisfies the password policy;
  - `POST /settings/deans/{deanId}/state` for a **second, existing Dean** with a
    valid desired state (disable an active account);
  - `POST /settings/deans/{deanId}/password` for that second Dean with a valid
    temporary password.
  The token is valid so that the refusal under test is the authorization one,
  not the antiforgery one (`ErasureAuthorizationTests` pattern).
- **FR-003** Each of the three answers is `403` with the translated "not
  permitted" error page (SC-4, TC-5: not a redirect, not `404`, not `400`).
- **FR-004** After the three refused requests the database shows no effect:
  - no account was created for the posted email;
  - the second Dean's enabled/disabled state, password hash, failed-attempt
    count, lockout end and security stamp equal their values before the
    requests;
  - the `AuditEvent` table has no row written by these requests (the row count
    before and after is equal).
- **FR-005** A test signs in an **Admin** (Google sign-in through the
  substituted ports, `InstallationSignInExtensions.SignInWithGoogleAsync`) and
  posts a complete own-password form (current and new password fields filled
  with policy-valid values) with a valid antiforgery token to
  `POST /account/password`.
- **FR-006** That answer is `403` with the translated error page; the Admin
  account's password hash (none), security stamp and role are unchanged, and no
  audit row is written.
- **FR-007** The installation is in normal (not read-only) mode in these tests,
  so a refusal cannot come from the read-only guard (`409`) instead of
  authorization.

### Allowed-role counterparts (AC-004)

- **FR-008** The same four requests, built the same way, are sent by the allowed
  role — an Admin for the three Dean-account POSTs, a full Dean session for
  `POST /account/password` — and each:
  - answers `302` (Post-Redirect-Get, `DeanAccountsController.cs:51,119`,
    `DeanPasswordController.cs:95`);
  - takes effect in the database (the account exists; the second Dean is
    disabled; its password hash changed; the Dean's own password hash changed);
  - writes exactly the one audit row SC-11 requires for that action.
  This proves the refusals of FR-003/FR-006 are a role boundary and not a broken
  request, and that "no audit row" in FR-004/FR-006 is not vacuous.

### Permission-matrix test (I-1)

- **FR-009** One test class holds a table — the **matrix table** — with one row
  per endpoint of the installation host: HTTP method(s), route pattern, and the
  expected authorization, which is exactly one of:
  - **a §2 policy**: the set of policy names the endpoint declares (class and
    method attributes combined), each policy being one listed in FR-010;
  - **any signed-in user**: `AuthenticatedUser` or
    `CompleteTemporaryPasswordChange`, for the endpoints that serve every
    account and are not a §2 cell (FR-011);
  - **anonymous**: the SC-4 anonymous-list entry the endpoint belongs to.
- **FR-010** The same class holds a second table mapping every installation
  role policy to its §2 row and the roles that row allows (✔), and asserts
  that the registered policy admits exactly those roles:

  | Policy | §2 row | Roles |
  |---|---|---|
  | `ViewCourses` | Просмотр курсов и участников | Admin, Dean |
  | `ViewJournal` | Просмотр и экспорт журнала успеваемости | Admin, Dean |
  | `ViewMeetReports` | Просмотр и экспорт статистики Meet-занятий | Admin, Dean |
  | `ViewMeetCodes`, `LinkMeetCodes` | Привязка кода встречи Meet к курсу | Admin, Dean |
  | `ViewDatabaseStatistics` | Раздел «Управление БД» (статистика) | Admin, Dean |
  | `StartSynchronization` | Запуск синхронизации | Admin, Dean |
  | `UseReportTemplates` | Использование шаблонов отчётов | Admin, Dean |
  | `EditReportTemplates` | Создание и редактирование шаблонов отчётов | Admin, Dean |
  | `ConfigureWorkspaceConnection` | Настройка `WorkspaceConnection` | Admin |
  | `ViewConnectionInstruction` | Просмотр инструкции по подключению | Admin |
  | `RunAccessCheck` | «Проверить доступ» | Admin |
  | `ManageDeanAccounts` | Создание, отключение и включение, сброс пароля учётных записей Деканов | Admin |
  | `EraseParticipantData` | Удаление всех данных участника … и снятие исключения | Admin |
  | `ChangeOwnPassword` | Смена собственного пароля | Dean |
  | `ViewLegitimacyStatus` | Просмотр статуса легитимности инсталляции | Admin, Dean |

  The §2 row "Просмотр состояния синхронизации с диагностикой ошибок на странице
  подключения" has no policy of its own: it is part of the connection page under
  `ConfigureWorkspaceConnection` (audit 2, §3) and is covered by that row. A
  policy registered in the host but missing from this table, or a table row
  whose policy is not registered, fails the test.
- **FR-011** The "any signed-in user" endpoints are exactly those of today's
  host: sign-out (`SignOutController`, `trebovaniya.md` §2 BR-026 list, v79),
  the UI-language choice (`UiLanguageController`, v51, NFR-073), and the forced
  temporary-password change page and form (`DeanPasswordController.Forced` /
  `CompleteForced`, §2 v38/v64). Each row of the matrix table of this kind names
  the requirement that makes it available to every account.
- **FR-012** The test enumerates the host's endpoints with
  `HostEndpoint.All` (the `EndpointDataSource` the other authorization tests
  use) and fails when:
  - an endpoint is missing from the matrix table;
  - a matrix-table row has no endpoint;
  - an endpoint declares a policy set different from its row;
  - an endpoint declares no policy and is not anonymous (it would be served by
    the fallback policy alone — SC-4 "every endpoint declares its required role");
  - an anonymous endpoint is not on the SC-4 anonymous list (already proven by
    `PublicPortAuthorizationTests`; the matrix restates it so the table is
    complete).
  The fallback catch-all counts as the SC-4 "Error page" entry (TC-5). The OAuth
  callback `signin-google` has no routed endpoint (`PublicPortAuthorizationTests`)
  and is not a table row. Static files are served by the static-files
  middleware, not by an endpoint, and are not table rows.
- **FR-013** The failure message lists every mismatching endpoint (method,
  pattern, expected, actual), not only the first, so one run shows the whole
  drift.

### Scope boundary

- **FR-014** No file under `src/` changes. The Story adds test files under
  `tests/ClassroomAgent.Tests/` only (TC-6 layout: `Web/Security`), reusing the
  existing test infrastructure; a new helper is added to `TestInfrastructure`
  only where the existing ones do not fit.
- **FR-015** If a matrix-table row cannot be written to agree with both §2 and
  the code — an endpoint whose declared policy contradicts its §2 row — the test
  is **not** bent to the code and production is **not** fixed in this Story; the
  conflict is recorded as an Open Decision and the stage returns BLOCKED.

## 5. Acceptance Criteria

Carried from the Story unchanged in meaning.

- **AC-001** A signed-in Dean with a valid antiforgery token posting a complete,
  valid create form to `/settings/deans` gets `403` with the translated error
  page; no account is created and no audit row is written.
- **AC-002** A signed-in Dean posting a valid state change and a valid password
  reset for a second, existing Dean gets `403` with the translated error page for
  each; the second account's state, password hash, lockout and security stamp are
  unchanged; no audit row is written.
- **AC-003** A signed-in Admin with a valid antiforgery token posting a complete
  own-password form to `/account/password` gets `403` with the translated error
  page; the Admin account is unchanged; no audit row is written.
- **AC-004** The same requests sent by the allowed role — an Admin for the three
  Dean-account POSTs, a Dean for `/account/password` — succeed.
- **AC-005** The matrix test lists every installation endpoint with its declared
  authorization; each is in the table with the policy of its §2 row (or in the
  SC-4 anonymous list); an endpoint missing from the table, a table row with no
  endpoint, or a different policy fails the test.
- **AC-006** The existing suite stays green with the new tests, and no file under
  `src/` is changed.

## 6. Validation Rules

No input of the system changes. The test inputs must be valid so that no
validation refusal can stand in for the authorization refusal:

- **VR-001** The create form's email is a well-formed address in the school
  domain not used by any account; the temporary password satisfies the Owner's
  password policy and the §2 rules (not containing the login part before `@`
  when that part is at least 4 characters).
- **VR-002** The state form's desired state is a value the endpoint accepts
  (`DeanAccountsController.cs:142`); the reset form's temporary password
  satisfies VR-001's policy.
- **VR-003** The own-password form carries the current password and a new
  policy-valid password that differs from the current one; for the Dean (FR-008)
  the current password is the Dean's real one.
- **VR-004** Every POST carries a valid antiforgery token obtained in the same
  session (SC-4, API-7).

## 7. Security Requirements

The Story enforces no new rule; it proves existing ones:

- `trebovaniya.md` §2 "Матрица прав": the Dean-account row is ✔ Admin / ✘ Dean;
  "Смена собственного пароля" is "— (пароля нет)" for Admin, ✔ Dean.
- SC-4: every endpoint declares its policy; deny by default; anonymous access is
  a closed list; `403` is the translated error page, not a redirect.
- TC-5: one allowed-role and one forbidden-role test per protected endpoint; a
  forbidden role on a page gets `403` with the translated error page.
- SC-11: the four actions are audited when they succeed; a request refused by
  authorization never reaches the use case and writes no audit row (no audit
  category exists for an authorization refusal, `AuditRefusalCategory.cs`).
- TC-4: no live Google API — the Admin's Google sign-in runs through the
  substituted ports. TC-2: the tests run against PostgreSQL via Testcontainers.
- Synthetic fixtures only; no data from `classroom_cache.db` or exports.

## 8. Error Handling

No production error path changes. In the tests: a `400` (antiforgery), `409`
(read-only), `302` to sign-in (lost session) or `404` in place of the expected
`403` is a failure of that test, not an acceptable outcome.

## 9. Non-Functional Requirements

- The new tests follow the existing test infrastructure (`InstallationTestHost`,
  `PostgreSqlFixture`) and add no package (no new NuGet reference).
- The matrix test reads endpoint metadata only and needs no HTTP requests; it
  should run in the time of the existing endpoint-enumeration tests.

## 10. Out of Scope

- Any change to production code, policies or the §2 matrix.
- Audit 2 note I-2 (the journal export sits under `UseReportTemplates`, not
  `ViewJournal`): the matrix table records the endpoint with its actual policy
  `UseReportTemplates`; both rows are ✔/✔, so nobody gains or loses access.
  Moving it is a production change and not this Story's (see I-3).
- The Control Plane — one role (Owner); its endpoints are already enumerated by
  `InstallationAuthorizationTests` and its siblings.
- The open US-037 test items (audit 2 I-4, I-5).
- The allowed-/forbidden-role tests of endpoints other than the four of F-1:
  audit 2 §3 found them covered.

## 11. Open Decisions

None. The Story carries none, and `trebovaniya.md` §7 (v96) has no open item this
Story depends on. See `docs/decisions/US-052-open-decisions.md`. FR-015 says what
happens if the matrix test exposes a contradiction.

## 12. Interpretations

- **I-1** The matrix test has two tables: endpoint → policy (FR-009) and policy →
  §2 row → roles (FR-010). The Story's "the policy of its §2 row" needs both: the
  first catches an endpoint under the wrong policy, the second a policy that
  admits the wrong role.
- **I-2** Sign-out, the UI-language choice and the forced temporary-password
  change are not §2 cells; they are open to every signed-in account by other
  requirements and stand in the table as "any signed-in user" with that
  requirement named (FR-011). An endpoint relying on the fallback policy alone,
  with no declared policy, fails the test (FR-012), because SC-4 requires every
  endpoint to declare one.
- **I-3** The journal export stays under `UseReportTemplates` (audit 2 I-2): the
  table pins today's policy, so a later change is a visible, deliberate edit of
  the table.
- **I-4** "Dean" in AC-001/AC-002 is a full Dean session — the temporary password
  already changed — so the refusal comes from `ManageDeanAccounts` and not from
  the temporary-password restriction (Story Scope).
- **I-5** AC-004's "succeed" means `302` plus the effect in the database plus the
  action's one audit row (FR-008); the last makes the "no audit row" of AC-001…
  AC-003 a meaningful check.
- **I-6** Red phase: the forbidden- and allowed-role tests pass against today's
  code by design. The implementation report records each new forbidden-role test
  and the matrix test failing once against a temporarily removed or changed
  attribute (not committed), so no test is vacuous (Story Notes; memory rule on
  vacuous tests in the red phase).
- **I-7** Expected stages: API_DESIGN and DB_DESIGN NOT_APPLICABLE (no endpoint or
  schema change); IMPLEMENTATION adds no production code (Story Notes).

## 13. Traceability

| AC | FR / VR |
|---|---|
| AC-001 | FR-001, FR-002, FR-003, FR-004, FR-007, VR-001, VR-004 |
| AC-002 | FR-001, FR-002, FR-003, FR-004, FR-007, VR-002, VR-004 |
| AC-003 | FR-005, FR-006, FR-007, VR-003, VR-004 |
| AC-004 | FR-008, VR-001…VR-004 |
| AC-005 | FR-009, FR-010, FR-011, FR-012, FR-013, FR-015 |
| AC-006 | FR-014 |

## 14. Delegation

Code facts (policy definitions, controller attributes and routes, `HostEndpoint`
fields, the sign-in and POST helpers, the `403` rendering path, the absence of an
authorization-refusal audit category) were collected by a read-only `quick-look`
subagent and spot-checked here against `DeanPasswordController.cs`,
`SignOutController.cs`, `UiLanguageController.cs`, `Program.cs` and
`PublicPortAuthorizationTests.cs`. `trebovaniya.md` §2 and §7, SC-4, SC-11, TC-5
and audit 2 were read directly.
