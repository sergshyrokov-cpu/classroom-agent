---
id: US-006
epic: EPIC-8
title: Control Plane push on status change
slug: control-plane-push
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v76.
---

# User Story

As the **Owner** of the service

I want a school's installation to learn at once that I suspended or resumed it,
by a push from the Control Plane that makes it check its legitimacy right away

So that a suspension takes effect within minutes rather than at the next 6-hourly
check, while a forged push can never change a school's status.

---

# Business Value

The `Installation` status decides **whether the school works at all**
(`trebovaniya.md` §9, "Три точки контроля Владельца"). US-004 gave the Owner the
lever and US-005 the guaranteed, but slow, delivery. The push closes the gap:
the Owner stops a school — for example when cooperation ends — and the school is
in read-only mode almost immediately (BR-024, BR-082, NFR-014).

The push is only a signal to check now (v76). The status still comes from the
Control Plane's answer to the ordinary legitimacy check, so the push adds speed
without adding a way to change a school's status from outside.

The Story also makes the installation's private port listen only on its
configured address (v75): the push receiver is the first private endpoint for
which that binding matters.

---

# Scope

**In scope:**

- the push address on `Installation`: an optional field at registration and on
  the school's page, its validation, changing it, its audit event, and the
  warning on the school's page while it is not set;
- the push payload in `ClassroomAgent.Contracts`;
- sending the push from the Control Plane after a suspend or resume that changed
  the status: timeout, retries, replacing an unfinished push, logging;
- the push receiver on the installation's private port: identifier check,
  `202`, a legitimacy check in the background, the one-minute limit, resetting
  the check schedule, logging;
- the mandatory private-port address setting of the installation, with the
  explicit "all addresses" value `*` (v75, v76);
- Ukrainian and English translations for everything this Story adds to the
  Control Plane.

**Out of scope:** enforcing read-only mode (US-007); showing the push result to
the Owner anywhere (`trebovaniya.md` §9, v76 — log only); a push on changes other
than the status — name, client ID, push address, `AllowedAdmin`; a persistent
retry queue (retries live in memory, v76); checking from the Control Plane that
the push address really belongs to the school; the deployment check that the
private port does not answer on the public address (DC-2, a procedure, not code);
anything in Google.

---

# Acceptance Criteria

## AC-001 The installation starts only with a valid private-port address

**Given** an installation being started

**When** the private-port address setting is missing, empty, or neither an IP
address nor the value `*`

**Then**:

- the installation does not start;
- the log states that the private-port address setting is wrong, without its
  value;
- with a valid IP address the private port listens on that address only; with
  `*` it listens on all addresses; there is no default
  (`trebovaniya.md` §5, §9, v75, v76; DC-6).

## AC-002 The Owner sets the push address

**Given** the Owner is signed in to the Control Plane

**When** they register an `Installation`, or change the push address on its
page

**Then**:

- the push address may be left empty at registration and set later;
- it can be set, changed or cleared at any status of the school;
- a submitted address is accepted only as `http://`, a host name or IP address
  and a port, with no path, query, fragment or user info — for example
  `http://10.0.0.5:8081`; anything else is refused with a translated message
  and nothing changes;
- it is not unique: an address another `Installation` already uses is accepted;
- submitting the address unchanged writes nothing and no audit row
  (`trebovaniya.md` §3, v76).

## AC-003 Changing the push address is audited

**Given** the Control Plane `AuditEvent` table (SC-11)

**When** the Owner sets, changes or clears the push address

**Then**:

- one row is written with the Owner account's internal id and role as actor, the
  action, target type `Installation` and its internal id, outcome "succeeded",
  UTC time and request id, in the same transaction as the change;
- the row carries neither the old nor the new address;
- a submission refused by validation writes no row
  (`trebovaniya.md` §5, v76).

## AC-004 The page warns when no push address is set

**Given** an `Installation` without a push address

**When** the Owner opens its page

**Then** the page shows the push address as not set and warns that a status
change reaches the school only with the periodic check, within 6 hours; with an
address set there is no warning (`trebovaniya.md` §3, §9, v76).

## AC-005 A status change sends a push

**Given** an `Installation` with a push address

**When** the Owner suspends or resumes it and the status actually changes
(US-004)

**Then**:

- the Control Plane sends a `POST` to the installation's push receiver at that
  address, carrying the `Installation` id and nothing else (SC-12);
- the suspend or resume completes and the Owner returns to the page without
  waiting for the push, and sees nothing about its result;
- an action that did not change the status (US-004, AC-006) sends no push;
- an `Installation` without a push address gets no push, and this is logged at
  `Warning`;
- renaming, changing the client ID or the push address, and adding or revoking
  an `AllowedAdmin` send no push (`trebovaniya.md` §9, v76).

## AC-006 Delivery, retries and a refused push

**Given** a push being sent

**When** the installation answers or fails to

**Then**:

- `202` within 10 seconds — delivered, logged at `Information` with the
  `Installation` id;
- no connection, no answer within 10 seconds, or any answer other than `202` and
  `404` — the attempt is logged at `Warning` with the failure category, and the
  push is retried after 5 seconds, 30 seconds and 2 minutes, then abandoned with
  a `Warning`;
- `404` — logged at `Warning`, not retried;
- no log line carries the push address, a domain or a response body (SC-10);
- a failed push changes nothing in the Control Plane and writes no audit row
  (`trebovaniya.md` §8, §9, v76; NFR-014).

## AC-007 A newer push replaces an unfinished one

**Given** a push to an `Installation` still waiting for a retry

**When** the Owner changes that school's status again

**Then** the remaining attempts of the earlier push are cancelled and a new push
starts from its first attempt; pushes to other schools are not affected
(`trebovaniya.md` §9, v76).

## AC-008 Retries do not outlive the Control Plane

**Given** pushes waiting for a retry

**When** the Control Plane stops

**Then** it stops without waiting for them and they are lost; nothing about them
is stored, and the periodic check delivers the status (`trebovaniya.md` §9, v76).

## AC-009 The installation accepts a push for itself

**Given** a running installation

**When** its push receiver gets a push whose installation id equals the id in its
configuration

**Then**:

- it answers `202` at once, without waiting for the check;
- it runs its ordinary legitimacy check (US-005) in the background, and the
  status, compatibility, domain and client ID come only from that check's answer;
- the push itself writes nothing to `LegitimacyState` and is not a successful
  check;
- after that check the next scheduled check is 6 hours after a success or
  15 minutes after a failure, counted from this check;
- the accepted push is logged at `Information` (`trebovaniya.md` §8, §9, v76).

## AC-010 A push for another installation is refused

**Given** a running installation

**When** a push arrives whose installation id is not the installation's own

**Then** it answers `404`, runs no check, changes nothing and logs at `Warning`
(`trebovaniya.md` §9, v76).

## AC-011 Pushes cannot flood the Control Plane

**Given** a running installation

**When** pushes for it arrive while a check is already running, or less than one
minute after the previous check started by a push

**Then** each is answered `202` and starts no check, and none is logged; a push
arriving later than that starts a check again (`trebovaniya.md` §8, §9, v76).

## AC-012 A malformed push breaks nothing

**Given** a running installation

**When** the push receiver gets a body that is missing, malformed or fails
validation

**Then** it answers with an error and never a server error, runs no check,
changes nothing, logs at `Error` without the body, and the periodic check goes on
as before (`trebovaniya.md` §8; SC-10; DC-12).

## AC-013 The push receiver is a private service endpoint

**Given** a running installation

**When** the push receiver is called

**Then**:

- it answers on the private port only; on the public port the same path answers
  `404` (DC-6, SC-9);
- it accepts `POST` only, without an antiforgery token and without a signed-in
  user — it is on the SC-4 service-channel exemption list, and the endpoint
  enumeration tests know it;
- no other endpoint becomes anonymous.

## AC-014 The push works in read-only mode

**Given** an installation in read-only mode — suspended, past the grace period,
or never confirmed

**When** a push for it arrives

**Then** it is accepted and the check runs and writes `LegitimacyState` exactly
as outside read-only mode, so a resumed school leaves read-only mode without
waiting for the periodic check (BR-026, `trebovaniya.md` §2, §9).

## AC-015 Pages and messages are translated

**Given** every label, warning and validation message this Story adds to the
Control Plane

**When** it is displayed

**Then** it is in the Owner's UI language, Ukrainian by default, and comes from
`ClassroomAgent.ControlPlane.Localization` in both Ukrainian and English; the push
address the Owner entered is shown as entered (NFR-073).

---

# Open Decisions

None. The questions this Story raised — where the Control Plane gets the address
to push to, what the push carries and what the installation does with it, the
retry parameters, a newer push replacing an unfinished one, whether the Owner
sees the push result, a school without an address, a push for another
installation, flood protection, which changes are pushed, and the "all
addresses" value — were decided by the Owner and recorded in `trebovaniya.md`
v76 (§3, §4, §5, §8, §9). The private-port address itself was decided in v75
(section 7, question 27).

---

# Notes

- The push sender lives in the Control Plane `Push` namespace (`package-map.md`)
  and runs outside the Owner's request, for example as a hosted service fed by
  the suspend and resume actions; retries must use an injectable clock so the
  5 s / 30 s / 2 min schedule is tested without waiting.
- The installation's check scheduler from US-005 gains a "check now" trigger;
  the one-minute limit and "no second check while one runs" use the same clock.
- The push address is not personal data, but SC-10 keeps it out of logs and
  SC-11 out of audit rows, like the domain and client ID.
- The Control Plane sends plain HTTP to the private port (SC-2, DC-6, v64); its
  HTTP client follows no redirects.
- Tests never need a real installation in the Control Plane's tests, nor a real
  Control Plane in the installation's: the push client and the Control Plane
  client are substituted at their ports (AD-4). The push address change gets an
  EF Core migration (PC-2) and integration tests against PostgreSQL (TC-2).
- US-005 SEC-001 (private port bound to all interfaces) is closed by AC-001.
