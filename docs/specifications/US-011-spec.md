---
artifact_type: specification
story: US-011
version: 1
status: APPROVED
created_at: 2026-09-21T07:48:44Z
updated_at: 2026-09-21T07:54:55Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-011-check-access.md
    version: null
  - path: trebovaniya.md
    version: 79
  - path: docs/decisions/US-011-open-decisions.md
    version: 1
supersedes: null
---

# US-011 Specification — Check access diagnostic

## 1. Overview

US-009 recorded *which* technical account the installation reads the school's
data as, and US-010 printed what the school's super-admin must configure. Neither
touched Google. This Story does, for the first time: "Проверить доступ" makes real
calls as the technical account and says, check by check, what works and what does
not (`trebovaniya.md` §4, Epic 6). The same checks run on their own at every start
and write their outcome to the log, which is how the Owner confirms a rotated key
without involving the school (§9 "Ротация ключей", DC-5).

Five properties shape everything below.

- **Only reads, and only the six scopes.** Every token is requested for one of the
  six read-only scopes of §6, taken from the constant US-010 placed in
  `Domain/Rules/GoogleDelegationScopes`. Nothing is written to Google — a Hard
  Stop — and no other scope is ever requested (SC-8, NFR-021).
- **The answer is used, never kept.** The two data reads return real school data
  (a course, a Meet event). It decides success or failure and is then discarded:
  never shown, stored or logged. The check result itself is not stored either
  (OD-003); it lives on the screen for the Admin who ran it and in one log line
  for the self-check.
- **Read-only mode means no call at all.** In read-only mode the action is refused
  and the self-check does not run; not even a token request leaves the process
  (BR-026, SC-5). This is the first Story where that rule is enforced rather than
  structurally true, and it is proven in `Application` with the Google port
  substituted (the carried US-007 finding F-5).
- **The key never leaves the process.** The service-account key is read through
  `ISecretStore` from a reference in configuration, used to sign token requests
  and dropped when the check ends. It is not in the database, a DTO, a view, a
  log, an error message or an audit row (SC-7, a Hard Stop).
- **A failure is a diagnosis, not an exception text.** Every failure is mapped to
  a closed set of causes the Admin (or, for the key, the Owner) can act on, and
  rendered from the translation files. Google's own error text never reaches the
  screen or the log (§4, SC-8, SC-10).

## 2. Business Goal

A delegation mistake today surfaces first as a failed synchronisation — the worst
place to diagnose it, because by then the Admin is waiting for data, not setting
up. The check moves the discovery to the moment of setup, and names the fix: which
scope the super-admin forgot, whether the technical account exists and can read,
or whether the problem is the Owner's (a missing key, an API not enabled in the
Owner's Cloud project).

For the Owner the startup self-check closes the gap in the rotation procedure
between "new key placed" and "old key deleted" (DC-5): the old key is deleted only
after a log line confirms the new one works.

## 3. Business Flow

### 3.1 After onboarding

The super-admin has followed the US-010 instruction; the Admin has saved the
connection (US-009). The Admin opens "Проверить доступ" in the settings section and
starts the check. Within seconds the page lists the six scopes and the two reads,
each with its outcome. All succeed: the page says access is in place. One audit row
records that the Admin ran the check.

### 3.2 A scope was forgotten

The super-admin authorised five of the six scopes. The page shows five successes,
names the sixth as not authorised in domain-wide delegation, and tells the Admin to
ask the super-admin to add exactly that scope — the full URI, as the instruction
prints it. The read that needs it is shown as not attempted.

### 3.3 The problem is not the school's

The key is not in the secret store, or the Classroom API is not enabled in the
Owner's Cloud project. The page says the problem must be fixed by the service Owner,
without revealing anything about the key or its reference. The school's super-admin
is not sent on a pointless errand.

### 3.4 Read-only mode

The installation is suspended, past its grace period, or never legitimated. The
Admin starts the check and is refused with the read-only reason, as every blocked
action is refused (`409`). No call leaves the process. The audit row records the
refused attempt.

### 3.5 No usable connection

No connection has been saved, or the saved one no longer matches the
`Installation` domain (US-009 OD-002). The check is refused with a plain statement
of what to do first; no call is made.

### 3.6 Key rotation

The Owner places a new key under the same reference and restarts the installation
outside teaching hours. The self-check runs, performs the same checks, and writes
one line: success at `Information`, failure at `Error`. The Owner reads it and only
then deletes the old key. If the installation is in read-only mode, or has no saved
connection, the line says the self-check was skipped and why (OD-004), so the Owner
does not mistake silence for success.

### 3.7 A Dean

The action is not offered to a Dean, and a Dean who requests it directly is refused
with `403`.

## 4. Functional Requirements

### FR-001 What one check consists of

A check is a fixed sequence of **eight steps**, in this order:

1. six **delegation steps**, one per scope of `GoogleDelegationScopes.All`, in the
   order of that list (FR-002);
2. the **Classroom read** (FR-003);
3. the **Admin Reports read** (FR-003).

Its result is a value produced in `Application` (a DTO, AD-8) carrying:

- one entry per step, with the step's identity (the scope URI for a delegation
  step; which API for a read) and its **outcome** from the closed list of FR-005;
- an **overall verdict**: `AccessInPlace` only when all eight steps succeeded;
  otherwise `NotConfigured` (at least one step failed for a configuration cause)
  or `Inconclusive` (no configuration failure, but at least one step could not be
  completed because Google did not answer or was unavailable);
- the technical account's address and the school's domain that were checked, as
  data (they are the values the Admin saved in US-009 and is entitled to see).

It carries **nothing Google returned**: no course, no event, no identifier from a
response, no Google error text.

### FR-002 The delegation steps

For each of the six scopes, separately, the program requests a delegated access
token from Google's token endpoint:

- signed with the service-account key (FR-016), **for that single scope**, with
  the technical account of the `WorkspaceConnection` as the impersonated subject
  (BR-015, BR-031) — never the Admin who pressed the button and never a
  super-admin (SC-8);
- the token proves the scope is authorised for this service account's client ID
  in the school's domain-wide delegation (OD-002). It is used for at most the one
  read of FR-003 that needs it and then discarded; no token is stored, logged,
  cached beyond the check, or returned to the browser.

A step whose token is issued is `Succeeded`. A refusal is classified by FR-005.

Some failures are not about one scope — the key is unusable, the technical
account does not exist. When the first delegation step fails with such a
**run-wide cause**, the check does not repeat the same failure six times: that
step carries the cause and every remaining step is `NotAttempted` with the same
cause referenced (FR-005).

### FR-003 The two reads

- **Classroom read** — one request for the list of courses, asking for at most
  one course, with the token of `classroom.courses.readonly` only.
- **Admin Reports read** — one request for the Meet activity of the audit log
  (application `meet`, all users), asking for at most one event, with the token of
  `admin.reports.audit.readonly` only.

Rules for both:

- a read runs only if the delegation step of its scope succeeded; otherwise it is
  `NotAttempted`;
- **an empty answer is success**: a domain with no courses or no Meet events passes
  when access is in place;
- the response body is inspected for nothing but success; no field of it is copied
  into the result, a log line or an audit row (SC-10, SC-12);
- a read never pages, never retries a permission failure (SC-8), and requests
  nothing beyond the first page of size one.

### FR-004 The Google port

A new port in `Application/Ports`, marked `IGoogleDataPort` (US-007 FR-007), with
one operation per kind of step: request a delegated token for one scope; perform
the Classroom read; perform the Admin Reports read. It accepts and returns only
`Application` types — plain strings for scope and address, a step-outcome value —
and **no Google SDK type crosses it** (AD-4). The implementation lives in
`Infrastructure` and is the only code that references the Google packages of
FR-015.

The port is the substitution point for every test (TC-4): no test uses a real key
or reaches a live Google endpoint.

### FR-005 Step outcomes and causes

Every step ends in exactly one outcome. The list is closed; the `Infrastructure`
implementation maps Google's answers onto it, and presentation maps each onto a
translated message (FR-012).

| Outcome | Kind | Meaning | Who acts |
|---|---|---|---|
| `Succeeded` | — | token issued / read answered | — |
| `ScopeNotAuthorized` | configuration, per scope | Google refuses the token for this scope: it is not authorised in domain-wide delegation for this client ID | super-admin |
| `TechnicalAccountUnknown` | configuration, run-wide | the impersonated address does not exist or cannot be impersonated in the domain | Admin / super-admin |
| `TechnicalAccountCannotRead` | configuration, per read | the token was issued but the API refuses the read for this account (it lacks the rights for Classroom or Admin Reports) | super-admin |
| `ApiNotEnabled` | configuration, per read | the API is not enabled in the Owner's Cloud project | Owner |
| `KeyUnavailable` | configuration, run-wide | no reference is configured, the store holds nothing under it, or what it holds is not a usable service-account key | Owner |
| `KeyRejected` | configuration, run-wide | Google rejects the key's signature (the key was deleted or disabled) | Owner |
| `GoogleUnavailable` | transient | no answer within the time limit (I-2), a network failure, or a `5xx` / `429` from Google | retry later |
| `NotAttempted` | — | an earlier step made this one impossible; the entry references the cause | — |

- Permission and configuration outcomes are **never retried** (SC-8, AD-5).
- `GoogleUnavailable` is **not** retried within a check either: the check is
  interactive and bounded (I-2); the Admin can run it again (OD-005).
- A Google answer the implementation cannot classify is reported as
  `GoogleUnavailable` for that step — an unrecognised answer must not be passed off
  as a configuration diagnosis — and logged at `Error` with the category only.
- The message for a `ScopeNotAuthorized` step names the scope by its **full URI**,
  exactly as the US-010 instruction prints it, so the super-admin can paste it.
- Owner-side outcomes (`KeyUnavailable`, `KeyRejected`, `ApiNotEnabled`) say that
  the service Owner must act and reveal nothing about the key, its reference, the
  store or the Cloud project (SC-7, SC-10).

### FR-006 The check-access use case

`RunAccessCheckUseCase` (name indicative; API_DESIGN and implementation may refine
it) in `Application/UseCases`, in this order:

1. **Read-only guard first** — `IReadOnlyModeGuard.EnsureAllowedAsync` before any
   repository, port or secret is touched (US-007 FR-002, AD-6). The use case holds
   an `IGoogleDataPort` and therefore takes the guard in its constructor — the
   structural rule of US-007 AC-007.
2. **Read the connection state** through the US-009 query (US-009 FR-002). A state
   that must not be used — `NotConfigured`, `DomainMismatch`, `DomainUnknown` —
   refuses the check with an outcome naming it (§8); no Google call is made. The
   use case does not re-derive the rule.
3. **Run the eight steps** of FR-001 through the port, sequentially, within the
   time limit of I-2, honouring the request's `CancellationToken`.
4. **Write the audit row** of FR-008.
5. **Return the result** of FR-001. Every refusal of steps 2 and every step failure
   is an **outcome, never an exception** (AD-9); only the read-only refusal is the
   existing `ReadOnlyModeException`, which the host already maps to `409`
   (US-008 FR-014).

There is no cooldown step (OD-005) and nothing is persisted except the audit row
(OD-003).

### FR-007 The action in the admin panel

In the settings section established by US-009 (US-009 FR-013), next to the
connection settings and the instruction:

- a **page** (`GET`, Admin only) that says what the check does in one or two
  sentences — it calls Google as the technical account, reads nothing it keeps, and
  is also run automatically at every start — and offers the action; in read-only
  mode it states the reason, as the other admin screens do;
- the **action** (`POST`, Admin only, with the antiforgery token under the global
  rule of US-008 FR-005, gaining no exemption) that runs FR-006 and renders the
  result of FR-001 on the page: one line per step with its outcome and message, and
  the overall verdict above them.

A `GET` never runs the check. The action is not hidden and not disabled to enforce
read-only mode; enforcement is FR-006 step 1 (AD-6, TC-5).

`api-conventions.md` API-3 already names `POST /api/v1/workspace-connection/test`
for this check, while the settings screens of US-009 and US-010 are server-rendered
pages. **API_DESIGN reconciles the two** (I-10): it defines one `POST` that runs the
check, and at most the page `GET`; it adds no other operation.

### FR-008 Audit

`AuditAction` gains one member for running the check (`AccessCheckRun`, name for
DB_DESIGN to confirm) — `trebovaniya.md` §5 "запуск «Проверить доступ»", SC-11.

| Row | When | Actor | Outcome | Target |
|---|---|---|---|---|
| check carried out | FR-006 reached step 4 — whatever the steps found | the Admin's `AppUser` id and role | `Succeeded` | `WorkspaceConnection`, its id |
| refused: read-only | FR-006 step 1 | the Admin's `AppUser` id and role | `Refused`, category `ReadOnlyMode` (existing) | `WorkspaceConnection` id if a row exists, else null |
| refused: no usable connection | FR-006 step 2 | the Admin's `AppUser` id and role | `Refused`, a new category for an unusable connection | as above |

- `Succeeded` means the check **was carried out**, not that access is in place
  (I-4). The findings are not recorded: the row has no column for them and gains
  none (OD-003).
- The read-only refusal row is written as US-009 FR-008 established: after the
  guard has thrown, around that commit alone, declaring
  `PermittedServiceWrite.AuditEvent` — audit rows are on the BR-026 list.
  `PermittedServiceWrite` keeps exactly its four members.
- **No email, no domain, no scope, no Google data in any row** (SC-11, PC-9).
- The row carries the request id (SC-11).
- The self-check writes **no** audit row: it is not a user action, and §5 lists
  the user-started check.

### FR-009 Read-only mode

Running the check is blocked in read-only mode (BR-026 names "check access"). In
that mode:

- the action answers `409` with the read-only reason through the existing handler
  (US-008 FR-014, API-5, SC-5); this Story adds no second mapping;
- **no call reaches Google** — no token request, no read. The guard runs before the
  port, the connection and the secret store are touched (FR-006 step 1);
- the page itself stays viewable (a `GET` writes nothing and calls nothing);
- the self-check does not run (FR-010).

Proven in `Application` with the port substituted, for all three BR-025 causes: the
substitute records zero calls (TC-5, TC-4). This closes the carried US-007
security-review finding F-5 for this port.

### FR-010 The startup self-check

At every start of the installation host, once:

- it runs **after** the host has started, in the background, and never delays or
  prevents the start; a failing self-check does not stop the installation (AC-008);
- it decides read-only mode through the same guard as every use case, from the
  stored `LegitimacyState` (I-7); in read-only mode it does not run;
- it reads the connection state as FR-006 step 2 does; with no usable connection it
  does not run;
- otherwise it performs **the same eight steps** through the same port and the same
  classification as FR-001…FR-005 — one implementation, not a copy;
- it writes exactly **one** summary line (DC-10): `Information` when the verdict is
  `AccessInPlace`, `Error` otherwise, carrying the verdict and each step's
  outcome by scope and API name — the outcome categories and scope URIs are not
  personal data;
- when it does not run, it writes one line saying it was **skipped and why** — read-
  only mode (with the BR-025 cause) or no usable connection (with the connection
  state) — at `Warning` (OD-004, I-6);
- it writes no audit row, no table, and shows nothing to a user.

### FR-011 Authorization

`InstallationPolicies` gains a policy for the matrix row "Проверить доступ" —
`✔` Admin, `✘` Dean (`trebovaniya.md` §2, v39). Both the page and the action declare
it. It is **its own policy**, not `ConfigureWorkspaceConnection` or
`ViewConnectionInstruction`: §2 keeps the three rows apart, and the read-only mode
treats them differently (I-9).

- A Dean is refused with `403`; an anonymous visitor is sent to sign in (SC-4).
- The SC-4 anonymous closed list gains nothing; the endpoint enumeration test of
  US-008 passes with the new endpoints classified as protected.
- No other matrix cell is implemented speculatively (SC-1).

### FR-012 Localization

Every string this Story adds exists in both `SharedResource.uk.resx` and
`SharedResource.en.resx` (NFR-073): the page text, the action label, the three
verdicts, one message per outcome of FR-005 (per-scope and per-read variants where
the wording differs), and the refusal messages of §8. The Ukrainian file is the
default.

Scope URIs, the technical account's address and the domain are **data**: rendered as
they are, never translated. Log lines are not translated (they are for the Owner and
carry categories, not sentences).

A test fails on a key present in one file and missing from the other.

### FR-013 Persistence

- **No new table, no new column** (OD-003). Nothing about a check's result is
  stored anywhere.
- `AuditAction` and `AuditRefusalCategory` each gain one member. If the audit table
  constrains those columns, the constraint is amended by an EF Core migration in
  this Story (PC-2), as US-009 did for `WorkspaceConnectionSaved`; DB_DESIGN decides
  whether one is needed and names it.
- No column that could hold the service-account key, its reference, a token or any
  Google response is added (SC-7, PC-9).
- `DbContext` appears in neither `Application` nor `Web` (AD-3).

### FR-014 Logging

- The self-check logs as FR-010.
- A check run by the Admin logs one line at `Information` with the actor's account
  id, the request id and the verdict; a failed step whose outcome is a
  configuration cause logs at `Warning` with the step and the category; an
  unclassifiable Google answer logs at `Error` with the category only (FR-005).
- A refused run logs at `Warning` with the refusal category only.
- **No log line carries** the key, its reference, a token, the technical account's
  address, the domain, Google's error text, or anything from a Google response
  (SC-7, SC-10, DC-10).

### FR-015 Packages and wiring

- `ClassroomAgent.Infrastructure` gains the three packages of OD-001 —
  `Google.Apis.Auth`, `Google.Apis.Classroom.v1`,
  `Google.Apis.Admin.Reports.reports_v1` — on the **1.76 line**, the newest stable
  on nuget.org when this Specification was written (1.76.0; Classroom 1.76.0.4254;
  Reports 1.76.0.4252). IMPLEMENTATION takes the newest stable patch of that line at
  the time and records the exact versions; `dotnet list package --vulnerable` must
  be clean (SECURITY_REVIEW).
- No other project gains a package. `Application` and `Domain` keep zero Google
  references, and the existing structural tests asserting the package and project
  reference rules pass unchanged (AD-3, AD-4).
- The use case, the port implementation, the self-check and the new policy are
  registered in the installation's composition root next to the US-009/US-010
  registrations. The `IUnitOfWork` the use case receives is the decorated one
  (US-008 FR-021).

### FR-016 The service-account key setting

- A new installation setting holds the **secret-store reference** of the
  service-account key (DC-3 lists it among an installation's settings; its name is
  fixed by API_DESIGN/IMPLEMENTATION in the style of
  `GoogleOAuth:ClientSecretReference`). The reference is resolved through the
  existing `ISecretStore` (US-008 OD-004: an environment variable), and what it
  resolves to is the service-account key in Google's JSON key format.
- The setting is **not required for the start** (I-1): DC-3 and §5 list the settings
  without which an installation refuses to start, and this one is not among them. A
  missing or unresolvable reference, or content that is not a usable key, is the
  outcome `KeyUnavailable` of FR-005 — for the check and for the self-check — and the
  school's viewing and export keep working.
- The key is resolved **per check** and held only for its duration; nothing about it
  is cached in a static field (AGENTS.md "Coding Conventions").
- Neither the reference nor the key is ever logged, rendered or stored (SC-7).

## 5. Acceptance Criteria

The Story's ten criteria, unchanged in meaning; ids are the Story's.

| Id | Criterion | Specified by |
|---|---|---|
| AC-001 | Only an Admin can run the check | FR-007, FR-011 |
| AC-002 | Each of the six scopes is checked on its own | FR-002, FR-004 |
| AC-003 | One real read from each API proves the technical account can read | FR-003 |
| AC-004 | A failure says what is not configured | FR-005, FR-012 |
| AC-005 | Without a saved connection there is nothing to check | FR-006 step 2, §8 |
| AC-006 | In read-only mode no call reaches Google | FR-006 step 1, FR-009 |
| AC-007 | Every run is audited without personal data | FR-008 |
| AC-008 | The self-check runs at every start and writes only to the log | FR-010, FR-014 |
| AC-009 | Tests never reach Google | FR-004, TC-4 |
| AC-010 | Every string is translated | FR-012 |

## 6. Validation Rules

### VR-001 The action carries no input

The `POST` carries the antiforgery token and nothing else: the technical account,
the domain and the scopes are read server-side (FR-006). A value submitted under any
field name is ignored — the request model has no property to bind it to. A missing
or invalid antiforgery token is `400` under the global rule (API-7).

### VR-002 The scope list

Only `GoogleDelegationScopes.All` is ever passed to the port, one scope per token
request. A test asserts that the set of scopes the substituted port received during a
full check equals that list exactly — six, in order, none repeated, no other.

### VR-003 The technical account

The address used as the impersonated subject is the one stored in the usable
`WorkspaceConnection` (US-009 VR-001 validated it when saved). It is not re-typed,
not taken from the request and not taken from the Admin's session.

### VR-004 The secret-store reference

Trimmed; empty or whitespace counts as absent (→ `KeyUnavailable`). The resolved
content counts as a usable key only if it parses as a service-account key of Google's
JSON format with a private key and a client email; anything else → `KeyUnavailable`.
The content is never echoed, even in part.

### VR-005 Data returned by Google

Validated only for success (SC-10, AGENTS.md "Security Policy": data returned by
Google is external input). Nothing from it is bound to a model the program keeps.

### VR-006 Every rendered value is encoded

The technical account's address, the domain and the scope URIs are HTML-encoded as
data; no value is rendered as markup.

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | The page and the action declare their own Admin-only policy; anonymous access is impossible and the SC-4 closed list gains nothing. | SC-4, §2 |
| S-02 | A Dean is refused (`403`); allowed-role and forbidden-role cases are tested. | SC-1, TC-5 |
| S-03 | In read-only mode no call reaches Google — not even a token request — proven in `Application` with the port substituted, for all three BR-025 causes. | SC-5, BR-026, US-007 F-5 |
| S-04 | Only the six read-only scopes of §6 are ever requested, one per token request; never `drive.file`, `classroom.profile.photos`, an identity scope or any write scope. | SC-8, NFR-021, VR-002 |
| S-05 | The impersonated subject is the technical account of the usable `WorkspaceConnection`, never the Admin and never a super-admin; the Admin's OAuth session is never used for a Google data call. | SC-8, BR-015, BR-031 |
| S-06 | Nothing is written to Google. | Hard Stop, BR-030 |
| S-07 | The service-account key and its reference appear in no response, view, DTO, log line, audit row, table or exception message; tokens likewise. | SC-7 (Hard Stop), PC-9 |
| S-08 | Nothing Google returns — course, event, identifier, error text — reaches a response, a log line, an audit row or the database. | SC-10, SC-12 |
| S-09 | Permission and configuration failures are never retried. | SC-8, AD-5 |
| S-10 | The action is a `POST` with the antiforgery token; a `GET` runs nothing. | SC-4, API-7 |
| S-11 | The audit row of every run and every refusal carries no personal data. | SC-11 |
| S-12 | The only outbound destination this Story adds calls to is Google, already on SC-13's list; no other service is contacted. | SC-13 (Hard Stop) |
| S-13 | The three Google packages are added to `Infrastructure` only and are free of known vulnerabilities at SECURITY_REVIEW. | OD-001, AGENTS.md |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous request | Redirected to sign-in (SC-4). |
| Signed-in Dean | `403` with the error page. |
| Missing or invalid antiforgery token | `400` under the global rule (API-7). |
| Read-only mode | `409` with the read-only reason (US-008 FR-014); audit row `Refused` / `ReadOnlyMode`; no Google call. |
| No connection saved, or the saved one unusable (`DomainMismatch`) | The check is refused as a state conflict — the answer shape (`409` under `/api/v1`, or the page re-rendered with the message) is fixed by API_DESIGN within API-5; the message says what to do first (save the connection); audit row `Refused`; no Google call. |
| Any step failure (FR-005) | **Not an error**: the check completed and the page shows the result with a message per failed step. |
| Google does not answer in time | Not an error: the affected steps are `GoogleUnavailable` and the verdict is `Inconclusive`. |
| The request is aborted by the browser | The remaining calls are cancelled; no audit row is required for a run that did not reach FR-006 step 4. |
| An unexpected failure | The single exception handler answers the error page with no detail (AD-9, API-10, SC-10). |

There is no rate-limit refusal (OD-005).

## 9. Non-Functional Requirements

- **NFR-021 / SC-8** — every requested scope is read-only, asserted by VR-002.
- **NFR-073** — every message translated (FR-012).
- **NFR-023 / SC-10** — no Google data and no secret in a log (FR-014).
- **NFR-062** — .NET 10, C#, nullable enabled, warnings as errors.
- **NFR-070** — the result is readable on a phone: one line per step.
- **Bounded** — a check finishes within the time limit of I-2 whatever Google does;
  eight calls at most per run.

## 10. Out of Scope

- Synchronisation of any data — US-013 and later; the check reads one course and one
  event at most, and keeps neither.
- Storing a check result or showing a history of checks (OD-003).
- Limiting how often the check runs (OD-005).
- Verifying the technical account's minimum Workspace roles — `trebovaniya.md` §7
  item 10 stays open (I-8).
- Changing the connection (US-009), the instruction (US-010), Dean accounts (US-012).
- Anything in the Control Plane, the service channel or `ClassroomAgent.Contracts`.
- Comparing the key's own client ID with the one in `LegitimacyState`: no artifact
  defines it, so a key of another school surfaces as `ScopeNotAuthorized` on every
  scope, not as a dedicated diagnosis.

## 11. Open Decisions

Full text, options and resolutions in `docs/decisions/US-011-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 Which library the program uses to call Google | **RESOLVED** 2026-09-21 (option 1) | none — FR-004, FR-015 written against it |
| OD-002 How the check finds out which scope is missing | **RESOLVED** 2026-09-21 (option 1) | none — FR-001…FR-003 |
| OD-003 Whether the result of a check is stored | **RESOLVED** 2026-09-21 (option 1) | none — FR-008, FR-013 |
| OD-004 What the self-check logs when it cannot run | **RESOLVED** 2026-09-21 (option 1) | none — FR-010 |
| OD-005 Whether repeated runs are limited | **RESOLVED** 2026-09-21 (option 1) | none — FR-006 |

`trebovaniya.md` §7 item 10 stays open and is touched, not closed (I-8). SPECIFICATION
raised no new Open Decision.

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally; each can
be corrected at `HUMAN_SPEC_APPROVAL`.

- **I-1 The key reference is not a start-up requirement.** §5 and DC-3 enumerate the
  settings without which an installation refuses to start, and the service-account
  key reference is not among them — unlike the OAuth client secret reference (v78).
  Making it one would take a school's viewing and export down for a problem that only
  affects Google access. It is therefore diagnosed, not enforced at start: the check
  and the self-check report `KeyUnavailable`, and the self-check's `Error` line is
  what the Owner reads.
- **I-2 Time limit.** One check is bounded by **30 seconds** in total — the same
  limit `ControlPlaneClient.CallLimit` uses for an outbound call the user waits on.
  Steps run sequentially; when the limit is reached, the step in progress and every
  later step are `GoogleUnavailable`. Sequential rather than parallel so that a
  run-wide cause found in the first step stops the rest (FR-002).
- **I-3 The minimal reads.** The Classroom read is the course list with a page size
  of one; the Admin Reports read is the Meet activity list for all users with at most
  one event. They were chosen because each needs exactly one scope and no
  precondition in the domain (no course id), so an empty domain still gives an answer.
- **I-4 What `Succeeded` in the audit means.** The action audited is "запуск" — the
  running of the check (§5). The row records that it ran, not what it found; recording
  findings would need a column, which OD-003 rules out.
- **I-5 Run-wide causes stop early.** `KeyUnavailable`, `KeyRejected` and
  `TechnicalAccountUnknown` are the same for every scope; repeating the token request
  six times would give six identical failures and six calls for nothing.
- **I-6 The skip line is `Warning`.** DC-10 lists `Information` for a successful
  self-check and `Error` for a failed one. A skip is neither; `Warning` is what makes
  it visible to an Owner scanning for problems after a rotation, which is the reason
  OD-004 exists.
- **I-7 The self-check does not wait for a legitimacy check.** It decides read-only
  mode from the stored `LegitimacyState`, as every use case does. A key-rotation
  restart of a healthy school therefore runs it at once; a never-legitimated school
  skips it with the read-only reason.
- **I-8 What the check cannot prove.** The Classroom course list answers with the
  courses the technical account itself can see. An empty answer is success (AC-003),
  so the check proves the account may call the API, not that it sees every course;
  and no check proves the account holds nothing beyond read access. Both belong to
  §7 item 10, which stays open. No message names a Workspace admin role (US-010
  OD-001).
- **I-9 Its own policy.** As US-010 I-7: one policy per matrix row, because §2 keeps
  "Настройка `WorkspaceConnection`", "Просмотр инструкции" and "Проверить доступ"
  apart and read-only mode treats them differently.
- **I-10 One `POST`.** API-3 names `POST /api/v1/workspace-connection/test`; the admin
  panel is server-rendered. API_DESIGN decides whether the check is posted to the
  settings page, to the `/api/v1` path by script, or both — but it is one operation,
  and every shape it chooses follows API-5 and API-7.
- **I-11 An unusable connection refuses.** US-009 OD-002 defined `DomainMismatch` and
  `DomainUnknown` as "must not be used" precisely so that this Story reads them as
  absent instead of inventing its own check.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-007, FR-011 | VR-001 | S-01, S-02, S-10 |
| AC-002 | FR-001, FR-002, FR-004 | VR-002, VR-003 | S-04, S-05 |
| AC-003 | FR-003 | VR-005 | S-06, S-08 |
| AC-004 | FR-005, FR-012 | VR-004, VR-006 | S-07, S-09 |
| AC-005 | FR-006 | VR-003 | S-11 |
| AC-006 | FR-006, FR-009 | — | S-03 |
| AC-007 | FR-008, FR-013 | — | S-11 |
| AC-008 | FR-010, FR-014, FR-016 | VR-004 | S-03, S-07, S-08 |
| AC-009 | FR-004 | — | — |
| AC-010 | FR-012 | VR-006 | — |
| — (packages, wiring, key setting) | FR-015, FR-016 | VR-004 | S-12, S-13 |

Requirement sources: `trebovaniya.md` v79 §1, §2, §4 (Epic 6), §5, §6, §7 (item 10,
open), §9; BR-015, BR-020, BR-025, BR-026, BR-030, BR-031, BR-032; NFR-021, NFR-023,
NFR-062, NFR-070, NFR-073; AD-3, AD-4, AD-5, AD-6, AD-8, AD-9; API-3, API-5, API-7,
API-10; SC-1, SC-4, SC-5, SC-7, SC-8, SC-10, SC-11, SC-12, SC-13; PC-2, PC-9; DC-3,
DC-5, DC-10; TC-4, TC-5.
