---
artifact_type: specification
story: US-067
version: 1
status: APPROVED
created_at: 2026-10-29T08:09:29Z
updated_at: 2026-10-29T08:16:59Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-067-admin-changes-dean-login.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/decisions/US-067-open-decisions.md
    version: 1
supersedes: null
---

# US-067 Specification — An Admin changes a Dean's login after the work email was renamed in Google

Delegation: one read-only lookup (quick-look) collected the code facts in §1.1 (entity, the
three existing Dean-account use cases, controller, view, audit enum and CHECK constraint,
report-template author join, translation keys, the US-012 OpenAPI). The spec writer read
`trebovaniya.md` §2 lines 704–721, the §2 permission matrix line 764, §5 audit lines 1775–1790,
the v104 changelog, §7, and SC-2 itself, and checked the retention query
(`RetentionPurgeStore.cs:110`) directly.

## 1. Overview

`trebovaniya.md` §2 (v104, lines 713–720): «**Смена логина**: если рабочий email Декана
переименовали в Google (например, сменилась фамилия), Админ меняет логин его учётной записи на
новый адрес — по тем же правилам, что при создании: только адрес в домене школы, не занятый
другой учётной записью. Учётная запись, пароль, история и аудит остаются прежними; старым адресом
войти больше нельзя. Смена меняет метку безопасности, поэтому открытая сессия Декана перестаёт
работать на следующем запросе, как при отключении. Пароль при этом не меняется. Сама программа за
переименованием в Google не следит: логин Декана — запись программы, а не аккаунт Google.»

The §2 matrix (line 764) gives the action to the Admin only; §5 (line 1780) adds «смена логина» to
the audited Dean-account actions. SC-2 and SC-11 repeat both. This Story adds a fifth Dean-account
management operation next to create, disable, re-enable and reset password (US-012).

### 1.1 Code facts the Specification must not contradict

- `AppUser` (`src/ClassroomAgent.Domain/Entities/AppUser.cs`) has `Email` and `NormalizedEmail`,
  both lower-cased via `AppUser.Normalize` (`Trim().ToLowerInvariant()`, :203), and no method
  that changes them. `RotateSecurityStamp()` (:241) replaces `SecurityStamp` and
  `ConcurrencyStamp`. `Disable` (:132) sets `IsDisabled` and rotates. Other state: `PasswordHash`,
  `PasswordIsTemporary` (:64), `AccessFailedCount`, `LockoutEnd`, `UiLanguage`, `IsDisabled`,
  `LastSuccessfulSignInAt` (:58), `CreatedAt`, `UpdatedAt` (set by `TimestampInterceptor`).
- The retention clock of an account is `LastSuccessfulSignInAt ?? CreatedAt`
  (`RetentionPurgeStore.cs:110`); `UpdatedAt` is not part of it.
- `CreateDeanAccountUseCase.ExecuteAsync` (:29) evaluates, in order: read-only
  (`readOnlyMode.EnsureAllowedAsync`, refused audit `DeanAccountManagementRefused`, rethrow,
  :40-48); address format `IsWellFormedAddress` on the trimmed value (:51-55, :113-124 — length
  1..254, no whitespace, exactly one `@`, non-empty local part, domain part passes
  `WorkspaceDomainAttribute.IsDomain`); exact school domain read from `LegitimacyState`, never the
  form, subdomains refused (:58-63, :130-134); password policy; uniqueness pre-check by
  `users.FindByNormalizedEmailAsync` across every role (:73-78). Refusals are
  `DeanAccountRefusal` values `NotAnEmailAddress`, `OutsideSchoolDomain`, `EmailAlreadyUsed`
  (`DeanAccountRefusal.cs`), shown with keys `DeanAccounts.Refused.NotAnEmail`,
  `.OutsideSchoolDomain`, `.EmailAlreadyUsed` (`DeanAccountTextKeys.cs:72-79`).
- The unique index `uq_app_user_normalized_email` (`AppUserConfiguration.cs:74`) backs the
  pre-check; a violation on save surfaces as `UniqueEmailViolationException`
  (`UnitOfWork.cs:28-31`). The creation use case does not catch it.
- `SetDeanAccountStateUseCase` (:24) shows the per-account pattern: read-only first, then
  `FindDeanByIdAsync` → `NoSuchDeanAccount` (controller `404`), then a no-change check →
  `AlreadyInThatState` (controller `409`, page re-rendered), then the change and one audit row
  `AuditEvent.DeanAccountManaged(adminId, action, dean.Id, now, requestId)` in one transaction.
- `DeanAccountsController` (`[Authorize(Policy = ManageDeanAccounts)]`, :23) is an MVC
  controller: success is Post-Redirect-Get with the confirmation in TempData; refusals re-render
  `/settings/deans` with one page-level message (`Index.cshtml:14-17`); there is no field-level
  validation markup. Per-row forms post to `/settings/deans/{deanId:long}/state` and
  `/password` (`Index.cshtml:38-50`). The contract is `docs/designs/api/US-012-openapi.yaml`.
- `AuditAction` (`src/ClassroomAgent.Domain/Enums/AuditAction.cs`) is closed; its string codes are
  mapped in `AuditEventConfiguration.cs:358-418` and enforced by the CHECK constraint
  `ck_audit_event_action` (:28-38), last changed by migration
  `20261028123007_AddErasureExclusionInAdvance`. `AuditEvent.DeanAccountManaged` (:544) accepts
  only the four management actions.
- Report templates store `AuthorId` with no foreign key; the list joins `AppUser` by id and shows
  its current `Email` (`ReportTemplateRepository.cs:15-21`, `ReportTemplates/Index.cshtml:52-59`).
- A session cookie is checked against the account's security stamp on every request
  (`InstallationSession.ValidatePrincipalAsync`, US-064 spec §1.1); a mismatch signs out and
  redirects to `/sign-in`.

## 2. Business Goal

Keep one Dean account — password, history, audit trail, authored templates, retention clock —
when the school renames the Dean's work email in Google, instead of disabling it and creating a
second account (BR-014, v104).

## 3. Business Flow

1. The school renames the Dean's work email in Google. The program notices nothing.
2. The Admin opens `/settings/deans`, finds the Dean's row and uses its "change login" form:
   enters the new address and saves.
3. The program checks the address by the creation rules. On a refusal the page explains why and
   nothing changes.
4. On success the account's login is the new address, its security stamp is rotated, an audit row
   is written, and the page confirms the change.
5. The Dean's open session (if any) ends on the next request. The Dean signs in with the new
   address and the same password. The old address no longer signs in and is free for a new
   account.

## 4. Functional Requirements

**FR-001 The operation.** A new Dean-account management operation "change login" takes the
acting Admin's id, the Dean account id, the new address as entered, and the request id. It is
reached by a per-row form on `/settings/deans` that posts to a new endpoint under
`/settings/deans/{deanId}` (path and method fixed at API_DESIGN, following the existing `state`
and `password` actions). Antiforgery applies as for the other forms on the page.

**FR-002 Order of evaluation.** The operation evaluates, in this order, and stops at the first
refusal:

1. read-only mode (FR-008);
2. the account exists and is a Dean — otherwise `NoSuchDeanAccount` (`404`, as for `state` and
   `password`; an Admin account's id is "no such Dean");
3. the trimmed address is well formed — the same check as creation (`NotAnEmailAddress`); an empty
   or whitespace-only value is malformed;
4. the address is in exactly the school's domain read server-side from `LegitimacyState`, no
   subdomain — the same check as creation (`OutsideSchoolDomain`); no domain known also refuses,
   as at creation;
5. the normalized address equals the account's own `NormalizedEmail` — the "unchanged" outcome
   (FR-006);
6. no other account (any role) has that normalized address (`EmailAlreadyUsed`).

Steps 3, 4 and 6 must be the same code as creation's checks, not a copy that can drift (shared
helper or shared validator; mechanics left to IMPLEMENTATION).

**FR-003 The change.** On success, in one transaction: a new `AppUser` domain method sets
`Email` and `NormalizedEmail` to the normalized new address and rotates the security stamp
(`RotateSecurityStamp`); one audit row is written (FR-007). No other field of the account changes:
id, `PasswordHash`, `PasswordIsTemporary`, `AccessFailedCount`, `LockoutEnd`, `IsDisabled`,
`UiLanguage`, `LastSuccessfulSignInAt`, `CreatedAt` keep their values (`UpdatedAt` moves, as on
any save). A disabled account may be changed and stays disabled (OD-001 a). The response is
Post-Redirect-Get to `/settings/deans` with a translated confirmation.

**FR-004 Concurrent use of the address.** If the unique index refuses the save because another
account took the address after step 6 (`UniqueEmailViolationException`), the operation answers
`EmailAlreadyUsed` like step 6 and nothing is written — no account change, no audit row.

**FR-005 Effects on sign-in and sessions.** With no further code these follow from FR-003 and
must hold:
- the Dean signs in with the new address (any letter case) and the unchanged password;
- the old address is refused like an unknown login — the generic refusal message, audit category
  "unknown login" (SC-2 step 1);
- a session cookie issued before the change is refused on its next request and the browser is
  sent to `/sign-in` (stamp mismatch);
- the old address is free: creating a Dean account with it succeeds;
- report templates authored by the account show the new address as author; earlier audit rows
  keep referring to the same account id.

**FR-006 Same address (OD-002 a).** When step 5 matches, nothing is written — no account change,
no stamp rotation, no audit row — and the page is re-rendered with a translated message that the
login is unchanged. It is a refusal outcome like `AlreadyInThatState` (a new `DeanAccountRefusal`
value; its status code is fixed at API_DESIGN, `409` being the existing analogue). Letter case and
surrounding spaces do not make an address different.

**FR-007 Audit.** A new closed-list action "Dean login changed" (`AuditAction.DeanLoginChanged`,
code `dean_login_changed`) is written through `AuditEvent.DeanAccountManaged`, which accepts it:
actor the Admin (id, role Admin), object the Dean account (type and id), outcome success, request
id. The row contains neither the old nor the new address (§5 v104, SC-11). The CHECK constraint
`ck_audit_event_action` is extended by an EF Core migration in this Story (PC-2). Validation
refusals (FR-002 steps 2–6, FR-004) and the unchanged outcome write no audit row, as for the
other management operations.

**FR-008 Read-only mode.** In read-only mode the operation is refused in `Application` before
anything else (BR-026, AD-6), the refusal audited with `DeanAccountManagementRefused` (action
`DeanLoginChanged`, the account id, category read-only) and answered like the other management
operations (host error page, `409`). Nothing changes.

**FR-009 Authorization.** The endpoint carries the `ManageDeanAccounts` policy (Admin only, §2
matrix line 764). A Dean is forbidden (`403`) for any account id, their own included; an anonymous
request is redirected to `/sign-in` (SC-4).

**FR-010 Page.** Each Dean row on `/settings/deans` gets a "change login" form: one email input
(`name` fixed at API_DESIGN) and a submit button. In read-only mode the form is shown like the
other per-row forms are today (enforcement is in `Application`, AD-6). On a refusal the page is
re-rendered with the page-level message, as for the other refusals; the entered value is not
re-filled into a row field (only the creation form re-shows its email). The Admin's own account is
not listed (the list holds Dean accounts only), so there is no form for an Admin login.

**FR-011 Texts.** New user-visible strings — the field label, the button, the confirmation, the
"login unchanged" message — are translation entries in Ukrainian and English
(`SharedResource.uk.resx`, `SharedResource.en.resx`, NFR-073). Refusals reuse the creation keys
`DeanAccounts.Refused.NotAnEmail`, `.OutsideSchoolDomain`, `.EmailAlreadyUsed`, so the message is
"the same as on the creation form" (AC-004, AC-005).

**FR-012 Password rule not re-checked.** The existing password is not checked against the new
login's local part (§2 v64 rule applies when a password is set). The next password the Dean or an
Admin sets is checked against the current login — the existing password use cases read
`dean.Email`, so this needs no change.

## 5. Acceptance Criteria

Carried from the Story; ids stable.

- **AC-001 The login changes, the account stays.** After the change of
  `olena.koval@school.example` to `olena.shevchenko@school.example` the account has the new login
  and the same id; templates the Dean authored show the new address as author; earlier audit rows
  refer to the same account id; the page confirms the change.
- **AC-002 Sign-in with the new address and the same password.** The new address and the old
  password sign in; the old address is refused with the generic refusal message.
- **AC-003 An open session ends.** A cookie captured before the change, replayed on a page after
  it, is sent to the sign-in page.
- **AC-004 The same rules as at creation.** An empty, malformed, out-of-domain or subdomain address
  changes nothing, shows the same message as the creation form for that case, and the rejected
  value is not written to the log (SC-10).
- **AC-005 An address taken by another account.** An address of another Dean or of an Admin, in
  any letter case, changes nothing and the page says it is already in use.
- **AC-006 Everything else is untouched.** For an account with a temporary password, a lockout in
  force and the disabled flag set, after the change the password is still temporary, the lockout
  still in force, the account still disabled, and `LastSuccessfulSignInAt`/`CreatedAt` unchanged.
- **AC-007 Audited without addresses.** One audit row "Dean login changed", actor the Admin,
  object the account id, containing neither address.
- **AC-008 Refused in read-only mode.** Refused in the Application layer; nothing changes.
- **AC-009 Only the Admin.** A Dean posting a change for any account (their own included) is
  forbidden; an anonymous request is sent to the sign-in page.
- **AC-010 The old address is free again.** Creating a Dean account with the old address
  succeeds.

## 6. Validation Rules

| Input | Rule | Refusal |
|---|---|---|
| `deanId` (route) | `long`; an id of an existing Dean account | not a number → no route match; unknown or not a Dean → `NoSuchDeanAccount`, `404` |
| new address (form) | trimmed; length 1..254; no whitespace; exactly one `@`; non-empty local part; domain part a valid domain | `NotAnEmailAddress`, key `DeanAccounts.Refused.NotAnEmail`, re-render |
| new address | domain part equals the school domain from `LegitimacyState` exactly (case-insensitive, no subdomain); no school domain known → refused | `OutsideSchoolDomain`, key `DeanAccounts.Refused.OutsideSchoolDomain` |
| new address | normalized value differs from the account's own `NormalizedEmail` | unchanged outcome (FR-006), no write |
| new address | normalized value not used by any other `AppUser` (any role) | `EmailAlreadyUsed`, key `DeanAccounts.Refused.EmailAlreadyUsed` |
| missing field | treated as empty → malformed | as row 2 |

The form value is never written to a log — neither on refusal nor on success (SC-10); the
validation does not rely on framework model binding defaults.

## 7. Security Requirements

- Admin only via `ManageDeanAccounts`; Dean `403`, anonymous → `/sign-in` (SC-4, TC-5).
- Antiforgery token required, as for every form (SC, `trebovaniya.md` §8).
- The school domain is read server-side; no domain value is accepted from the request (SC-2, v55).
- The security stamp is rotated on every real change, ending open sessions (SC-2, v104).
- The password, its hash and temporary flag are untouched; no password is returned or logged.
- The audit row holds neither address (SC-11); logs hold neither address (SC-10 for the rejected
  value; a successful change logs at most the account id).
- An Admin's own login cannot be changed by this operation (FR-002 step 2, FR-010; BR-010).
- Read-only refusal in `Application` (AD-6, BR-026).

## 8. Error Handling

| Situation | Outcome |
|---|---|
| read-only mode | refused audit row, host error page, `409` (as other management operations) |
| unknown id / not a Dean | `404`, nothing written |
| malformed / out of domain / taken | page re-rendered with the creation message for that case, status as for creation refusals (`400`), nothing written |
| same address | page re-rendered with "login unchanged", nothing written (FR-006) |
| unique-index race | as "taken" (FR-004) |
| missing antiforgery token | `400`, as the other forms |

## 9. Non-Functional Requirements

- NFR-073: all new texts in Ukrainian and English.
- PC-2: the audit action CHECK change ships as a migration in this Story; no column change.
- No new NuGet package, no new endpoint outside `/settings/deans`.

## 10. Out of Scope

- An Admin's own login (BR-010: a renamed Admin is a new `AllowedAdmin` entry by the Owner).
- Noticing a rename in Google automatically (v104: not watched).
- Changing the password together with the login (reset stays a separate action).
- Mailing the Dean (the program sends no mail).
- Checking the new address against the Control Plane's `AllowedAdmin` list — creation does not do
  it either; an address that is a Dean login is refused at Google sign-in (v96).

## 11. Interpretations for confirmation

- **I-1 (FR-002 order).** Unknown id is checked before the address, and the "same address" check
  after the format and domain checks: a malformed or out-of-domain entry is reported as such even if
  it would normalize to the current login.
- **I-2 (FR-004).** A concurrent take of the address answers "already in use" instead of an error
  page; the creation path does not do this today and is not changed here.
- **I-3 (FR-006).** "Login unchanged" is a refusal-type re-render with its own message, not a
  success confirmation, by analogy with `AlreadyInThatState`.
- **I-4 (FR-008).** A read-only refusal writes a refused audit row with the account id, like the
  other management operations (US-012 FR-017); the Story's AC-008 does not mention it.
- **I-5 (FR-010).** On a refusal the entered value is not re-shown in the row (AC-004 "the field
  shows the message" is read as the page-level message all Dean-account refusals use).
- **I-6 (FR-005).** AC-001's "templates show the Dean as author" is satisfied by the existing join
  on `AuthorId`; no template data changes.

## 12. Open Decisions

None open. OD-001 (disabled Dean: allowed, stays disabled) and OD-002 (same address: nothing
written, "unchanged" message) were resolved a) by the Owner on 2026-10-28 and are applied in FR-003
and FR-006. `trebovaniya.md` §7 holds no open item this Story depends on. See
`docs/decisions/US-067-open-decisions.md`.

## 13. Traceability

| AC | FR | Validation / other |
|---|---|---|
| AC-001 | FR-001, FR-003, FR-005 | §1.1 template join |
| AC-002 | FR-005 | SC-2 step 1 |
| AC-003 | FR-003, FR-005 | stamp check per request |
| AC-004 | FR-002 steps 3–4, FR-011 | §6 rows 2–3, SC-10 |
| AC-005 | FR-002 step 6, FR-004, FR-011 | §6 row 5 |
| AC-006 | FR-003 | OD-001 a, retention clock §1.1 |
| AC-007 | FR-007 | SC-11, PC-2 |
| AC-008 | FR-008 | AD-6, BR-026 |
| AC-009 | FR-009 | SC-4, TC-5 |
| AC-010 | FR-005 | unique index |
| (OD-002) | FR-006 | §6 row 4 |
