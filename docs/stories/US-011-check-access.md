---
id: US-011
epic: EPIC-6
title: Check access diagnostic
slug: check-access
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v79.
---

# User Story

As an **Admin** of a school's installation

I want a "Проверить доступ" button that tries the school's Google connection for
real and tells me, check by check, what works and what is not configured

So that I know the school's super-admin set up domain-wide delegation correctly
before anyone relies on synchronisation — and, when something is missing, I can
tell them exactly what, instead of forwarding a raw Google error.

---

# Business Value

`trebovaniya.md` §4 (Epic 6) asks for it in plain words: test calls to the
Classroom API and the Admin Reports API on behalf of the impersonation user, a
result per call, and on a permission error "внятное сообщение о том, что именно не
настроено (какой скоуп отсутствует в DWD), а не сырой текст исключения Google API".
Until this exists, the first sign of a delegation mistake is a failed
synchronisation, which is the worst place to diagnose it.

The same calls, run automatically at every start and written to the log, are
how the **Owner confirms a rotated service-account key without the school**
(`trebovaniya.md` §5, §9 "Ротация ключей", DC-5, v54): create the new key, restart,
read the self-check line, only then delete the old key. Without the self-check
the rotation procedure has no safe step between "new key placed" and "old key
deleted".

This is also the Story that adds the **first real Google port** of the program,
and with it the first code that holds the service-account key at runtime. The
read-only rule "no call to Google at all" (BR-026, SC-5) stops being a structural
fact and becomes something this Story must enforce and prove.

---

# Scope

**In scope:**

- the "Проверить доступ" action in the admin-panel settings section, Admin only —
  the permission-matrix row "Проверить доступ" is `✔` Admin, `✘` Dean
  (`trebovaniya.md` §2, v39);
- a per-scope check of domain-wide delegation for the six scopes of
  `trebovaniya.md` §6, and one minimal read from each of the Classroom API and
  the Admin Reports API as the technical account (OD-002);
- a result per check, and for every failure a translated, diagnosable message
  naming what is not configured — never Google's raw error text (§4, SC-8);
- the startup self-check: the same checks at every start, the outcome written to
  the log only, never shown to a user (§4, DC-5, DC-10);
- the first Google data port in `Application/Ports` and its implementation in
  `Infrastructure`, obtaining credentials from the service-account key through
  the secret-store reference in configuration (SC-7, DC-5);
- the audit row for each run of the diagnostic (`trebovaniya.md` §5, SC-11);
- read-only mode: the action refuses and no Google call is made; the self-check
  does not run (BR-026, SC-5);
- Ukrainian and English for every message (NFR-073).

**Out of scope:** synchronisation of any data — **US-013** and later; storing
the result of a check (OD-003); limiting how often the check runs (OD-005);
changing `WorkspaceConnection` — **US-009**; the connection instruction —
**US-010**; Dean accounts — **US-012**; any write to Google Workspace — a Hard
Stop, not a scope choice; verifying the minimum Workspace roles of the technical
account, which is `trebovaniya.md` §7 item 10 and stays open — this Story reports
whether reads succeed, it does not name roles.

---

# Acceptance Criteria

## AC-001 Only an Admin can run the check

**Given** the installation is running

**When** "Проверить доступ" is requested

**Then**:

- an Admin can run it; a Dean is refused; an anonymous visitor is sent to sign
  in (`trebovaniya.md` §2, SC-4);
- both the allowed-role and the forbidden-role case are tested (TC-5);
- the SC-4 closed list of anonymous endpoints gains nothing;
- the action sits in the settings section US-009 established, next to the
  connection settings and the instruction.

## AC-002 Each of the six scopes is checked on its own

**Given** a saved `WorkspaceConnection` and a service-account key available
through the configured reference

**When** an Admin runs the check

**Then**:

- delegation is checked separately for each of the six scopes of
  `trebovaniya.md` §6, impersonating the technical account of the
  `WorkspaceConnection` (OD-002);
- the result lists every scope with its outcome, so a scope the super-admin
  forgot is named, not inferred;
- only the six scopes are ever requested — never `drive.file`,
  `classroom.profile.photos` or any scope that allows writing; the list is the
  constant US-010 placed in `Domain/Rules`, not a second copy (SC-8);
- the Admin's own OAuth session is never used for any of these calls (SC-8).

## AC-003 One real read from each API proves the technical account can read

**Given** delegation is in place for the scopes a call needs

**When** the check runs

**Then**:

- one minimal read is made against the Classroom API and one against the Admin
  Reports API (Meet events), as the technical account (OD-002);
- each read's outcome is shown next to the scope results;
- whatever Google returns is used only to decide success or failure: no course
  name, event, email or other school data from the response is shown, stored or
  logged (SC-10, AGENTS.md "Security Policy");
- a domain with no courses and no Meet events still passes when access is in
  place — an empty answer is success.

## AC-004 A failure says what is not configured

**Given** a check that fails

**When** the result is shown

**Then**:

- the message is translated text that names the cause the Admin can act on —
  for example that a scope is not authorised in domain-wide delegation, that the
  technical account cannot read, or that the service-account key is not
  available — never Google's exception text (§4, SC-8, SC-10);
- permission failures (`unauthorized_client`, `access_denied`, a missing scope)
  are not retried (SC-8, AD-5);
- a missing key or an unresolvable secret reference is reported as a problem for
  the Owner, without revealing the reference or anything about the key (SC-7);
- a Google outage or timeout is reported as such, distinct from a configuration
  mistake.

## AC-005 Without a saved connection there is nothing to check

**Given** no `WorkspaceConnection` has been saved yet

**When** an Admin opens the check

**Then** the action is refused with a plain statement that the connection must
be configured first, and no Google call is made.

## AC-006 In read-only mode no call reaches Google

**Given** an installation in read-only mode — suspended, past its grace period,
or never legitimated (BR-025)

**When** an Admin runs the check

**Then**:

- it is refused with `409` and a message stating the read-only reason
  (API-5, SC-5, v39);
- no call is made to Google, including the token request — proven in the
  Application layer with the Google port substituted (TC-5, TC-4);
- the startup self-check does not run in read-only mode (AC-008).

## AC-007 Every run is audited without personal data

**Given** an Admin runs the check

**When** it finishes or is refused

**Then**:

- one `AuditEvent` row is written: the Admin as actor, the action, the target,
  the outcome and the request identifier (`trebovaniya.md` §5, SC-11);
- the row carries no email, no domain-user name and no Google data; the overall
  result ("access in place" / "not configured") at most (OD-003);
- no other table is written — the result of the check is not stored (OD-003).

## AC-008 The self-check runs at every start and writes only to the log

**Given** the installation starts

**When** the startup self-check runs

**Then**:

- it performs the same checks as AC-002 and AC-003;
- success is logged at `Information`, failure at `Error`, as DC-10 fixes;
- the log line carries only internal identifiers and the per-check outcome — no
  email, no domain, no Google response text (SC-10);
- it is never shown to a user and writes no audit row;
- a failing self-check does not stop the installation from starting;
- when it cannot run — no `WorkspaceConnection` saved, or the installation in
  read-only mode — one line says it was skipped and why, so an absence in the log
  is never mistaken for success (OD-004).

## AC-009 Tests never reach Google

**Given** the test suite

**When** it runs

**Then** every Google call goes through the substituted port with synthetic
answers, including permission failures for individual scopes, an empty domain
and an outage; no test uses a real key or calls a live Google API (TC-4).

## AC-010 Every string is translated

**Given** an installation whose UI language is Ukrainian or English

**When** the result of a check is shown in either language

**Then** every sentence comes from the translation files; scope identifiers are
data and are rendered as they are; both language files carry every key this
Story adds (NFR-073).

---

# Open Decisions

## OD-001 Which library the program uses to call Google

No Google data package is referenced in any project yet, and adding a NuGet
package requires an approved Open Decision (AGENTS.md "Technology Stack").

Options:

1. **Google's official .NET client libraries** — `Google.Apis.Auth`,
   `Google.Apis.Classroom.v1`, `Google.Apis.Admin.Reports.reports_v1`. Maintained
   by Google; synchronisation (US-013) will need the same ones.
2. Only `Google.Apis.Auth` for credentials, with the REST calls written by hand.
3. No package: sign the service-account token ourselves.

**Resolution:** option 1, decided by the Owner on 2026-09-21. The three official
packages are added to `Infrastructure` only; no Google SDK type crosses into
`Application` or `Domain` (AD-4). The Specification pins the versions and the
security review checks them for known vulnerabilities.

## OD-002 How the check finds out which scope is missing

Google issues a delegated token only when every requested scope is authorised,
so a single request for all six fails without saying which one is missing — and
§4 requires the message to name it.

Options:

1. **One token request per scope, then one minimal read per API.** Six token
   requests name each missing scope precisely and read no data; one Classroom
   read and one Admin Reports read prove the technical account can actually read.
2. Two reads only, with all six scopes requested at once. Cannot name the
   missing scope.
3. A real read per scope. Rosters, coursework and materials need an existing
   course, so a domain without courses would report nothing useful.

**Resolution:** option 1, decided by the Owner on 2026-09-21.

## OD-003 Whether the result of a check is stored

Options:

1. **Not stored.** The result is shown right after the run; the audit row
   records that the check ran and its overall outcome, nothing more. No schema
   change beyond what the audit row itself needs.
2. Store the last result in a new table so it can be seen later — a migration,
   and a new service write to place in BR-026's closed list for read-only mode.

**Resolution:** option 1, decided by the Owner on 2026-09-21.

## OD-004 What the self-check logs when it cannot run

Options:

1. **One line saying it was skipped and why** (no connection saved, read-only
   mode). The Owner reading the log after a key rotation sees that no check took
   place and does not mistake silence for success.
2. Nothing.

**Resolution:** option 1, decided by the Owner on 2026-09-21. Which level that
line uses is the Specification's to fix within DC-10.

## OD-005 Whether repeated runs are limited

Each run is about eight calls to Google and one audit row.

Options:

1. **No limit.** Only an Admin can run it, every run is audited, and the volume
   is far below Google's quotas.
2. A cooldown (for example once a minute), which needs stored state and a
   refusal message of its own.

**Resolution:** option 1, decided by the Owner on 2026-09-21.

---

# Notes

- **The first real Google port.** `IGoogleDataPort` has been a marker since US-007
  (spec FR-007): a use case holding one must also take `IReadOnlyModeGuard` and
  call it first. This Story is the first to do so, and it inherits the US-007
  security-review finding F-5 (SC-8): prove the read-only refusal at the port's
  own use case.
- **The key never leaves the process.** The service-account key is read through
  `ISecretStore` from the reference in configuration (SC-7, DC-5); it is not in
  the database, not in a DTO, not in a view, not in a log, not in an error
  message. DC-2 already lists the secret-store reference among an installation's
  settings; no code reads it yet, so the name of the configuration key is fixed by
  the Specification.
- **Impersonation is the technical account** of the saved `WorkspaceConnection`
  (BR-015) — never the Admin who pressed the button, never a super-admin (SC-8).
- **Audit enum growth.** `AuditAction` gains a member for this action. If the
  audit table constrains the action column, that is a constraint-amending
  migration, as US-009 needed for `WorkspaceConnectionSaved` — DB_DESIGN decides.
- **A refused run.** Whether a run refused in read-only mode or for a missing
  connection writes an audit row with outcome "refused" should follow the
  precedent US-009 set for its own refusals; the Specification states it.
- **API shape.** `api-conventions.md` API-3 already names
  `POST /api/v1/workspace-connection/test` for this check; the settings screens
  of US-009 and US-010 are server-rendered pages. API_DESIGN reconciles the two
  and is bound by the antiforgery rule for a POST (API-7).
- **Startup order.** The self-check must decide read-only mode from the stored
  `LegitimacyState` like every other use case; whether it waits for anything else
  at start is for the Specification. A key rotation restarts the installation
  (§9), so the self-check at that restart is the one the Owner reads.
- **What the check cannot tell.** It proves that reads succeed; it cannot prove
  that the technical account holds *nothing beyond* read access. §7 item 10 stays
  open, and no message of this Story names a Workspace admin role (the same line
  US-010 OD-001 drew).
- Nothing in this Story touches the Control Plane, the service channel or
  `ClassroomAgent.Contracts`.
