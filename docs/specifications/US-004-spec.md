---
artifact_type: specification
story: US-004
version: 1
status: APPROVED
created_at: 2026-09-17T10:46:26Z
updated_at: 2026-09-17T10:53:50Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-004-suspend-resume-installation.md
    version: null
  - path: trebovaniya.md
    version: 71
  - path: docs/decisions/US-004-open-decisions.md
    version: 1
supersedes: null
---

# US-004 Specification — Suspend and resume an Installation

## 1. Overview

The fourth Control Plane Story. On an installation's detail page (US-002) the
signed-in Owner changes the installation's status:

- suspending an active installation, through a confirmation step;
- resuming a suspended installation, through a confirmation step;
- a confirmed action that would not change the status changes and audits
  nothing, and tells the Owner the current status;
- two new Control Plane audit events;
- Ukrainian and English translations for everything shown.

Sources: `trebovaniya.md` v71 §2 (read-only mode), §3 (`Installation` and its
record rules, v69, v71), §4 (Epic 8, v71), §5 (Control Plane audit list), §8
(antiforgery, error page), §9 ("Три точки контроля Владельца", "Приостановка и
возобновление школы", v71); `security-conventions.md` SC-4, SC-10, SC-11, SC-12,
SC-13; `api-conventions.md` API-4, API-5; `architecture.md` AD-3, AD-8, AD-9;
`persistence-conventions.md` PC-2, PC-6, PC-9; `deployment-conventions.md` DC-7;
BR-021, BR-022, BR-023, BR-025, BR-079, BR-080; NFR-073. The US-001 host baseline
(deny-by-default, global antiforgery, setup gate, error page, session,
localization) and the US-002 installation list and detail page are reused, not
re-specified.

**There are no Open Decisions** (section 11). Behaviour not literally fixed by the
Story or `trebovaniya.md` is stated as interpretations I-1 … I-9 for review at
`HUMAN_SPEC_APPROVAL`.

## 2. Business Goal

The `Installation` status decides **whether a school works at all** — the third of
the Owner's control points (`trebovaniya.md` §9). It is the only lever that stops a
school; revoking an Admin does not (BR-022, BR-023). A suspended school goes to
read-only mode (BR-025) once the status reaches it. Resuming is also how a school
returning to its former domain gets back to work, because a domain never gets a
second `Installation` (BR-021). This Story delivers the lever in the Control
Plane; delivering the status to a school is US-005 (legitimacy check) and US-006
(push).

## 3. Business Flow

### 3.1 Suspending a school

1. On an active school's detail page the Owner chooses "suspend" (FR-001).
2. A confirmation page shows the school's name and domain and what suspending does
   (FR-002).
3. The Owner confirms; the status becomes "suspended" and the change is audited
   (FR-003, FR-006). The detail page now offers "resume".
4. Cancelling instead returns to the detail page with nothing changed.

### 3.2 Resuming a school

1. On a suspended school's detail page the Owner chooses "resume" (FR-001).
2. A confirmation page shows the school's name and domain and that the school
   returns to normal work (FR-002).
3. The Owner confirms; the status becomes "active" and the change is audited
   (FR-003, FR-006).

### 3.3 A stale page

1. The Owner suspended the school in one browser tab; another tab still shows the
   suspend confirmation.
2. Confirming there changes nothing and writes no audit row; the Owner lands on
   the detail page with a message stating that the school is already suspended
   (FR-004).

## 4. Functional Requirements

### FR-001 Status action on the detail page

The installation detail page of US-002 (FR-005 there) gains one status action:

- an **active** installation offers "suspend" and not "resume";
- a **suspended** installation offers "resume" and not "suspend";
- the action leads to the confirmation page of FR-002 (a GET link, not a
  state-changing request) *(interpretation I-1)*;
- the page keeps showing the current status as US-002 specified it — no reason
  and no time of the last status change (`trebovaniya.md` §3, v71);
- everything else on the detail page, including the Admins section of US-003, and
  the installations list stay as specified there; the list shows the stored status
  and offers no status action *(interpretation I-8)*.

### FR-002 Confirmation pages

**GET** — one confirmation page per action for one installation *(interpretation
I-1)*:

- **Suspend confirmation** — shows the installation's name and domain and the
  translated text (`trebovaniya.md` §9, v71): the school goes to read-only mode —
  viewing and export keep working, synchronization and configuration stop; no
  data is deleted; the Admins in the list stay.
- **Resume confirmation** — shows the installation's name and domain and the
  translated text: the school returns to normal work.
- Each page has a confirm button submitting FR-003 with the antiforgery token and
  a "cancel" link back to the detail page. Cancel sends no state-changing request.
- Opening a page changes nothing (API-4).
- If the installation is already in the status the action would set — for example
  the suspend confirmation of a suspended installation — the page is not shown:
  the Owner is redirected to the detail page with the FR-004 message
  *(interpretation I-3)*.
- Unknown installation or non-UUID route value: `404` (FR-007).

### FR-003 Changing the status

**POST** — one endpoint per action *(interpretation I-1)*; each step runs only if
the previous one passed:

1. **Antiforgery** — global validation of US-001 (FR-015 there). Missing or
   invalid token: `400`, error page "page expired"; nothing changed.
2. **Installation exists** — else `404` (FR-007).
3. **Change** — in one transaction in `ControlPlane.Services` (AD-3):
   - the status is set to the action's target ("suspended" for suspend, "active"
     for resume) **only if** the stored status is the other one;
   - if it was set: the audit row "installation suspended" or "installation
     resumed" (FR-006);
   - if the stored status already equals the target: nothing is written, audit
     included — the result is "unchanged" (FR-004).
   The decision is taken against the stored status inside the transaction (for
   example a conditional update whose affected-row count decides whether the
   audit row is written), not only by a check before the update (FR-005).
4. **Response** — after commit, redirect (`302`, post/redirect/get) to the
   installation's detail page; for "unchanged" the redirect carries the FR-004
   message *(interpretation I-2)*.

- Only the status changes. The identifier, name, domain, creation date and client
  ID are untouched; the installation's `AllowedAdmin` entries stay as they were
  (`trebovaniya.md` §9, v71; BR-080).
- No reason is accepted or stored; no field records the time of the status change
  (`trebovaniya.md` §3, v71). The technical `updated_at` column (PC-6) advances as
  on any update; it is not shown and is not a status-change time
  *(interpretation I-6)*.
- An installation may be suspended and resumed any number of times.
- Nothing is sent to the installation: no push, no call (US-005, US-006).

### FR-004 An action that changes nothing

- When the confirmed action's target equals the stored status (FR-003 step 3),
  including the same confirmation submitted twice: no change, no audit row, no
  error.
- The Owner lands on the detail page (`302` redirect, not `4xx`/`5xx`) with a
  translated message stating the installation's current status — "the
  installation is already suspended" or "the installation is already active"
  *(interpretation I-2)*.
- The message is shown once, on that page view; it is not stored in the database.

### FR-005 Concurrency

- Two submissions against the same installation that arrive at the same time are
  applied one after the other against the stored status; each writes an audit row
  only if it actually changed the status.
- Two identical submissions (both suspend): exactly one changes the status and
  writes one row; the other is "unchanged" (FR-004).
- Two opposite submissions (suspend and resume): the installation ends in one of
  the two statuses, and the number of audit rows equals the number of status
  changes actually made, each row matching its change (AC-006).
- No submission is answered `500` because of a concurrent one.

### FR-006 Audit events

The Control Plane `AuditEvent` table exists (US-001). This Story adds exactly:

| Event | Actor (id, role) | Target | Outcome |
|---|---|---|---|
| Installation suspended | Owner account id, `Owner` | `Installation`, its internal id | succeeded |
| Installation resumed | Owner account id, `Owner` | `Installation`, its internal id | succeeded |

- Every row carries UTC timestamp, actor, action, target, outcome and the request
  identifier (`trebovaniya.md` §5).
- **No** installation name or domain in any column (SC-11, PC-9).
- The row is written in the same transaction as the status change.
- Opening a confirmation page, cancelling, antiforgery refusal, `404` and
  "unchanged" write no row (AC-008).
- Rows are never updated or deleted; kept indefinitely (SC-11, US-001 FR-012).
- The audit row is the only trace of who changed the status and when
  (`trebovaniya.md` §3, v71).

### FR-007 Not found

- Every route of this Story is nested under the installation's UUID identifier
  (US-002 I-5).
- `404` with the translated error page (US-001 FR-016) when the identifier does
  not exist or is not a UUID — for both confirmation pages and both submissions.
- A `404` changes and audits nothing.

### FR-008 Authorization

- Every endpoint of this Story requires the signed-in Owner, declared by the Owner
  policy; none is anonymous and none is added to the SC-4 anonymous list or to the
  antiforgery exemption list (SC-4, API-9).
- A request without a session is redirected to the sign-in page; nothing is shown
  or changed (AC-009).
- While no Owner account exists, the US-001 setup gate redirects these endpoints
  to the setup page, like any other.
- The US-001 endpoint-enumeration test covers the new endpoints without a special
  case (TC-5). The Control Plane has one role, so the "forbidden role" case of
  TC-5 is the unauthenticated request.

### FR-009 GET safety and antiforgery

- Suspending and resuming are POST; GET shows the detail page and the confirmation
  pages only and changes nothing — opening a confirmation changes no status
  (API-4, AC-010).
- Antiforgery is the host's global validation; a missing or invalid token is `400`
  with the "page expired" error page.

### FR-010 Persistence

- No new entity and no new status value: the `installation.status` column with
  the closed set `active` / `suspended` exists (US-002 DB design).
- No new column: neither a reason nor a status-change time is stored
  (`trebovaniya.md` §3, v71).
- The status column is already updatable (US-002's immutability trigger covers
  identifier and domain only); whether this Story needs a migration at all —
  for example for the new audit action codes, if they are constrained in the
  database — is `db-designer`'s decision (PC-2).
- If the status change bypasses the change tracker (a conditional update), it
  must still set `updated_at` as PC-6 requires — mechanism: `db-designer`.
- New audit action codes extend the existing Control Plane audit vocabulary; the
  target type `Installation` exists (US-002).

### FR-011 Localization

- Every page, label, text, button, link and message of this Story comes from
  `ClassroomAgent.ControlPlane.Localization`, each key in Ukrainian and English
  (NFR-073, TC-8). Shown in the Owner's UI language, Ukrainian by default.
- The installation's name and domain are shown as stored, never translated,
  HTML-encoded by the view (AC-011).
- The status labels "active" / "suspended" reuse the US-002 translation keys.

### FR-012 Logging

- Log lines written by these actions carry internal identifiers only — never the
  installation's name or domain (SC-10, DC-10; `non-functional-requirements.md`).

## 5. Acceptance Criteria

Carried from the Story with the same ids; the Story is the authority for their
wording.

| AC | Title | Specified by |
|---|---|---|
| AC-001 | The detail page offers the action the status allows | FR-001 |
| AC-002 | Suspending asks for confirmation | FR-002 |
| AC-003 | The Owner suspends an Installation | FR-003 |
| AC-004 | Resuming asks for confirmation | FR-002 |
| AC-005 | The Owner resumes an Installation | FR-003 |
| AC-006 | An action that changes nothing writes nothing | FR-002 (redirect), FR-004, FR-005 |
| AC-007 | Unknown targets answer 404 | FR-007 |
| AC-008 | Suspending and resuming are audited | FR-006 |
| AC-009 | Only the Owner reaches these pages | FR-008 |
| AC-010 | State-changing forms are protected | FR-009 |
| AC-011 | Pages are translated | FR-011 |

## 6. Validation Rules

This Story accepts no user-entered field: no reason, no text.

### VR-001 Route identifier

| Rule | Value |
|---|---|
| format | a UUID in the route (US-002 I-5) |
| invalid | not a UUID, or no installation with that identifier |
| status | `404` (FR-007) — before any other check except antiforgery on POST |

### VR-002 Action

| Rule | Value |
|---|---|
| allowed values | exactly two actions, each its own endpoint: suspend, resume *(interpretation I-1)* |
| target status | suspend → `suspended`; resume → `active` |
| request body | none beyond the antiforgery token; any other posted field is ignored and never changes the status, name, domain or client ID |

### VR-003 Messages

- The FR-004 message and error pages are translated and display-safe — no stack
  trace, SQL, type name or internal detail (SC-10).
- No value submitted by the Owner is echoed or logged.

## 7. Security Requirements

| # | Requirement | Source |
|---|---|---|
| S-01 | Every endpoint requires the signed-in Owner; nothing added to the anonymous or antiforgery-exemption lists | SC-4, API-9 |
| S-02 | Suspending and resuming are POST with the global antiforgery token; GET — including the confirmation pages — changes nothing | SC-4, API-4 |
| S-03 | Only the status changes; no posted field can change identifier, name, domain, client ID or `AllowedAdmin` entries (over-posting) | BR-080, `trebovaniya.md` §9 v71 |
| S-04 | The "changed / unchanged" decision is taken against the stored status inside the transaction; concurrent submissions never produce an audit row without a change or a change without its row | SC-11, FR-005 |
| S-05 | Audit rows per FR-006: internal ids only, same transaction, never updated or deleted | SC-11, PC-9 |
| S-06 | Logs carry no installation name or domain | SC-10 |
| S-07 | Name and domain are HTML-encoded on every page | SC-10 (output safety), AD-8 |
| S-08 | Controllers and views see DTOs only; no `DbContext` outside `Persistence`/`Services` | AD-3, AD-8 |
| S-09 | Nothing is sent to an installation and nothing is added to `ClassroomAgent.Contracts` in this Story; no reference to `ClassroomAgent.Domain` | SC-12, AD-1 |
| S-10 | No outbound call of any kind | SC-13 |
| S-11 | Error responses carry no stack trace, SQL or type name; a concurrent submission never surfaces as `500` | SC-10, AD-9 |

## 8. Error Handling

| Situation | Status | What the Owner sees | Audit |
|---|---|---|---|
| Any endpoint without a session (Owner exists) | `302` | sign-in page | — |
| Any endpoint before setup | `302` | setup page (US-001) | — |
| Missing/invalid antiforgery token | `400` | error page "page expired" | — |
| Unknown or non-UUID installation identifier | `404` | error page "not found" | — |
| Confirmation page opened for an installation already in the target status | `302` | detail page with the "already …" message | — |
| Suspend or resume: success | `302` | detail page with the new status | suspended / resumed row |
| Suspend or resume: status already the target (repeat, stale page, lost race) | `302` | detail page with the "already …" message | — |
| Unhandled exception | `500` | error page "internal error" | — |

Expected outcomes (not found, unchanged) are results of `ControlPlane.Services`,
not exceptions (AD-9).

## 9. Non-Functional Requirements

- **NFR-073** — Ukrainian and English, Ukrainian by default (FR-011).
- **NFR-025** — audited actions in `AuditEvent`, never updated, kept indefinitely
  (FR-006).
- **NFR-026** — no path to teaching data (S-09).
- **NFR-032** — any schema change only through EF Core migrations, applied by
  deployment.
- **NFR-062** — .NET 10; `Nullable` enabled, warnings as errors.
- **Testability** — integration tests run against PostgreSQL via Testcontainers
  (TC-2); the concurrent cases of FR-005 are tested against the real database.
  No test needs an installation: nothing is delivered to one in this Story.

## 10. Out of Scope

- The legitimacy check, `InstanceLicenseCheck`, `LegitimacyState` and
  `ClassroomAgent.Contracts` (US-005).
- The push to the installation on a status change (US-006).
- Read-only mode enforcement in an installation (US-007).
- A reason for the change, a status-change time or a status history on the
  installation's page (`trebovaniya.md` §3, v71).
- Status actions on the installations list (I-8).
- Deleting an `Installation` (none in the first version).
- Viewing the audit log (EPIC-9); the Owner choosing their UI language (US-039).
- Anything in a school installation.

## 11. Open Decisions

Full text: `docs/decisions/US-004-open-decisions.md`.

**None.** The Story's own section records none — its questions were decided in
`trebovaniya.md` v71 — and `trebovaniya.md` §7 has no open item this Story depends
on (items 10 and 14 concern Google access and synchronization).

### Interpretations for human review

Behaviour not literally fixed by the Story or `trebovaniya.md`, derived from the
conventions cited and from the choices confirmed for US-002 and US-003. A
rejected interpretation becomes an Open Decision.

- **I-1** Each action has its own confirmation page (GET) and its own POST
  endpoint — suspend and resume are not one "toggle" endpoint. A toggle would turn
  a stale confirmation into the opposite action; with separate endpoints a stale
  submission can only be "unchanged". Same page pattern as the US-003 revoke
  confirmation, works without JavaScript.
- **I-2** An "unchanged" submission redirects (`302`) to the detail page with a
  one-time message, rather than answering `409`: the Story requires landing on the
  detail page and "not a server error", and the outcome the Owner wanted is
  already in place. The one-time message mechanism is API design.
- **I-3** Opening a confirmation page for an installation already in the target
  status (a stale link) does not show the confirmation; it redirects to the detail
  page with the same message as I-2.
- **I-4** A successful change shows no separate success banner: the detail page
  shows the new status and the opposite action, which is the confirmation of
  success (AC-003, AC-005).
- **I-5** The suspend confirmation text is the one fixed in `trebovaniya.md` §9
  v71; it does not mention that the status reaches the school only after US-005 /
  US-006 are delivered — that is a delivery detail, not something the Owner acts
  on.
- **I-6** The technical `updated_at` column advances on a status change as on any
  update (PC-6); it is not displayed and is not the "time of status change" that
  v71 excludes.
- **I-7** No extra confirmation such as typing the domain — the Owner chose a
  plain confirmation page (`trebovaniya.md` v71).
- **I-8** The installations list keeps showing the status and gets no status
  actions; suspending and resuming happen from the detail page only, as v71 states.
- **I-9** "Unchanged" is not audited even though the Owner confirmed an action:
  v71 says such an action writes nothing, audit included.

## 12. Traceability

| AC | Functional requirements | Validation rules | Security | Open Decisions |
|---|---|---|---|---|
| AC-001 | FR-001, FR-011 | — | S-01, S-07 | — |
| AC-002 | FR-002 | VR-001 | S-02, S-07 | — |
| AC-003 | FR-003, FR-010 | VR-001, VR-002 | S-03 | — |
| AC-004 | FR-002 | VR-001 | S-02, S-07 | — |
| AC-005 | FR-003, FR-010 | VR-001, VR-002 | S-03 | — |
| AC-006 | FR-002, FR-004, FR-005 | VR-003 | S-04, S-11 | — |
| AC-007 | FR-007 | VR-001 | — | — |
| AC-008 | FR-006 | — | S-04, S-05 | — |
| AC-009 | FR-008 | — | S-01 | — |
| AC-010 | FR-009 | — | S-02 | — |
| AC-011 | FR-011 | VR-003 | S-07 | — |

Requirements with no single AC but required by conventions the Story cites:
FR-010 (persistence — PC-2, PC-6), FR-012 and S-06 (SC-10), S-08 … S-10 (Story
Notes, AD-3, AD-8, SC-12, SC-13).
