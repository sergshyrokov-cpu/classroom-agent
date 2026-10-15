---
artifact_type: specification
story: US-051
version: 1
status: APPROVED
created_at: 2026-10-15T19:54:25Z
updated_at: 2026-10-15T20:20:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-051-google-sign-in-admin-accounts-only.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/decisions/US-051-open-decisions.md
    version: 1
supersedes: null
---

# US-051 Specification — The Google sign-in admits only Admin accounts

## 1. Overview

The Admin's Google sign-in (US-008, `CompleteGoogleSignInUseCase`) is brought to
the rule `trebovaniya.md` v96 added: **one account, one role** (§2 "Модель
авторизации": «Вход через Google пускает только учётную запись с ролью Админ (или
создаёт её при первом входе). Если email … принадлежит учётной записи Декана, вход
отклоняется, даже когда этот email есть в `AllowedAdmin` … Учётная запись Декана в
Админа не превращается и не меняется. Порядок проверок прежний: сначала
`AllowedAdmin` …, затем роль, затем отключение»; §5 audit refusal category
«учётная запись не Админа»; §9 «Учётная запись Декана с тем же email Админом не
становится»; SC-3, SC-11, BR-011).

Today the use case admits any existing, enabled account whose email the Control
Plane approves, whatever its role (pre-pilot audit 2, finding F-2). A Dean whose
address is also in `AllowedAdmin` therefore signs in through Google as a Dean,
skipping the password, the forced temporary-password change and the lockout
(SC-2), and the audit records an Admin sign-in.

After this Story the Google sign-in admits only an account with role Admin, or
creates one; an existing account with any other role is refused with its own
message and audit category and is left untouched.

Touched: `CompleteGoogleSignInUseCase` (Application), the sign-in refusal and
audit refusal enums, the callback's refusal-to-text mapping and its log line
(Web), two translation entries per language, and the audit table's allowed
refusal categories (a CHECK constraint — one migration, PC-2). No endpoint,
route, status code or page is added.

## 2. Business Goal

Every account keeps exactly one role, so the Dean password rules (SC-2) protect
every Dean account without exception, and the audit tells the truth about who
signed in. A person who needs both roles uses two addresses (§2 v96, §9 v50).

## 3. Business Flow

The Google sign-in decision, after the callback has proven `state` and the
correlation cookie (unchanged, US-008):

1. The email Google returned is normalized (unchanged).
2. The Control Plane is asked whether the email is in `AllowedAdmin` (unchanged).
   Not approved, unknown installation or no answer → refused exactly as before
   v96, whatever account exists (AC-003).
3. **New.** An account with that email exists and its role is not Admin → refused
   as "not an Admin account". The account is not changed.
4. The account exists and is disabled → refused as "account disabled" (unchanged).
5. Otherwise the account is created as Admin (first sign-in) or stamped, and the
   Admin is signed in (unchanged).

Every outcome writes exactly one audit row (unchanged).

## 4. Functional Requirements

**FR-001 The role check.** In `ExecuteAsync`, after the Control Plane answered
`Allowed` and before the disabled check, an existing account whose `Role` is not
`AppRole.Admin` is refused with audit category `NotAnAdminAccount` and sign-in
refusal `NotAnAdminAccount` (§2 v96, SC-3). The check is "role is not Admin", not
"role is Dean": the rule admits only Admin accounts (I-1).

**FR-002 The account is not touched.** The refusal of FR-001 does not modify the
account: no sign-in time, no failed-attempt count, no lockout, no security stamp,
no role, no disabled flag change; it creates no session and no cookie of the
signed-in user. The account is not converted to Admin (§2 v96).

**FR-003 The audit row.** The refusal of FR-001 writes exactly one audit row
through the existing refusal path: action Admin sign-in, outcome refused, actor
the account's internal id and its role (Dean), refusal category "not an Admin
account", the request id; no email, no name (§5 v96, SC-11, SC-10). It is the
BR-026 service write `PermittedServiceWrite.AuditEvent`, so it is written in
read-only mode as every other sign-in refusal is (AC-005).

**FR-004 The order of checks.** `AllowedAdmin` first, then the role, then the
disabled state (§2 v96, SC-3):
- a reply other than `Allowed` keeps its pre-v96 category and refusal whatever
  the account's role (`NotInAllowedAdmin` / `NotApproved`;
  `UnknownInstallation` and `ControlPlaneUnavailable` / `CouldNotConfirm`);
- a disabled Dean account with an approved email is refused as "not an Admin
  account", not as "account disabled";
- a disabled Admin account is refused as "account disabled", as before.

**FR-005 The concurrent first sign-in.** When the first sign-in of an address
finds no account, tries to create the Admin, and the unique index reports that an
account with that address now exists (`UniqueEmailViolationException`, US-008
spec I-10), the account read again goes through the same role and disabled checks
as in FR-001 and FR-004 before it is stamped: a Dean account created in the
meantime is refused as "not an Admin account" and left untouched, a disabled
account is refused as "account disabled". The rejected Admin insert leaves no
row, and the outcome writes exactly one audit row (I-3).

**FR-006 What the person sees.** The callback answers the refusal of FR-001 the
way it answers every other refusal — redirect to the sign-in page with the
refusal text — using a new translation key `SignIn.Refused.NotAdminAccount`
(NFR-073). Its text says that the address is a Dean account, that the Dean signs
in with login and password, and that the Admin role needs a different address:

| Language | Text |
|---|---|
| uk | Ця адреса — обліковий запис декана. Увійдіть за логіном і паролем. Для ролі адміністратора потрібна інша адреса. |
| en | This address is a Dean account. Sign in with your login and password. The Admin role needs a different address. |

The text names no email address and no other account data (I-4).

**FR-007 The log line.** The refusal is logged by `SignInLog.Refuse` at
`Warning` — it is a decision, like "not approved" and "account disabled" (US-008
FR-020) — with its own event id and the request id only; no email, no account id
beyond what the existing refusal lines carry (SC-10).

**FR-008 The database accepts the category.** The audit table's allowed
refusal categories (`ck_audit_event_refusal_category_value`) include the new
category; the change ships as an EF Core migration (PC-2). No other column,
constraint or table changes. Existing rows are unaffected.

**FR-009 Nothing else changes.** The Admin's first sign-in and later sign-ins,
the Dean's password sign-in (US-012, US-048), the Control Plane and its
`AllowedAdmin` check, the refusal texts and categories that exist today are
unchanged.

## 5. Acceptance Criteria

| AC | Source | Statement |
|---|---|---|
| AC-001 | Story AC-001 | Given an enabled Dean account with email E and the Control Plane answering that E is in `AllowedAdmin`, when a person completes the Google sign-in as E, then no session is created, the Dean account is unchanged (sign-in time, failed-attempt count, lockout, security stamp, role, disabled flag), and the sign-in page shows the `SignIn.Refused.NotAdminAccount` text. |
| AC-002 | Story AC-002 | Given the refusal of AC-001, the audit holds exactly one new row for it: Admin sign-in, refused, actor the Dean account's id with role Dean, category "not an Admin account", no email. |
| AC-003 | Story AC-003 | Given a Dean account with email E, when the Control Plane answers not approved, unknown installation, or does not answer, then the refusal and category are the pre-v96 ones (`NotApproved`/`NotInAllowedAdmin`, `CouldNotConfirm`/`UnknownInstallation`, `CouldNotConfirm`/`ControlPlaneUnavailable`); and given a **disabled** Dean account whose email is approved, the category is "not an Admin account", not "account disabled". |
| AC-004 | Story AC-004 | An approved email with no account gets a new Admin account and is signed in; an approved email of an existing enabled Admin account is signed in; a disabled Admin account is refused as "account disabled" — all as before. |
| AC-005 | Story AC-005 | In read-only mode, the refusal of AC-001 and its audit row of AC-002 are written exactly as outside it. |
| AC-006 | Story AC-006 | After the refusal of AC-001, the Dean signs in with login and password as before, including the forced change of a temporary password. |
| AC-007 | derived, FR-005 | When a first Google sign-in of E finds no account but its Admin insert is rejected because a Dean account with E was created in the meantime, the sign-in is refused as "not an Admin account", the Dean account is unchanged, no Admin account with E exists, and exactly one audit row (AC-002 shape) is written. |
| AC-008 | derived, FR-008 | The database accepts an audit row with the new refusal category, and still rejects an unknown category. |
| AC-009 | derived, FR-007 | The refusal is logged at `Warning` with its own event id, and the log line carries no email. |

## 6. Validation Rules

- **VR-001** The input is the email the callback already validated (US-008
  VR-005: present, verified by Google, usable); this Story adds no input and no
  new validation.
- **VR-002** The role compared is the stored `AppUser.Role`; the only value that
  passes is `AppRole.Admin` (FR-001).
- **VR-003** The stored refusal category code is one of the CHECK constraint's
  closed list; the new member has exactly one code (FR-008).

## 7. Security Requirements

- **SC-3 / BR-011.** The Google sign-in admits only an `AppUser` with role Admin
  or creates one; a Dean account is never admitted, converted or changed through
  it. Admitting it would skip SC-2 — a Critical finding (SC-3 v96).
- **SC-2.** The Dean account's password, forced change and lockout state are not
  read, reset or bypassed by the Google path (FR-002).
- **SC-11.** One audit row per outcome, actor the account's id and role, the new
  category, no personal data; rows never updated.
- **SC-10.** No email, name or token in the log line or the audit row.
- **SC-4 / authorization.** No endpoint, policy or anonymous surface changes; the
  callback keeps its `state` and correlation-cookie protection.
- **AD-6 / BR-026.** The refusal runs in read-only mode only through the existing
  `PermittedServiceWrite.AuditEvent`; the closed list is not widened.
- **Information shown.** The refusal text tells only the person who just proved,
  through Google, that they control address E that E is a Dean account here; it
  shows no address and no other account (I-4).

## 8. Error Handling

- The refusal is an outcome, not an exception (AD-9): `SignInOutcome.Refused`.
- A Control Plane failure keeps its existing handling (AC-003).
- A failure to write the audit row propagates as today (the existing refusal
  path); no new error page or status code.
- FR-005: the unique-index conflict is handled as today, then routed through the
  role and disabled checks instead of always stamping.

## 9. Non-Functional Requirements

- **NFR-073** The new text exists in Ukrainian and English translation files; no
  hard-coded string.
- **TC-4** Tests use the Control Plane stub and the Google stub; no live call.
- **TC-2** The audit row and the migration are tested against PostgreSQL
  (Testcontainers).
- No new NuGet package.

## 10. Out of Scope

- Converting a Dean into an Admin, or any change to the Dean account.
- Any check in the Control Plane — it does not know the installation's accounts.
- The Dean password sign-in — unchanged.
- Revoking a live session already open (audit 1 I-2) — unchanged.
- A data fix for past audit rows that recorded a Dean's Google sign-in as an Admin
  sign-in — rows are never updated (SC-11).
- The reverse case (an Admin creates a Dean with an existing Admin's address) —
  already closed by the unique email (§2 v96).

## 11. Open Decisions

None. The Story carries none, and `trebovaniya.md` §7 (v96) has no open item this
Story depends on. See `docs/decisions/US-051-open-decisions.md`.

## 12. Interpretations

- **I-1** "Not an Admin account" is checked as `Role != Admin`. v1 has only Admin
  and Dean, so the effect is the Story's; the wording follows §2 "пускает только
  учётную запись с ролью Админ" rather than naming Dean.
- **I-2** The new category is checked before "account disabled" even for a
  disabled Dean (Story AC-003; §2 v96 order "роль, затем отключение").
- **I-3** FR-005 extends the role and disabled checks to the concurrent
  first-sign-in path. Neither §2 nor the Story names that path, but without it a
  Dean created at the same moment as the Admin's first sign-in would still be
  stamped and signed in through Google — the exact gap v96 closes.
- **I-4** The message text of FR-006 is the Story's content in the project's
  voice; it reveals the account kind only to the person who controls the address.
- **I-5** The audit row's actor role is the account's own role (Dean), as SC-11
  requires for every actor; the action stays "Admin sign-in" because it is the
  Admin sign-in channel that refused.
- **I-6** The refusal does not count as a failed Dean sign-in attempt and does
  not touch the lockout: no password was checked (FR-002).

## 13. Traceability

| AC | FR / VR |
|---|---|
| AC-001 | FR-001, FR-002, FR-006, VR-002 |
| AC-002 | FR-003, FR-008 |
| AC-003 | FR-004 |
| AC-004 | FR-004, FR-009 |
| AC-005 | FR-003 |
| AC-006 | FR-002, FR-009 |
| AC-007 | FR-005 |
| AC-008 | FR-008, VR-003 |
| AC-009 | FR-007 |

## 14. Delegation

Nothing was delegated: the facts were gathered by direct reads of
`trebovaniya.md` (header v96, §2 lines 593–603, §5 lines 1470–1477, §9 lines
2414–2416), `security-conventions.md` SC-3 and SC-11, `business-rules.md` BR-011,
and the code `CompleteGoogleSignInUseCase.cs`, `AuditRefusalCategory.cs`,
`SignInRefusal.cs`, `AuditEventConfiguration.cs`
(`ck_audit_event_refusal_category_value`), `GoogleSignInEvents.cs`
(`TextKey`), `SignInLog.cs` (event ids 2200–2204).

Expected next stages: API_DESIGN not applicable (no endpoint, route, status
code or body change); DB_DESIGN applicable (FR-008: CHECK constraint migration).
