---
artifact_type: specification
story: US-009
version: 1
status: APPROVED
created_at: 2026-09-20T13:09:21Z
updated_at: 2026-09-20T13:25:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-009-configure-workspace-connection.md
    version: null
  - path: trebovaniya.md
    version: 79
  - path: docs/decisions/US-009-open-decisions.md
    version: 1
supersedes: null
---

# US-009 Specification — Configure WorkspaceConnection

## 1. Overview

US-008 gave the installation its first human user. This Story gives that user the
first thing they can *change*: the record of which Google Workspace domain the
installation serves and which technical account it will read the school's data
on behalf of (`trebovaniya.md` §3, BR-015).

Three properties shape everything below.

- **It is a control, not a form.** Saving the connection is the second of the
  Owner's three points of control (`trebovaniya.md` §9): the domain comes from
  the Control Plane, and a connection that does not match it is refused (BR-020).
  The refusal lives in `Application`, so no screen, API client or later Story can
  route around it.
- **It reads nothing from Google.** The technical account is recorded, not
  verified: proving that it works is "check access" (US-011). This Story adds no
  Google port, requests no scope and makes no call, so the US-007 security-review
  finding F-5 stays with US-011.
- **It is the first Admin write that read-only mode blocks.** US-007 built the
  refusal and US-008 mapped it to HTTP; here it finally guards a real business
  write, with the settings still viewable behind it (BR-026).

What the record holds is fixed by the requirements and is deliberately small: the
domain and the impersonation user. **Neither the service-account key nor the
reference to it appears in this table, in any DTO, in any view or in any form** —
the Owner places both at deployment (DC-3, SC-7, PC-9), and a column or field
carrying either is a Critical finding, not a design choice.

## 2. Business Goal

A school cannot be read until someone states *where* to read and *as whom*. The
Owner has already fixed the "where" in the Control Plane; the Admin supplies the
"as whom" and confirms the binding. Until this Story exists, DC-2 step 7 cannot
be performed, so the connection instructions (US-010), the "check access"
diagnostic (US-011) and all of EPIC-1 have nothing to work with.

The Owner's interest is narrower and sharper: **an installation must be unable to
look at a domain the Owner never approved**, whatever a school's Admin types
(BR-020, SC-9).

## 3. Business Flow

### 3.1 The first configuration of a new school

The Admin signs in (US-008) and opens the connection settings. The page states
that the connection is not configured yet and shows the school's domain, which
the installation knows from its last successful legitimacy check. The Admin
enters the technical account's address, saves, and sees the saved connection with
the note that checking access is a separate action (US-011).

### 3.2 Correcting the technical account

The school's super-admin recreates the technical account under another address.
The Admin opens the same page, enters the new address and saves. The single
record is updated; the previous value is gone from the table and the change is in
the audit trail.

### 3.3 An address outside the school's domain

The Admin enters their own address, a personal Gmail account, or an address in a
neighbouring school's domain. The save is refused with a message naming the
domain this installation may work with. Nothing is written except the audit row
of the refused attempt.

### 3.4 A school in read-only mode

The `Installation` is suspended, the grace period has expired, or no legitimacy
check has ever succeeded (US-007). The settings page still shows everything and
names the reason; the save is refused before it reaches the database, and the
refusal is audited.

### 3.5 An installation that has never been legitimated

`LegitimacyState` holds no domain, so the allowed domain is unknown. The save is
refused with a message that blames neither the Admin nor their input: the
installation has not yet confirmed its legitimacy, and the domain comes from the
Control Plane (BR-020). This case is also read-only, and the two refusals are
tested independently so neither hides the other.

### 3.6 A saved connection that no longer matches (OD-002)

A legitimacy check reports a domain that differs from the saved connection —
which BR-021 says cannot legitimately happen. The connection is reported as
invalid and is not used by anything; nothing is deleted or corrected
automatically. Saving again, which writes today's domain, is what clears it.

## 4. Functional Requirements

### FR-001 The `WorkspaceConnection` entity

`WorkspaceConnection` in `ClassroomAgent.Domain.Entities`: the installation's
single connection record (`trebovaniya.md` §3).

| Property | Rule |
|---|---|
| `Id` | surrogate key (PC-3) |
| `Domain` | the Google Workspace domain the connection is bound to, lower-cased, non-empty |
| `ImpersonationUserEmail` | the school's technical account, lower-cased, non-empty (BR-015) |
| `CreatedAt`, `UpdatedAt` | by the timestamp interceptor (PC-6) |

- **At most one row.** Enforced in the database by the singleton pattern US-005
  established for `legitimacy_state` — a shadow `singleton` column, always true,
  with a unique index and a check constraint — not only by a check before insert
  (`trebovaniya.md` §3: one active domain at a time).
- Private setters and a factory; the domain is set from `LegitimacyState` by the
  use case, never from a request (OD-001).
- **No key, no secret, no reference to either, no client id, no password** — the
  table has exactly the columns above (PC-9, SC-7). This is a Hard Stop.
- No navigation property to any other entity; nothing references it (PC-8).

### FR-002 The connection state

`Application` produces one value describing the connection, and every consumer —
this Story's page, and later synchronization and "check access" — reads that
value instead of re-deriving it:

| State | Meaning |
|---|---|
| `NotConfigured` | no row exists |
| `Configured` | a row exists and its domain equals the `Installation` domain |
| `DomainMismatch` | a row exists and its domain differs from the `Installation` domain (OD-002) |
| `DomainUnknown` | no legitimacy check has ever succeeded, so no domain is known; any existing row cannot be judged |

`GetWorkspaceConnectionQuery` returns the state together with the school's domain
(when known) and, when a row exists, its two values — as a DTO, never the entity
(AD-8). `DomainMismatch` and `DomainUnknown` both mean **the connection must not
be used**; the DTO says so as a single boolean so a later caller cannot forget a
case (OD-002).

### FR-003 The allowed domain comes from `LegitimacyState`

The `Installation` domain is read from `LegitimacyState` (US-005, v54), which the
legitimacy check writes even in read-only mode (BR-026). This Story reads it and
never writes it, and never asks the Control Plane directly — an installation with
an unreachable Control Plane must still be able to see its settings.

The domain is "known" only when a legitimacy check has succeeded at least once
and the stored domain is non-empty; otherwise the state is `DomainUnknown`
(FR-002). A stale value is never substituted by a guess: the last recorded answer
is used when one exists, and nothing is used when none does.

### FR-004 The connection settings page

A Razor view in the admin panel, `GET`, Admin only (FR-010).

It shows:

- the school's domain, from FR-003, or the statement that it is not yet known;
- the connection state of FR-002 in words: not configured yet, configured, or
  saved for a domain that no longer matches;
- the saved technical account, when a row exists;
- one sentence saying what the technical account is — a school account created by
  the super-admin, not the Admin's own account and not a super-admin (BR-015).
  The full instruction text is US-010;
- one sentence saying that saving records the account and does not check it, and
  that checking access is a separate action (US-011);
- in read-only mode, the reason, in the user's language (BR-026);
- the form of FR-005.

Opening the page writes nothing. The view receives a view model built from the
`Application` DTO and holds no rule of its own (AD-3, AD-8).

### FR-005 The save endpoint

`POST`, Admin only, with the antiforgery token — the global rule of US-008 FR-005
covers it and gains no exemption. A `GET` saves nothing.

The request carries **one field: the impersonation user's email** (OD-001). The
domain is not submitted; a value that nevertheless arrives under any field name
is ignored, because the use case takes its domain from `LegitimacyState` (FR-003,
FR-006) and the request model has no domain property to bind to.

The request model lives in `Application/Models/Requests` with its DataAnnotations
and the custom attribute in `Application/Validation` (`trebovaniya.md` §8,
`package-map.md`) — the first members of both namespaces. Validation runs before
the use case (FR-006 step 1).

On success the Admin is redirected back to the page, which shows the saved values
and a confirmation. On a refusal the page is re-rendered with the message, and
the address the Admin typed is preserved in the form so it can be corrected —
it is not written to a log (SC-10).

### FR-006 The save use case

`SaveWorkspaceConnectionUseCase` in `Application/UseCases`, in this order:

1. **The request has already been validated** (VR-001…VR-003). An invalid request
   never reaches the use case.
2. **Read-only guard first** — `IReadOnlyModeGuard.EnsureAllowedAsync` before any
   repository, port or transaction (US-007 FR-002, AD-6). The use case takes the
   guard in its constructor, so the structural rule of US-007 AC-007 is satisfied
   without registering it in `PermittedServiceWrites`: saving a connection is
   **not** on the BR-026 closed list and the list is not widened (FR-008).
3. **Read the allowed domain** (FR-003). Unknown → refuse with
   `DomainNotConfirmed`.
4. **Check the impersonation user's email domain** against it (FR-007). Mismatch
   → refuse with `ImpersonationDomainMismatch`.
5. **Check the domain to be written** against it (FR-007). This is the same
   comparison the previous step makes, applied to the value that will be stored;
   it is unreachable through the form (OD-001) and is written and tested because
   BR-020 is a control, not a form rule. Mismatch → refuse with `DomainMismatch`.
6. **Write**, inside one transaction (`IUnitOfWork.ExecuteInTransactionAsync`, as
   US-008 db-design §4.4 established): create the row, or update the existing
   one, and write the audit row of FR-009 in the same transaction, so a saved
   connection without its audit row is impossible.
7. **Return an outcome, never an exception**, for every refusal of steps 3–5
   (AD-9). The outcome carries a reason code; the user-visible string is resolved
   in presentation (AD-6, NFR-073).

Saving values identical to the stored ones is accepted, updates nothing beyond
`UpdatedAt` and still writes the audit row of a change: the Admin performed the
action, and the audit records actions.

### FR-007 The domain comparison rule

One implementation in `Application`, used by FR-002, FR-006 step 4, FR-006 step 5
and nothing else:

- both sides are trimmed, lower-cased with the invariant culture, and one
  trailing dot is removed;
- the comparison is then ordinal equality;
- **a subdomain is not the domain**: `sub.school.example` never equals
  `school.example`;
- the domain part of an email is everything after its single `@`, taken after
  VR-001 has proven there is exactly one.

The rule is a pure function and is unit-tested directly, including the cases
AC-004 names: a neighbouring domain, a subdomain, a difference in case only, and
a trailing dot.

### FR-008 Read-only mode

Saving the connection is blocked in read-only mode (BR-026, which names
"connection settings" explicitly). Viewing keeps working, including the school's
domain and the saved values (FR-004).

- `PermittedServiceWrite` gains **no member** and `PermittedServiceWrites` gains
  **no entry for the save**. A test asserts the enum still has exactly the four
  members US-007 established.
- The refusal is `ReadOnlyModeException` from the guard, which US-008 FR-014 maps
  to `409` under `/api/v1` and to the error page elsewhere. This Story adds no
  second mapping.
- The audit row of a read-only refusal (FR-009) is written after the guard has
  thrown, by declaring `PermittedServiceWrite.AuditEvent` around that commit
  alone — audit rows are on the BR-026 list. The connection itself stays
  unwritten, and the exception continues to the handler after the row is
  committed.
- Read-only behaviour is proven in `Application`, not as UI state (TC-5, AD-6):
  the form is not hidden and the button is not disabled to achieve it.

### FR-009 Audit

`AuditAction` gains `WorkspaceConnectionSaved`. `AuditTargetType`, empty since
US-008, gains its first member: `WorkspaceConnection` (US-008 I-11).

| Row | When | Actor | Outcome |
|---|---|---|---|
| first save | no row existed | the Admin's `AppUser` id and role | `Succeeded` |
| change | a row existed | the Admin's `AppUser` id and role | `Succeeded` |
| refused save | any refusal of FR-006 steps 2–5 | the Admin's `AppUser` id and role | `Refused` |

- The row distinguishes a first save from a change. Both are the one action
  `WorkspaceConnectionSaved` that `trebovaniya.md` §5 names ("сохранение и
  изменение `WorkspaceConnection`"); which of the two it was is carried by the
  target id being newly created or already present, and the Specification
  requires the distinction to be assertable by a test without reading any typed
  value.
- `TargetType` is `WorkspaceConnection`; `TargetId` is the row's id on success,
  and null on a refusal, where no row exists to name.
- **No email, no domain, no typed value in any row** (SC-11, PC-9). The refusal
  category is the whole explanation: `DomainMismatch`,
  `ImpersonationDomainMismatch`, `DomainNotConfirmed`, `ReadOnlyMode` — four new
  members of `AuditRefusalCategory`.
- Rows are never updated and are deleted only by the retention purge (SC-11,
  PC-11), which is not in this Story.
- The row carries the request id that ties it to the log line (SC-11).
- Merely observing a `DomainMismatch` writes nothing (I-5).

### FR-010 Authorization

`InstallationPolicies` gains `ConfigureWorkspaceConnection`, the permission-matrix
row "Настройка `WorkspaceConnection` (домен, impersonation)" — `✔` Admin, `✘`
Dean (`trebovaniya.md` §2). Both the page and the save endpoint declare it.

- A Dean is refused with `403` and the error page; an anonymous visitor is sent
  to sign in and never sees the page (SC-4).
- The SC-4 anonymous closed list is unchanged, and the endpoint enumeration test
  of US-008 passes unchanged.
- The matrix cell is implemented as written; no other cell is implemented
  speculatively (SC-1).

### FR-011 Localization

Every string this Story adds exists in `SharedResource.uk.resx` and
`SharedResource.en.resx` (NFR-073, US-008 FR-017): labels, the explanatory
sentences of FR-004, the confirmation, and every refusal message of §8. The
Ukrainian file is the default for a school that set no language.

The technical account's address and the school's domain are **data**: they are
rendered as stored and are never translated or re-cased for display.

A test fails on a key present in one file and missing from the other.

### FR-012 Persistence

- Table `workspace_connection`, snake_case, with explicit column mapping (PC-4,
  PC-5); columns `domain` (max 253) and `impersonation_user_email` (max 254),
  both `NOT NULL`.
- Check constraints: the singleton column; `char_length(domain) BETWEEN 3 AND
  253`; `char_length(impersonation_user_email) BETWEEN 3 AND 254`; the email
  contains exactly one `@` and no whitespace; both values equal their lower-cased
  form, so a row that bypassed the application cannot hold a mixed-case value.
- One EF Core migration creates it; no schema change outside a migration and no
  `EnsureCreated()` (PC-2, DC-4). The US-008 tables are untouched and the Control
  Plane schema gains nothing (AD-1).
- No index beyond the singleton unique index: no query uses either column as a
  lookup key (PC-7).
- The repository port `IWorkspaceConnectionRepository` lives in
  `Application/Ports` and is implemented in `Infrastructure` (AD-4); `DbContext`
  appears in neither `Application` nor `Web` (AD-3).

### FR-013 The admin panel

The landing page US-008 built gains a navigation entry leading to the connection
settings, visible to an Admin and not to a Dean (FR-010).

The entry point is defined once as a settings section, because US-010, US-011 and
US-012 each add a page next to this one. The section itself carries no business
rule and no state.

### FR-014 Logging

The sign-in log pattern of US-008 FR-020 continues:

- a saved or changed connection logs at `Information` with the actor's account id
  and the request id — never the domain and never the address;
- a refused save logs at `Warning` with the refusal category only;
- a rejected request body is not logged at all (SC-10);
- no log line carries the typed address, the domain, or any value from the form.

### FR-015 Wiring

The use case, the query, the repository and the request validation are registered
in the installation's composition root next to the US-008 registrations. The
`IUnitOfWork` the use case receives is the decorated one — the undecorated
registration stays unreachable, as the US-007 finding F-1 fix established
(US-008 FR-021).

No NuGet package is added by this Story. `Application` still references none and
`Domain` keeps zero package references (AD-3), and the existing structural tests
that assert this pass unchanged.

## 5. Acceptance Criteria

| Story AC | Satisfied by |
|---|---|
| AC-001 Only an Admin reaches the connection settings | FR-004, FR-005, FR-010 |
| AC-002 An unconfigured installation says so plainly | FR-002, FR-004 |
| AC-003 Saving a valid connection stores the domain and the technical account | FR-001, FR-005, FR-006 |
| AC-004 A domain the Owner did not approve is refused | FR-006, FR-007 |
| AC-005 A technical account outside the domain is refused | FR-006, FR-007 |
| AC-006 Malformed input is rejected before business logic | FR-005, FR-014 |
| AC-007 Changing the connection updates the one record | FR-001, FR-006, FR-012 |
| AC-008 Saving and changing are audited without personal data | FR-009 |
| AC-009 Read-only mode blocks the save and keeps the view | FR-004, FR-008 |
| AC-010 An installation that has never been legitimated cannot be configured | FR-002, FR-003, FR-006 |
| AC-011 Every new string is translated | FR-011 |
| AC-012 The schema change ships as a migration | FR-012 |

## 6. Validation Rules

Framework defaults are not relied on. Every rule is stated explicitly and is
enforced before the use case runs (`trebovaniya.md` §8).

### VR-001 The impersonation user's email

Required; trimmed; at most 254 characters; exactly one `@`; a non-empty name part
and a non-empty domain part; no whitespace anywhere; lower-cased before use. The
domain part must itself satisfy VR-002. Rejected with a per-field message; the
value is never logged.

This is the rule of US-008 VR-006 (the service-channel email) applied to a field
a person types, not the stricter `AllowedAdmin` rule of US-003: refusing a
technical account because its name part is long would refuse an address the
school legitimately created (I-3).

### VR-002 A domain value

Between 3 and 253 characters; at least two labels separated by dots; each label
1–63 characters of letters, digits and hyphens, not starting or ending with a
hyphen; no whitespace; one optional trailing dot, which is removed. Applied to
the domain part of VR-001 and to the value read from `LegitimacyState` before it
is written (FR-006).

An internationalised domain is accepted only in its ASCII (punycode) form, which
is what Google reports and what the Control Plane stores; no Unicode
normalisation is performed (I-4).

### VR-003 The save request

Exactly one field — the impersonation user's email. A missing body, a body that
is not a form, or a missing field is a `400`-class rejection rendered as the
page with its message. Extra fields are ignored and never bound; the domain is
not a field of this request (OD-001).

### VR-004 The comparison inputs

Both sides of every domain comparison (FR-007) are values that have passed
VR-002. A stored value that does not — impossible through this Story, possible
through a hand-edited database — makes the connection `DomainMismatch`, never a
crash and never a silent pass.

## 7. Security Requirements

| # | Requirement | Source |
|---|---|---|
| S-01 | The page and the save endpoint declare the Admin-only policy; a Dean gets `403`, an anonymous visitor is sent to sign in; both cases tested | SC-4, API-9, §2 |
| S-02 | BR-020 is enforced in `Application` on every save — the connection domain **and** the impersonation user's email domain against the `Installation` domain. A check that exists only in the UI, or only for the email, is a Critical finding | SC-9, BR-020 |
| S-03 | The allowed domain comes from `LegitimacyState`, never from the request, and is never guessed when no check has succeeded | SC-9, BR-020, §3 v54 |
| S-04 | The table holds the domain and the impersonation user and nothing else: no key, no secret, no reference to either, no client id. A column, DTO property, view field or form field carrying one is a Critical finding | SC-7, PC-9, §3 v33 |
| S-05 | The save is blocked in read-only mode by the guard, before any repository or transaction; `PermittedServiceWrite` gains no member and the save is not registered as a permitted write | SC-5, BR-026 |
| S-06 | Audit rows carry no email, domain or typed value; the actor is the Admin's account id and role; rows are never updated | SC-11, PC-9 |
| S-07 | A refused save is audited with its category, and the refusal message reveals nothing beyond the domain the installation may work with — which the Admin is entitled to see | SC-10, SC-11 |
| S-08 | Antiforgery covers the save through the global rule of US-008; the public-port exemption list stays empty | SC-4 |
| S-09 | Logs carry the account id, the request id and categories only — never the address typed, the domain, or the rejected body | SC-10, DC-10 |
| S-10 | No Google call, no Google port, no scope is added anywhere in this Story; nothing is sent to any destination outside the installation's own database | SC-8, SC-13, NFR-021 |
| S-11 | Nothing is sent to the Control Plane either: the connection is the school's own data and is never reported | SC-12, SC-13 |
| S-12 | Errors leak no internals: no stack trace, SQL, type name, path or configuration value | NFR-023, SC-10 |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Not signed in | redirect to the sign-in page (US-008) |
| Signed in as a Dean | `403` error page |
| Antiforgery missing or invalid | `400`; the "out of date" text of US-008 |
| Missing or malformed field (VR-001…VR-003) | the page, re-rendered with a per-field message; nothing written; nothing logged of the value |
| Impersonation user outside the `Installation` domain | the page with the refusal naming the allowed domain; audit `ImpersonationDomainMismatch` |
| Domain to be written differs from the `Installation` domain | the page with the same shape of refusal; audit `DomainMismatch` |
| No legitimacy check has ever succeeded | the page with "legitimacy not yet confirmed, the allowed domain is unknown"; audit `DomainNotConfirmed` |
| Read-only mode | `ReadOnlyModeException` → the US-008 mapping (`409` under `/api/v1`, error page elsewhere), reason named; audit `ReadOnlyMode` |
| Saved connection whose domain no longer matches | the page states it and asks for a new save; nothing written; no audit row (I-5) |
| Any other unhandled exception | `500` with no internals |

A refusal is an expected outcome and is returned as data; only the read-only
refusal is an exception, because it is one already (AD-9, US-007).

## 9. Non-Functional Requirements

- **NFR-073** — Ukrainian and English; no hard-coded user-visible string; the
  domain and the address are data and are not translated.
- **NFR-070** — the settings page works at phone width.
- **NFR-023** — no personal data in logs or in an HTTP error body.
- **NFR-021** — unaffected in substance: this Story adds no Google scope at all.
- **NFR-062 / coding conventions** — nullable enabled, warnings as errors, async
  methods end in `Async` and take a `CancellationToken`, constructor injection,
  one public type per file.
- **Testing** (TC-2, TC-3, TC-4, TC-5): no test calls Google, and none is needed;
  integration tests run against real PostgreSQL via Testcontainers; the page and
  the save endpoint each have an allowed-role and a forbidden-role test;
  read-only behaviour is proven in `Application`; the domain comparison rule is
  unit-tested directly.

## 10. Out of Scope

- The connection instructions for the school's super-admin, including the client
  id, the scope list and the technical account's required roles — **US-010**.
- "Check access" and the start-up self-check — **US-011**. Nothing here verifies
  the technical account against Google.
- Any Google API call, port, scope or credential use — EPIC-1 onwards. The
  US-007 finding F-5 stays open for US-011.
- The service-account key and its reference — the Owner's deployment
  configuration (DC-3, SC-7), never the UI and never the database.
- Dean accounts — **US-012**; choosing a personal UI language — **US-039**.
- Synchronization, `SyncState` and what a connection change means for already
  synced data — EPIC-1. This Story records the connection; no data exists yet to
  invalidate.
- Viewing the audit trail — EPIC-9; the retention purge — EPIC-10.
- Anything the Owner sees about a school's connection: the Control Plane is not
  told that a school saved its settings.
- Editing the `Installation` domain, which no screen of any installation offers
  (BR-021).

## 11. Open Decisions

Full text, options and resolutions in
`docs/decisions/US-009-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 Whether the Admin types the domain at all | **RESOLVED** by the Owner on 2026-09-20 (option 1) | none — FR-005, VR-003 and S-02 are written against the resolution |
| OD-002 A saved connection whose domain no longer matches | **RESOLVED** by the Owner on 2026-09-20 (option 1) | none — FR-002 and FR-004 are written against the resolution |

`trebovaniya.md` §7 has no open item this Story depends on (see the
`open_decisions` artifact). SPECIFICATION raised no new Open Decision.

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally. Each
is a decision this Specification makes explicit so it can be corrected at the
gate rather than discovered in code.

- **I-1** A **refused** save is audited. `trebovaniya.md` §5 names the action
  ("сохранение и изменение `WorkspaceConnection`"), and SC-11 gives every row an
  outcome of succeeded or refused; recording the refusal is therefore the same
  action with the other outcome, not a new audited action. It matters: a refused
  save is an attempt to point the program at a domain the Owner did not approve,
  which is precisely what this control exists to catch.
- **I-2** The first save and a change are **one** audit action
  (`WorkspaceConnectionSaved`) distinguished by the row it names, not two
  actions. §5 names them in one breath, and a second enum member would have to be
  invented.
- **I-3** The technical account's address is validated by the rule of US-008
  VR-006, not by the stricter `AllowedAdmin` rule of US-003: the account is
  created by the school's super-admin and its name part is not ours to constrain.
- **I-4** Internationalised domains are handled in ASCII (punycode) form only,
  because that is what the Control Plane stores and what Google reports. No
  Unicode normalisation is performed, and none is required by any artifact.
- **I-5** Observing a `DomainMismatch` writes no audit row. Audit records what a
  person or the system *did*; a mismatch is a state that is read, and writing a
  row each time the page is opened would fill the trail with duplicates and, in
  read-only mode, would write where nothing was attempted.
- **I-6** The domain is stored on the connection although it can only ever equal
  the `Installation` domain. Without the column the OD-002 mismatch is
  undetectable, and §3 describes the record as holding both values.
- **I-7** Saving the identical values is accepted and audited as a change rather
  than rejected as a no-op: the Admin performed the action, and refusing it would
  mean inventing a rule no artifact states.
- **I-8** The audit row of a read-only refusal is committed by declaring
  `PermittedServiceWrite.AuditEvent` around that one commit, while the connection
  write never happens. This uses the US-007 mechanism as designed and widens
  nothing; without it, a read-only refusal would be the only refusal with no
  trace.
- **I-9** No message rendered by this Story depends on the school time zone,
  which is still not a setting (US-008 I-12 continues to hold).
- **I-10** The settings section of FR-013 is presentation only. Where US-010,
  US-011 and US-012 place their pages is theirs to decide; this Story defines the
  section so the next three do not each invent one.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-004, FR-005, FR-010 | — | S-01, S-08 |
| AC-002 | FR-002, FR-004 | — | S-03 |
| AC-003 | FR-001, FR-005, FR-006 | VR-001, VR-003 | S-04, S-10, S-11 |
| AC-004 | FR-006, FR-007 | VR-002, VR-004 | S-02, S-03 |
| AC-005 | FR-006, FR-007 | VR-001, VR-002 | S-02 |
| AC-006 | FR-005, FR-014 | VR-001, VR-002, VR-003 | S-09, S-12 |
| AC-007 | FR-001, FR-006, FR-012 | — | S-04 |
| AC-008 | FR-009 | — | S-06, S-07 |
| AC-009 | FR-004, FR-008 | — | S-05 |
| AC-010 | FR-002, FR-003, FR-006 | VR-004 | S-03 |
| AC-011 | FR-011 | — | — |
| AC-012 | FR-012 | — | S-04 |
| — (admin panel entry, wiring) | FR-013, FR-015 | — | S-01 |

Requirement sources: `trebovaniya.md` v79 §2, §3, §4 (Epic 6), §5, §8, §9;
BR-015, BR-020, BR-021, BR-026, BR-079; NFR-021, NFR-023, NFR-062, NFR-070,
NFR-073; AD-1, AD-3, AD-4, AD-6, AD-8, AD-9; API-5, API-6, API-9, API-10; SC-1,
SC-4, SC-5, SC-7, SC-8, SC-9, SC-10, SC-11, SC-12, SC-13; PC-2, PC-3, PC-4,
PC-5, PC-6, PC-7, PC-8, PC-9, PC-11; DC-2, DC-3, DC-4, DC-10; TC-2, TC-3, TC-4,
TC-5.
