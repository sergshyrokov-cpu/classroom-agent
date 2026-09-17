---
artifact_type: specification
story: US-006
version: 2
status: APPROVED
created_at: 2026-09-17T15:31:06Z
updated_at: 2026-09-17T15:37:50Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-006-control-plane-push.md
    version: null
  - path: trebovaniya.md
    version: 77
  - path: docs/decisions/US-006-open-decisions.md
    version: 2
supersedes: null
---

# US-006 Specification — Control Plane push on status change

## 1. Overview

The push makes a status change reach a school within minutes instead of at the
next 6-hourly legitimacy check. It is only a signal to check now: the status still
comes from the Control Plane's answer to the ordinary check of US-005.

This Story adds:

- in the Control Plane: the optional **push address** on `Installation` — at
  registration and on its own page, validated, audited, with a warning on the
  school's page while it is not set; the push payload in `ClassroomAgent.Contracts`;
  sending the push after a suspend or resume that changed the status, with timeout,
  retries, replacement of an unfinished push and logging;
- in the installation: the **push receiver** on the private port — identifier
  check, `202`, a legitimacy check in the background, the one-minute limit,
  resetting the check schedule, logging; and the mandatory **private-port address**
  setting with the explicit "all addresses" value `*`.

Sources: `trebovaniya.md` v77 §2 (read-only closed list of service writes), §3
(`Installation`, push address, v76), §4 (Epic 8), §5 (installation settings —
private-port address v75/v76; Control Plane audit list v76), §8 (logs v76,
"unparseable push", antiforgery exemptions, HTTP on the private port), §9
("Сетевая изоляция", "Приватный порт инсталляции защищён дважды", parameters table,
"Проверка легитимности", "Push о смене статуса" v76, v77, "Приостановка и возобновление
школы"); `architecture.md` AD-1, AD-3 … AD-6, AD-8, AD-9; `package-map.md`
(Control Plane `Push`, `Contracts`, Web `Security`); `security-conventions.md` SC-2,
SC-4, SC-9, SC-10, SC-11, SC-12, SC-13; `deployment-conventions.md` DC-3, DC-6,
DC-7, DC-10, DC-12; `persistence-conventions.md` PC-2, PC-6; `testing-conventions.md`
TC-2, TC-4, TC-5, TC-8; BR-024, BR-026, BR-080, BR-081, BR-082; NFR-014, NFR-073.
US-002 (registration, detail page, name and client ID pages), US-004 (suspend and
resume) and US-005 (check use case, scheduler, private route group, settings
reader, service channel) are reused, not re-specified.

**Open Decisions:** OD-001 — a push that cannot start a check at once — resolved
with option 2: one pending check, recorded in `trebovaniya.md` v77 and Story AC-011
(section 11).
Behaviour not literally fixed by the Story or `trebovaniya.md` is stated as
interpretations I-1 … I-14.

## 2. Business Goal

The `Installation` status decides whether a school works at all (`trebovaniya.md`
§9, "Три точки контроля Владельца"). US-004 gave the Owner the lever; US-005
delivers it with a guarantee but up to 6 hours late. The push closes the gap so a
suspension — for example when cooperation ends — takes effect almost immediately
(BR-024, BR-082, NFR-014), without giving anyone outside a way to change a school's
status: a forged push only causes a check.

## 3. Business Flow

### 3.1 Setting up a school

1. The Owner registers the `Installation` (US-002) and may enter its push address
   right away, or later on the push address page.
2. While no address is set the school's page shows a warning that status changes
   reach the school only with the periodic check, within 6 hours.
3. At deployment the installation is configured with its private port and the
   private-port address (normally the server's private IP; `*` in a container).

### 3.2 Suspension with a reachable installation

1. The Owner suspends the school (US-004). The status change and its audit row
   commit; the Owner is back on the school's page at once.
2. The Control Plane sends the push (`POST`, installation id only) to the push
   address. The installation answers `202`.
3. The installation runs its legitimacy check, receives status `suspended`, records
   it in `LegitimacyState` and enters read-only mode (US-005 FR-008, FR-010). Its
   next scheduled check is 6 hours later.

### 3.3 The installation is unreachable

1. Attempts fail; the Control Plane retries after 5 s, 30 s and 2 min, logging each
   failed attempt at `Warning`, then gives up with a `Warning`.
2. The installation learns the status at its next periodic check (≤ 6 h, or 15 min
   while failing).

### 3.4 Quick change back

1. The Owner suspends and, while that push is still retrying, resumes.
2. The unfinished push is cancelled; a new push starts from its first attempt.
3. If the installation cannot start a check for the second push at once (a check is
   running, or less than a minute has passed since the first push's check), it
   remembers one pending check and runs it as soon as allowed; it receives the
   current status within about a minute (FR-010, v77).

### 3.5 A push to the wrong school

The address on the page points at another school's installation. That installation
answers `404`, does nothing and logs `Warning`; the Control Plane logs `Warning` and
does not retry. The right school learns the status from its periodic check.

## 4. Functional Requirements

### FR-001 Private-port address setting (installation)

- A new mandatory installation setting names the address the private port listens
  on (`trebovaniya.md` §5 v75, v76; DC-6). Key name: API design.
- Valid values: an IPv4 or IPv6 address literal, or exactly `*` meaning all
  addresses *(interpretation I-1)*. No default.
- Missing, empty or invalid → the installation does not start; the log names the
  setting and the rule, never the value (US-005 FR-001 mechanism; SC-10).
- With an address, the private endpoint listens on that address and the private
  port only; with `*`, on all addresses and the private port. The public endpoints
  are unchanged.
- The private route group's local-port filter (DC-6, US-005 FR-013) stays: the
  address binding is a second, independent protection, not a replacement.
- Closes US-005 SEC-001.

### FR-002 Push address on `Installation` (Control Plane)

- `Installation` gains an optional push address (`trebovaniya.md` §3 v76).
- **Registration** (US-002 form) gains an optional push address field; empty means
  not set. Every other registration rule is unchanged.
- **Push address page**: a GET page and POST form on the school, like the name and
  client ID pages of US-002, showing the current value; submitting a valid address
  sets or changes it, submitting an empty value clears it *(interpretation I-3)*.
- Allowed at any status of the school.
- Stored in canonical form (VR-001); submitting a value whose canonical form equals
  the stored one — or empty when none is stored — changes nothing and writes no
  audit row.
- Not unique across installations.
- Setting, changing or clearing sends no push (FR-005).
- Schema change ships with its migration (PC-2); `updated_at` follows PC-6.

### FR-003 Push address audit (Control Plane)

- Setting, changing and clearing the address each write one `AuditEvent` row: actor
  the Owner account's internal id and role `Owner`, one action code for all three
  *(interpretation I-4; code: API/DB design)*, target type `Installation` and its
  internal id, outcome `succeeded`, UTC time, request id (`trebovaniya.md` §5 v76;
  SC-11).
- Written in the same transaction as the change.
- The row carries neither the old nor the new address.
- A registration that includes an address writes only the existing registration row
  (US-002) — no second row *(interpretation I-5)*.
- A submission refused by validation, and an unchanged submission, write nothing.

### FR-004 Detail page (Control Plane)

- The school's page shows the push address as entered-and-normalized (HTML-encoded),
  or a translated "not set".
- While it is not set, the page shows a translated warning that a status change
  reaches the school only with the periodic check, within 6 hours
  (`trebovaniya.md` §3, §9 v76). With an address set, no warning.
- A link leads to the push address page.
- Nothing about push results is shown anywhere (`trebovaniya.md` §9 v76).

### FR-005 When a push is sent (Control Plane)

- Exactly when US-004's suspend or resume **changed** the status
  (`InstallationStatusChangeResult.Changed`), after its transaction has committed.
- Not for an unchanged action (US-004 AC-006), a rename, a client ID change, a push
  address change, or an `AllowedAdmin` add or revoke.
- If the `Installation` has no push address: no push; one `Warning` log line with the
  `Installation` internal id (or identifier) and the category "no push address".
- The address used is the one stored at the moment the status change committed
  *(interpretation I-6)*.
- The Owner's request does not wait for any attempt: it is handed to a background
  sender in the Control Plane `Push` namespace (`package-map.md`) and the
  redirect happens at once. A failure to hand it over never fails the suspend or
  resume that already committed.

### FR-006 Push delivery (Control Plane)

- Transport: HTTP `POST` of the push payload (FR-008) to the path of the receiver
  (API design) under the push address, plain HTTP (DC-6, SC-2).
- One attempt waits at most **10 seconds** for the answer, connecting included
  *(interpretation I-7)*.
- Outcome of an attempt:

  | Installation answer | Result | Log |
  |---|---|---|
  | `202` within 10 s | delivered, stop | `Information`, `Installation` id |
  | `404` | refused, stop, no retry | `Warning` |
  | no connection, timeout, any other status | failed attempt | `Warning` with category |

- After a failed attempt: retry after **5 s**, then **30 s**, then **2 min**, each
  counted from the end of the failed attempt; after the fourth attempt fails, stop
  with a `Warning` "retries exhausted" (`trebovaniya.md` §9 v76; NFR-014)
  *(interpretation I-8)*.
- Redirects are not followed (a `3xx` is a failed attempt); no cookies or
  credentials are sent; the response body is never read into logs or state.
- Waiting runs on an injectable clock so the schedule is tested without waiting.
- Nothing about the push is stored; no audit row; no change to `Installation`.

### FR-007 One push per school at a time (Control Plane)

- The sender keeps at most one push in progress per `Installation`.
- A new push for a school that has one in progress (attempting or waiting)
  cancels the remaining attempts of the old one and starts from attempt 1; an
  attempt already in flight is abandoned *(interpretation I-9)*.
- Pushes to different schools are independent and run concurrently.
- Pushes live in memory only. On Control Plane shutdown the sender stops without
  waiting for pending retries; they are lost (`trebovaniya.md` §9 v76).

### FR-008 Push payload (Contracts)

- `ClassroomAgent.Contracts` gains the push payload: the installation id (UUID) and
  nothing else (SC-12). Shape and path: API design, following `ServiceChannel`
  JSON options (camelCase; unknown fields ignored, DC-12).
- No teaching-data type; no reference to another project.

### FR-009 Push receiver (installation)

- One endpoint in the private route group of `ClassroomAgent.Web` (DC-6), `POST`
  only, anonymous and exempt from antiforgery as the existing SC-4 "Status-change
  push receiver" entries; on the public port the same path answers `404`, also with a
  forged `Host` / `X-Forwarded-Host`.
- Steps, each only if the previous passed:
  1. **Validation** of the body (VR-002). Invalid, missing, malformed or oversized →
     an error answer (`400`, or `413`/`415` where the API design fixes it), never
     `500`; no check; nothing changes; `Error` log without the body
     (`trebovaniya.md` §8; DC-12; SC-10).
  2. **Identifier**: not equal to the configured installation id → `404`; no check;
     `Warning` log (the received id is a UUID, not personal data, but it is not
     logged either — *interpretation I-10*).
  3. **Deferred** (FR-010): if the push may not start a check now → remember one
     pending check (if none is remembered yet) → `202`; no log.
  4. **Trigger**: signal the check scheduler to run a check now → `202` at once,
     without waiting for the check; `Information` log "push accepted, check started".
- The receiver writes nothing itself: no `LegitimacyState` write, no audit (there is
  no installation audit event for it in §5).
- Works identically in read-only mode (FR-012).

### FR-010 Check on push and the one-minute limit (installation)

- The US-005 scheduler (`LegitimacyCheckBackgroundService`) gains a "check now"
  trigger. A triggered check is the ordinary `CheckLegitimacyUseCase` with all US-005
  consequences — `LegitimacyState` update, mode and result logging, readiness.
- A push starts a check only if **both**:
  - no legitimacy check (scheduled or push-triggered) is running; and
  - at least **one minute** has passed since the start of the previous
    push-triggered check (or none has run since startup)
  *(interpretation I-11)*.
- Otherwise the push is accepted and the installation **remembers one pending
  check** (`trebovaniya.md` §9 v77; OD-001 option 2). The pending check is a flag, not
  a queue: any number of such pushes leave at most one.
- The pending check starts as soon as both conditions above hold — when the running
  check completes, or when one minute has passed since the start of the previous
  push-triggered check, whichever comes last. A scheduled check completing does not
  clear it: that check may have asked the Control Plane before the status changed.
- A pending check is a push-triggered check: starting it clears the flag, starts the
  next minute, resets the schedule after it completes, and is logged at `Information`
  ("pending push check started").
- A push arriving while the pending check runs remembers a new pending check under
  the same rules.
- After a push-triggered check completes, the next scheduled check is 6 hours after
  a success or 15 minutes after a failure, counted from its completion (US-005 I-5).
  The previously pending scheduled wait is discarded.
- The decision "may start" and the start are atomic: two concurrent pushes never
  start two checks.
- Time comes from the injected clock (US-005).

### FR-011 Logging

Both hosts, per `trebovaniya.md` §8 v76, DC-10, SC-10. Every line carries the
request id when written inside a request; event names and ids: API design.

| Host | Event | Level | Carries |
|---|---|---|---|
| Control Plane | push delivered (`202`) | `Information` | `Installation` id |
| Control Plane | attempt failed | `Warning` | `Installation` id, attempt number, category (connection failed / timeout / unexpected status + the status code) |
| Control Plane | retries exhausted | `Warning` | `Installation` id |
| Control Plane | refused by installation (`404`) | `Warning` | `Installation` id |
| Control Plane | no push address on status change | `Warning` | `Installation` id |
| Installation | push accepted, check started | `Information` | — |
| Installation | push for another installation | `Warning` | — |
| Installation | malformed push | `Error` | category only |
| Installation | push deferred (check running or minute not passed) | — (not logged) | — |
| Installation | pending push check started | `Information` | — |
| Installation | invalid private-port address setting | startup refusal | setting name, rule |

Never logged: the push address, a domain, a client ID, a request or response body,
remote exception text.

### FR-012 Read-only mode

- The push receiver and the check it triggers run in read-only mode exactly as
  outside it: the `LegitimacyState` write is on the BR-026 closed list
  (`trebovaniya.md` §2, §9 v76).
- Nothing in this Story adds a write outside that list in the installation. The
  Control Plane has no read-only mode.

### FR-013 Translations (Control Plane)

Every label, warning, link text and validation message this Story adds — the
registration field, the detail page row, "not set", the warning, the push address
page and its messages — comes from `ClassroomAgent.ControlPlane.Localization` in
Ukrainian and English, Ukrainian by default; the address itself is never translated
(NFR-073, TC-8).

## 5. Acceptance Criteria

Carried from the Story with the same ids; the Story is the authority for wording.

| AC | Title | Specified by |
|---|---|---|
| AC-001 | The installation starts only with a valid private-port address | FR-001, VR-003 |
| AC-002 | The Owner sets the push address | FR-002, VR-001 |
| AC-003 | Changing the push address is audited | FR-003 |
| AC-004 | The page warns when no push address is set | FR-004 |
| AC-005 | A status change sends a push | FR-005, FR-006, FR-008 |
| AC-006 | Delivery, retries and a refused push | FR-006, FR-011 |
| AC-007 | A newer push replaces an unfinished one | FR-007 |
| AC-008 | Retries do not outlive the Control Plane | FR-007 |
| AC-009 | The installation accepts a push for itself | FR-009, FR-010 |
| AC-010 | A push for another installation is refused | FR-009 |
| AC-011 | Pushes cannot flood the Control Plane | FR-010 |
| AC-012 | A malformed push breaks nothing | FR-009, VR-002 |
| AC-013 | The push receiver is a private service endpoint | FR-009 |
| AC-014 | The push works in read-only mode | FR-012 |
| AC-015 | Pages and messages are translated | FR-013 |

## 6. Validation Rules

### VR-001 Push address (Control Plane form input)

| Rule | Valid | Invalid examples |
|---|---|---|
| optional | empty or whitespace-only → not set (clears on the push address page) | — |
| surrounding whitespace | trimmed before validation | — |
| length | at most 255 characters after trimming *(I-2)* | 256+ characters |
| scheme | exactly `http` (case-insensitive) | `https://10.0.0.5:8081`, `ftp://…`, `10.0.0.5:8081` |
| host | a DNS host name (letters, digits, hyphens, dots) or an IPv4 address or a bracketed IPv6 address | `http://:8081`, `http://ho st:8081` |
| port | present, decimal, 1–65535 | `http://10.0.0.5`, `http://10.0.0.5:0`, `http://10.0.0.5:70000` |
| path | none, or exactly `/` | `http://10.0.0.5:8081/push` |
| query, fragment, user info | none | `http://h:1?x=1`, `http://h:1#f`, `http://u:p@h:1` |

- Canonical stored form: `http://<host lower-case>:<port>` without a trailing `/`
  *(I-2)*. "Unchanged" is decided on the canonical form.
- Not unique.
- Refusal: the form is shown again with the translated message and the entered
  value; nothing changes; nothing is audited or logged with the value (SC-10).

### VR-002 Push body (installation receiver)

| Field | Required | Valid | Invalid → |
|---|---|---|---|
| body | yes | JSON that parses into the payload; unknown fields ignored (DC-12) | error answer (FR-009 step 1) |
| installation id | yes | UUID | error answer |
| size | — | at most 4 KB *(I-12)* | error answer |

A valid UUID that is not this installation's id is not a validation failure: it is
the `404` outcome (FR-009 step 2).

### VR-003 Private-port address setting (installation startup)

| Required | Valid | Invalid examples |
|---|---|---|
| yes | an IPv4 literal (`10.0.0.5`), an IPv6 literal (`fd00::5`, with or without brackets), or exactly `*` | empty, `localhost`, `server.local`, `0.0.0.0/24`, `**`, `10.0.0.5:8081` |

Failure: the host does not start; the log names the setting and the rule, never the
value. `0.0.0.0` and `::` are valid IP literals and are accepted as written; only
`*` is documented as "all addresses" *(I-1)*.

## 7. Security Requirements

| # | Requirement | Source |
|---|---|---|
| S-01 | The push receiver is the only endpoint added to the installation's anonymous and antiforgery-exemption lists, as their existing "Status-change push receiver" entries; `POST` only; the endpoint enumeration tests account for it; no other endpoint becomes anonymous or exempt | SC-4, TC-5 |
| S-02 | The receiver answers only on the private port, bound by the local-port filter; on the public port `404`, also with forged `Host` / `X-Forwarded-Host` | DC-6, SC-9, TC-5 |
| S-03 | The private port listens only on the configured address; "all addresses" only as the explicit `*`; no default | `trebovaniya.md` §5, §9 v75, v76; SC-9, DC-6 |
| S-04 | A push never sets the status: it carries only the installation id and triggers the ordinary check; the receiver writes nothing to `LegitimacyState` from the body | SC-9 (v76), SC-12, BR-082 |
| S-05 | Push-triggered checks start at most once a minute and never concurrently with another check | SC-9 (v76), BR-082 |
| S-06 | A push with another installation's id answers `404` and does nothing | SC-9 (v76) |
| S-07 | The push body is validated and size-limited before use; a rejected body is never logged or echoed | AGENTS.md Security Policy, SC-10 |
| S-08 | The push payload and the Contracts addition carry no teaching data; the Control Plane references no installation project | SC-12, SC-13, AD-1 |
| S-09 | The Control Plane sends a push only to the push address stored for that `Installation`; the installation sends nothing new outbound (the triggered check uses the existing Control Plane address) | SC-13 |
| S-10 | The push sender follows no redirects, sends no cookies or credentials, and never reads the response body into logs or state | SC-10, SC-13 |
| S-11 | The push address page and the registration field are Owner-only, POST with antiforgery token, deny-by-default as the other US-002 pages | SC-4, TC-5 |
| S-12 | Push address changes are audited without the address; push delivery is never audited | SC-11, `trebovaniya.md` §5, §9 v76 |
| S-13 | Logs carry identifiers, attempt numbers, categories and status codes only — never the push address, domain, client ID, body or remote exception text | SC-10, DC-10 |
| S-14 | The push address is HTML-encoded wherever displayed | SC-10 (output safety) |
| S-15 | Controllers see DTOs only; no `DbContext` in `Web` or in Control Plane `Controllers`; no HTTP type in the installation's `Application` | AD-3, AD-4, AD-8 |
| S-16 | No secret is added to configuration or source | SC-7 |

## 8. Error Handling

### Control Plane

| Situation | Response to the Owner | Recorded | Log |
|---|---|---|---|
| Valid push address submitted, changed | redirect to the school's page | address + audit row, one transaction | — |
| Unchanged or empty-when-none | redirect to the school's page | nothing | — |
| Invalid push address | form again with translated message | nothing | none with the value |
| Unknown installation on the push address page | `404` error page | nothing | — |
| Status changed, address set | redirect at once; push in background | status + audit (US-004) | per FR-011 |
| Status changed, no address | redirect at once | status + audit (US-004) | `Warning` |
| Handing the push to the sender fails | redirect as usual (status already committed) | — | `Error` |
| Push attempt fails / exhausted / `404` | none (not shown) | nothing | `Warning` |
| Control Plane stops with pending retries | — | nothing | — |
| Unhandled exception in the sender | none; sender keeps serving other pushes | nothing | `Error` without remote detail |

### Installation

| Situation | Answer | Effect | Log |
|---|---|---|---|
| Invalid private-port address | host does not start | — | names the setting |
| Valid push, own id, may start | `202` | check runs now; schedule resets after it | `Information` |
| Valid push, own id, check running or minute not passed | `202` | one pending check remembered; runs when allowed | none; `Information` when it starts |
| Valid push, other id | `404` | nothing | `Warning` |
| Missing / malformed / oversized body | `400` (or `413`/`415`, API design) | nothing | `Error` without body |
| Method other than POST on the receiver path | not handled (`404`/`405`, API design) | nothing | — |
| Push on the public port | `404` | nothing | — |
| Triggered check fails in any US-005 way | (already `202`) | US-005 FR-007 | US-005 FR-010 |

Expected outcomes are results, not exceptions (AD-9).

## 9. Non-Functional Requirements

- **NFR-014** — check every 6 hours plus the push: 10-second timeout, 3 retries after
  5 s, 30 s, 2 min (FR-006).
- **Latency** — the Owner's suspend/resume response is not delayed by the push
  (FR-005); the receiver answers `202` without waiting for the check (FR-009).
- **Load bound** — at most one push-triggered check per minute per installation,
  pending included, and at most one pending check (FR-010); at most one push in progress per school in the Control Plane (FR-007).
- **NFR-062** — .NET 10; nullable enabled; warnings as errors; no new NuGet package:
  the HTTP client factory comes with the `Microsoft.AspNetCore.App` shared framework
  and retries are written in code, not with a resilience library *(I-13)*.
- **NFR-073** — Control Plane additions in Ukrainian and English (FR-013). The
  installation shows no user-visible text in this Story.
- **Testability** — the push sender and the check trigger run on the injected clock;
  the Control Plane push client is substituted at its seam in Control Plane tests
  and tested itself against a local test HTTP server (`202`, `404`, `500`, timeout,
  refused connection, redirect); installation tests never call a real Control Plane
  (the US-005 `IControlPlaneClient` substitute); the receiver's local port is set via
  `TestServer.SendAsync` (DC-6); the migration and the audit transaction are tested
  on PostgreSQL via Testcontainers (TC-2).

## 10. Out of Scope

- Enforcing read-only mode (US-007).
- Showing push results to the Owner — on the page, in a message, in a list.
- Pushing name, client ID, push address or `AllowedAdmin` changes.
- A persistent retry queue; surviving a Control Plane restart.
- Verifying from the Control Plane that the push address belongs to the school.
- The deployment check that the private port does not answer on the public address
  (DC-2 procedure).
- The installation host baseline on the public port (US-008).
- Anything in Google.

## 11. Open Decisions

Full text: `docs/decisions/US-006-open-decisions.md`.

| Id | Subject | Status | Impact |
|---|---|---|---|
| OD-001 | A push swallowed by the one-minute limit or a running check may leave a stale status for up to 6 hours | RESOLVED (2026-09-17): option 2 — one pending check; `trebovaniya.md` v77, Story AC-011 updated | FR-009 step 3, FR-010, AC-011 |

### Interpretations for human review

A rejected interpretation becomes an Open Decision.

*Accepted 2026-09-17 by the human (the Owner) in the conversation: I-1 … I-14;
formal approval of the Specification is `/so:approve`.*

- **I-1** The private-port address setting accepts an IPv4 or IPv6 literal or `*`,
  not a host name: Kestrel binds addresses, and resolving a name at startup would
  make the binding depend on DNS. `0.0.0.0` and `::` are accepted as literals, but
  only `*` is the documented "all addresses" value.
- **I-2** The push address is at most 255 characters, allows a single trailing `/`,
  and is stored canonically as `http://host:port` with the host in lower case.
  IPv6 hosts are written in brackets.
- **I-3** The push address has its own page (like name and client ID in US-002);
  clearing is done by submitting it empty. No separate "remove" button.
- **I-4** Setting, changing and clearing use one audit action code ("push address
  changed"): the row may not carry values, so separate codes would only say
  whether the old or new value was empty.
- **I-5** A registration with a push address writes only the existing registration
  audit row; the address is part of the registered record.
- **I-6** A push uses the address stored when the status change committed; changing
  the address while a push is retrying does not redirect or restart that push.
- **I-7** The 10 seconds cover the whole attempt — connecting and receiving the
  response status.
- **I-8** "3 retries" means at most four attempts in total: the first, then after
  5 s, 30 s and 2 min, each pause counted from the end of the previous failed attempt.
- **I-9** A newer push to the same school abandons an attempt already in flight, not
  only the waiting retries; its outcome, if it arrives, is not logged.
- **I-10** The installation does not log the received id of a push for another
  installation — only that one arrived. It is not personal data, but it is not
  needed either.
- **I-11** "Not more than once a minute" is measured from the start of the previous
  push-triggered check (a pending check included); a scheduled check does not start
  the minute. A push arriving while any check runs leaves a pending check (FR-010).
- **I-12** The receiver accepts a body of at most 4 KB — the payload is one UUID.
- **I-13** No new NuGet package: `IHttpClientFactory` is in the shared framework
  and the fixed 5 s / 30 s / 2 min schedule does not need a resilience library.
- **I-14** Nothing distinguishes a push that arrives while the installation is in
  read-only mode; the triggered check behaves as in US-005.

## 12. Traceability

| AC | Functional requirements | Validation rules | Security | Open Decisions |
|---|---|---|---|---|
| AC-001 | FR-001, FR-011 | VR-003 | S-03, S-16 | — |
| AC-002 | FR-002 | VR-001 | S-11, S-14 | — |
| AC-003 | FR-003 | — | S-12 | — |
| AC-004 | FR-004, FR-013 | — | S-14 | — |
| AC-005 | FR-005, FR-006, FR-008 | — | S-08, S-09 | — |
| AC-006 | FR-006, FR-011 | — | S-10, S-13 | — |
| AC-007 | FR-007 | — | — | — |
| AC-008 | FR-007 | — | — | — |
| AC-009 | FR-009, FR-010, FR-011 | VR-002 | S-04 | OD-001 |
| AC-010 | FR-009, FR-011 | VR-002 | S-06 | — |
| AC-011 | FR-010 | — | S-05 | OD-001 |
| AC-012 | FR-009, FR-011 | VR-002 | S-07, S-13 | — |
| AC-013 | FR-009 | — | S-01, S-02 | — |
| AC-014 | FR-012 | — | S-04 | — |
| AC-015 | FR-013 | — | — | — |

Requirements with no single AC but required by conventions the Story cites: S-15
(AD-3, AD-4, AD-8), PC-2 migration for the push address, NFR load bound.
