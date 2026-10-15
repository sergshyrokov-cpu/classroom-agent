---
artifact_type: cross_cutting_audit
scope: whole solution (installation host and Control Plane)
commit: 9377f8e
version: 1
status: DRAFT
created_at: 2026-10-15
produced_by: pre-pilot audit 2 (pilot plan stage 0c), fresh context
critical_findings: 0
major_findings: 0
minor_findings: 2
informational_findings: 5
runtime_checks: REUSED
---

# Pre-pilot audit 2 — cross-cutting rules, requirement → code → test

A standalone review, not a Story review. Audit 1
(`2026-10-pre-pilot-security-audit.md`) asked "is the solution secure against
SC-1 … SC-14". This audit asks a narrower question for four rules that cut
across every Story: does each requirement have the code that implements it,
and a test that would fail if that code broke? Nothing was changed during the
audit. A finding that needs a code change becomes a Story the Owner authors.

The four rules:

1. the permission matrix, `trebovaniya.md` §2 «Матрица прав»;
2. the closed list of writes permitted in read-only mode, §2 / BR-026;
3. the audit list, §5 «Аудит действий» / SC-11;
4. personal-data handling, §5 «Приватность данных», «Хранение и удаление
   персональных данных» / BR-070 … BR-078.

## 1. Executive summary

No Critical or Major finding. All four rules are implemented as written, on
both hosts. The matrix in code (`InstallationPolicies`, its registrations in
`InstallationSecurityServices.cs:38-163`) has exactly one policy per §2 row and
the roles of each cell; the BR-026 registry (`PermittedServiceWrites.cs`) has
exactly the members of the closed list and an architecture test that fails on
any unregistered, unguarded write path; every action of the SC-11 list has an
audit factory, a caller and a test on both hosts; every retention rule of §5 has
a delete statement and a test.

Two Minor findings: four state-changing endpoints have no forbidden-role test,
which TC-5 counts as "no policy" (F-1), and the requirements do not say what
happens when one email is both a Dean account and an `AllowedAdmin` — the code
then lets the Dean in through Google, past the password rules (F-2, audit 1 I-3
traced to §2; needs the Owner's decision).

## 2. Method and evidence

The code is unchanged since audit 1: `git diff 71c8c4b 9377f8e -- src tests` is
empty (the commits in between are requirements, Stories and the audit 1
report). The build, test and dependency results of audit 1 §4 therefore stand
for this commit — 0 warnings, 5917/5917 tests passed — and were not re-run.

Read by the reviewing model: `trebovaniya.md` §2 and §5, BR-026, BR-070 …
BR-078, SC-5, SC-11, TC-5; every controller's authorization attributes; the
policy registrations; `TemporaryPasswordMiddleware`; the BR-026 registry, the
guard, the commit backstop, `ServiceWriteScope` and every declaration site; every
`SaveChangesAsync`, `ExecuteDelete`, `ExecuteSql*` call in `src/`; the audit
enums and factories of both hosts and their callers; `RunRetentionPurgeUseCase`,
`RetentionPurgeStore`, `ParticipantErasureStore`, `EraseParticipantUseCase`,
`CompleteGoogleSignInUseCase`, `CreateDeanAccountUseCase`; the personal-data
fields of every installation entity. Tests were located by text search over
`tests/` and read where the search alone could not show what they prove.

## 3. Rule 1 — permission matrix (§2)

| §2 row | Policy | Endpoints | Allowed-role test | Forbidden-role test |
|---|---|---|---|---|
| Просмотр курсов и участников | `ViewCourses` | courses, course card, teachers | `CourseList…`, `CourseCard…`, `TeacherListAuthorizationTests` | anonymous; Dean on temporary password |
| Просмотр и экспорт журнала | `ViewJournal`; export under `UseReportTemplates` (I-2) | journal page; `POST /api/v1/exports/journal-xlsx` | `JournalAuthorizationTests`, `GroupLessonsBlankAuthorizationTests` | anonymous 401; restricted Dean 403 |
| Просмотр и экспорт статистики Meet | `ViewMeetReports` | Meet reports, meeting page, two exports | `MeetReports…`, `MeetExportAuthorizationTests` | same |
| Привязка кода встречи Meet | `ViewMeetCodes`, `LinkMeetCodes` | list, course choice, link, confirm, not-a-course | `MeetCodesAuthorizationTests` | same |
| Управление БД (статистика) | `ViewDatabaseStatistics` | `/database` | `DatabaseStatisticsAuthorizationTests` | same |
| Запуск синхронизации | `StartSynchronization` | two presses | `SynchronizationRequestTests`, `DatabaseStatistics…` | same |
| Использование / редактирование шаблонов | `UseReportTemplates`, `EditReportTemplates` | list, report page, 4 forms, 3 saves | `ReportTemplateAuthorizationTests`, `ReportNameSource…` | same |
| Настройка `WorkspaceConnection` | `ConfigureWorkspaceConnection` (Admin) | GET + POST | `WorkspaceConnectionAuthorizationTests` | Dean 403 over HTTP, GET and POST (`DeanSessionBoundaryTests`) |
| Просмотр инструкции | `ViewConnectionInstruction` (Admin) | GET | `ConnectionInstruction…` | Dean 403 over HTTP |
| «Проверить доступ» | `RunAccessCheck` (Admin) | GET + POST | `AccessCheckAuthorizationTests` | Dean 403 over HTTP, GET and POST |
| Состояние синхронизации с диагностикой *(v80)* | on the connection page (Admin) | — | `LastSynchronizationBlockTests` | `ADean_IsRefusedAndSeesNothingOfTheBlock`; the Dean's `/database` shows "ask the Admin" only, no cause (`Views/Database/Index.cshtml:62-69`) |
| Учётные записи Деканов | `ManageDeanAccounts` (Admin) | list GET; create, state, reset POST | `AnAdmin_ReachesTheScreen` | **GET only** — F-1 |
| Удаление данных участника и снятие исключения | `EraseParticipantData` (Admin) | 5 endpoints | `ErasureAuthorizationTests` | `ADean_IsForbidden_EveryOperation` |
| Смена собственного пароля | `ChangeOwnPassword` (Dean) | GET + POST | `TheOwnPasswordPage_IsServedToADean` | Admin 403 **on GET only** — F-1 |
| Статус легитимности | `ViewLegitimacyStatus` | landing | `LandingPageTests` | anonymous redirect (`PublicPortAuthorizationTests`) |

For the rows both roles hold, v1 has no third role, so the "forbidden role" is
the anonymous visitor and the Dean still on a temporary password; every such
endpoint has both. No policy grants a Dean an Admin cell or the reverse. The
fallback policy closes any endpoint that forgets an attribute (audit 1, SC-4).

## 4. Rule 2 — read-only writes (BR-026)

| BR-026 item | Declared by | Test that it still works in read-only mode |
|---|---|---|
| audit rows | `AuditEvent` declarations on every refusal audit and on the three exports | `…_TheRefusalWritesExactlyOneAuditRow`, `InReadOnlyMode_TheAuditRowIsWritten`, export read-only tests |
| failed attempts and lockout, last sign-in time | `SignInDeanUseCase.cs:133` | `DeanSignInPageTests` in read-only causes; `TheSequence_NeverConsultsTheReadOnlyGuard` |
| creating an approved Admin's `AppUser` | `CompleteGoogleSignInUseCase.cs:175` | `InReadOnlyMode_TheAccountIsCreatedAndStamped` |
| a Dean changing their own password | `ChangeOwnPasswordUseCase.cs:51`, `CompleteTemporaryPasswordChangeUseCase.cs:49` | `BothChanges_WorkInReadOnlyMode` |
| UI language | `ChooseUiLanguageUseCase.cs:31` | `UiLanguageReadOnlyTests` |
| security stamp at sign-out | `AccountSessionService.cs:41` | `InReadOnlyMode_SigningOutEndsTheSession` |
| legitimacy-check state | `CheckLegitimacyUseCase.cs:87` | `TheLegitimacyCheckWrite_RunsInReadOnlyMode` |
| retention purge and its audit row | `RunRetentionPurgeUseCase.cs:38` | `RetentionPurgeReadOnlyTests` |

Every write the matrix names as blocked has a guard and a refusal test in the
Application layer: synchronization (`SynchronizationRefusalTests`), templates
(`ReportTemplateReadOnlyTests`), Meet linking (`MeetReadOnlyTests`), Dean
accounts (create, state, reset), connection settings
(`WorkspaceConnectionReadOnlyTests`), "check access" (`AccessCheckRefusalTests`),
erasure and lifting (`ErasureReadOnlyHttpTests`). The enum has exactly four
members (`TheList_HasExactlyTheFourMembersOfBr026`) and
`TheApplicationAssembly_HasNoUnprotectedWritePath` fails on an unregistered,
unguarded write path.

Writes that bypass `SaveChangesAsync` and so the commit backstop: the purge's
set-based deletes (declared, permitted), the erasure salt insert
(`ErasureSaltStore.cs:28`, only inside the guarded erasure) and the erasure
advisory lock (`ErasureFence.cs:29`, writes no row). None is a violation — see
I-4 for the test that should keep it so.

## 5. Rule 3 — audit (SC-11)

Installation: each action of the SC-11 list maps to an `AuditAction` value
(`Domain/Enums/AuditAction.cs`), a factory in `AuditEvent.cs`, a caller, and a
test asserting the row: sign-in and refusal for Admin and Dean, with the refusal
categories unknown login, wrong password, disabled, locked out, not in
`AllowedAdmin`, Control Plane unavailable; Dean create, disable, re-enable,
reset; own password change (both the forced and the voluntary path);
`WorkspaceConnection` save; "check access"; manual synchronization (both
presses share one use case); all six Meet link changes, including the automatic
one with actor `system` (`LinkMeetCodesStep`); erasure and lifting; template
create, change, delete; every export — the journal, the group-lessons blank form
(through `ExportJournalCommand`), Meet reports and one Meet meeting; the purge
run. Only two controllers return a file, and both go through an audited command
that commits the row before the file is returned (`ExportJournalCommand.cs:133-150`).

Control Plane: all ten actions (`ControlPlane/Persistence/AuditAction.cs`) have
callers and tests asserting their stored action names, including the anonymous
first-run refusal (`SetupAuditTests`).

Immutability and the absence of personal data were verified by audit 1 (SC-11:
add-only repository, deletion only in `RetentionPurgeStore.cs:114`).

## 6. Rule 4 — personal data (§5, BR-070 … BR-078)

| Requirement | Code | Tests |
|---|---|---|
| N is required, 1 … 100, or the installation does not start | settings validation | `RetentionConfigurationTests` |
| a course goes N years after its last activity, any state; Meet start counts | `RetentionPurgeStore.cs:20-36`, `RunRetentionPurgeUseCase.cs:44`, `RetentionRule` | `RetentionPurgeTests`, `RetentionRuleTests`, `RetentionPurgeMeetLinkTests` |
| a new course already older than N is not imported | synchronization | `CourseImportRefusalTests` |
| a leaver's membership, submissions and Meet connections go by "last seen" | `RetentionPurgeStore.cs:70-110` | `RetentionPurgeTests`, `RetentionPurgeMeetTests` |
| every Meet meeting goes by its own date | `RetentionPurgeStore.cs:134-161` | `RetentionPurgeMeetTests` |
| a "not a course" mark goes when its code has no meeting | `RetentionPurgeStore.cs:167-170` | `RetentionPurgeMeetLinkTests` |
| a participant goes when no membership references them | `RetentionPurgeStore.cs:112-115` | `RetentionPurgeTests` |
| `AppUser` goes N years after last sign-in or creation | `RetentionPurgeStore.cs:117-120` | `RetentionPurgeTests`, `AdminReturnsAfterPurgeTests` |
| audit rows go N years after their own date | `RetentionPurgeStore.cs:122-123` | `RetentionPurgeAuditEventTests` |
| the purge runs daily, in read-only mode too, one audit row with counts | `RunRetentionPurgeUseCase.cs:150-163` | `RetentionPurgeScheduleTests`, `RetentionPurgeReadOnlyTests` |
| synchronization never deletes a person who left Google (BR-074) | no delete in `RunSynchronizationUseCase` or its repositories | — (absence verified by search) |
| erasure on request: everything of one participant in one transaction, mentions cleared, exclusion with Google id and salted fingerprint only (BR-076) | `EraseParticipantUseCase.cs:70-100`, `ParticipantErasureStore.cs:15-75` | `EraseParticipantUseCase…`, `ParticipantErasureTests`, `ErasureInvariantTests`, `ErasureExclusionSyncTests`, `ErasureExclusionMeetTests` |
| access to personal data only for Admin and Dean (BR-070) | rule 1 above | rule 1 above |
| no personal data in logs or error bodies (BR-071) | audit 1 SC-10 | audit 1 SC-10 |

The synchronization failure stored for the Admin's diagnosis is the name of a
`SyncDiagnosis` value, never an exception message (`SyncState.cs:39,121`), so no
Google error text — which can contain an address — is kept in the database.

## 7. Findings

### F-1 (Minor, TEST, TC-5) — four state-changing endpoints have no forbidden-role test

- **Evidence:** `POST /settings/deans`, `POST /settings/deans/{id}/state` and
  `POST /settings/deans/{id}/password` are refused to a Dean only by the
  class-level `[Authorize(Policy = ManageDeanAccounts)]`
  (`DeanAccountsController.cs:23`); `POST /account/password` is refused to an
  Admin by its own method attribute (`DeanPasswordController.cs:70`). The policy
  objects are tested (`DeanAccountsAuthorizationTests`), but no test sends these
  POSTs as the forbidden role, and none asserts that these endpoints carry the
  policy — `DeanSessionBoundaryTests` and `TheOwnPasswordPage_IsForbiddenToAnAdmin`
  cover the GETs only. Other Admin-only screens (connection, access check,
  erasure) are tested on every endpoint.
- **Effect:** the code is correct today. TC-5: "an endpoint with no
  authorization test is treated as an endpoint with no policy" — if the
  attribute moved from the class to the GET action, or the POST lost its own,
  the fallback policy would admit any signed-in user and no test would fail.
  Creating or resetting a Dean account is the most sensitive write a Dean must
  not reach.
- **Correction:** a test-only Story: a real Dean session POSTing to the three
  Dean-account endpoints and an Admin POSTing to `/account/password`, each
  asserted `403` with nothing changed. The same Story could add the matrix test
  of I-1.
- **Resolution (2026-10-15):** Story US-052 (test-only), which also covers I-1.

### F-2 (Minor, REQUIREMENTS GAP, §2) — one email as both a Dean account and an `AllowedAdmin`

- **Evidence:** §2 «Модель авторизации» and «Сценарий входа» give Admin
  Google sign-in and Dean password sign-in and say nothing about one person
  holding both. `CreateDeanAccountUseCase.cs:72-78` refuses an address that
  already has an `AppUser`, but cannot see `AllowedAdmin`, which lives only in
  the Control Plane. `CompleteGoogleSignInUseCase.cs:51-71` then admits any
  existing, enabled account whose email the Control Plane approves, without
  checking its role, and writes `AdminSignInSucceeded` for it.
- **Effect:** if the Owner approves as Admin an email the school already uses
  for a Dean (or an Admin later creates a Dean with an approved Admin's email
  before that Admin first signs in), the person signs in through Google **as a
  Dean** — skipping the Dean password, the forced change of a temporary
  password and the lockout, and recorded as an Admin sign-in. They do not gain
  Admin rights. This is audit 1 I-3, now traced to the requirement it lacks.
- **Correction:** the Owner decides; the code must not invent the rule. Options:
  (a) **(recommended)** Google sign-in refuses an existing account that is not an
  Admin, with its own refusal category, and the school uses two different
  addresses — one role per account is what §2 already assumes;
  (b) the account is converted to Admin at its Google sign-in;
  (c) leave it and record the Owner's procedure "never approve a Dean's email".
  (a) and (b) need a new `trebovaniya.md` version, then a small Story.
- **Resolution (2026-10-15):** the Owner chose (a). The rule went into
  `trebovaniya.md` v96 (§2, §5, §9), SC-3, SC-11 and BR-011; Story US-051.

### Informational

- **I-1 — the matrix has no single test.** Each Story pins its own endpoints'
  policies; nothing compares the whole route table with §2. An endpoint added
  later under a policy of the wrong row would pass every existing test if its
  own Story forgot the check. A test listing every endpoint with its expected
  policy, built from the §2 table, would make the matrix one reviewable artifact.
- **I-2 — the journal export sits under the templates policy.**
  `JournalExportController.cs:28` uses `UseReportTemplates`, not `ViewJournal`.
  Both rows are ✔/✔, so nobody gains or loses access; it matters only if the
  rows ever diverge.
- **I-3 — a write declaration covers the whole commit.** The backstop checks
  only that a declaration is open (`ReadOnlyModeUnitOfWork.cs:23-31`), not that
  the staged changes are of the declared kind. Every current `AuditEvent`
  declaration stages nothing but the audit row — the refusal audits run before
  any change is staged — so nothing leaks today. A future use case that stages
  a change and then writes a refusal row under the declaration would commit
  both in read-only mode.
- **I-4 — the confinement test still looks for `ExecuteDelete` only.** Two
  `ExecuteSql*` calls outside the purge store bypass the backstop
  (`ErasureSaltStore.cs:28`, `ErasureFence.cs:29`); both are legitimate, but the
  architecture test would not notice a third. Same as US-037 M-2/N-4, still open.
- **I-5 — US-037 follow-ups re-checked, still open.** M-1 (a purged account's
  cookie is refused — the stamp check implies it, no test proves it) and N-5
  have no test; `trebovaniya.md` §7 item 27 (a purged leaver re-imported from old
  submissions) is still in «Решить». None blocks the pilot: no pilot data will
  be N years old.

## 8. Positive controls verified

- One policy per §2 row, Admin-only rows refused to a real Dean session over
  HTTP, the Dean-only row refused to an Admin.
- The temporary-password session reaches only the forced change, sign-out,
  language and sign-in (`TemporaryPasswordMiddleware.cs:23-29`), and is tested
  on every both-roles screen.
- The BR-026 registry equals the closed list; an architecture test fails on an
  unregistered, unguarded write path; every blocked write has an
  Application-layer refusal test.
- Every SC-11 action on both hosts has a factory, a caller and a test; every
  file download is audited before it is returned.
- Every §5 retention rule has its delete and its test; erasure clears the
  organizer and owner mentions and keeps no readable name or address.

## 9. Limitations

- Test coverage was established by text search and reading, not by a coverage
  tool or by mutating the code; a test named for a rule was read only where its
  name left doubt.
- No running instance; the build and test results are audit 1's, valid because
  `src/` and `tests/` are unchanged since that commit.
