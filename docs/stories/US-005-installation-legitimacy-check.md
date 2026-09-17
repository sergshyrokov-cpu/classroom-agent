---
id: US-005
epic: EPIC-8
title: Installation legitimacy check and grace period
slug: installation-legitimacy-check
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v73.
---

# User Story

As the **Owner** of the service

I want every school installation to confirm with the Control Plane that its
`Installation` exists and is active, keep the last answer, and fall back to
read-only mode when it is suspended or cannot confirm itself for longer than the
grace period

So that no school runs without my record, a suspension reaches the school
without my going to its server, and a short Control Plane outage never stops a
school mid-lesson.

---

# Business Value

The periodic legitimacy check is how the Owner's decisions reach a school: it
carries the `Installation` status — the lever that decides **whether the school
works at all** (`trebovaniya.md` §9, "Три точки контроля Владельца") — and the
domain and client ID later Stories check the connection against (US-009,
US-010). The push (US-006) only makes it faster; the check is the guarantee
(BR-024, NFR-014).

It is also the first Story on the school's side: it brings the installation
itself into existence — its host, its database and its mandatory configuration —
which every later installation Story builds on.

---

# Scope

**In scope:**

- the installation skeleton: the `ClassroomAgent.Web` host and the `Domain`,
  `Application`, `Infrastructure` and `Contracts` projects (AD-2), the
  installation database with its first migration, and the mandatory
  configuration this Story needs — the installation id and the Control Plane
  address;
- the wire contract of the legitimacy check in `ClassroomAgent.Contracts`;
- the check endpoint in the Control Plane, the compatibility decision and
  `InstanceLicenseCheck`;
- the background check in the installation, `LegitimacyState`, and the
  determination of whether the installation is in read-only mode and why;
- the last check on the school's page in the Control Plane, with Ukrainian and
  English translations;
- the installation's private port with liveness and readiness;
- log lines for the check on both sides.

**Out of scope:** enforcing read-only mode — refusing writes and Google calls
(US-007); the status-change push and its receiver (US-006); showing the
legitimacy status, or the reason of an unsuccessful check, to an Admin or Dean —
nobody can sign in to an installation yet (US-008, US-012); the Admin login
check (US-008); the installation's sign-in, deny-by-default authorization,
antiforgery, error page and UI localization baseline (arrive with the first
installation page, US-008); the other mandatory installation settings — school
time zone, retention period N, default UI language — which become mandatory in
the Stories that first use them; the "synchronization service not running"
readiness state (US-013); anything in Google.

---

# Acceptance Criteria

## AC-001 The installation starts only with its mandatory configuration

**Given** an installation being started

**When** the installation id is missing or is not a UUID, or the Control Plane
address is missing or is not an absolute HTTPS address

**Then**:

- the installation does not start;
- the log states which setting is wrong, without its value
  (`trebovaniya.md` §5, v69, v73);
- with both settings valid, the installation starts.

## AC-002 The check runs at startup and on schedule

**Given** a started installation

**When** time passes

**Then**:

- the first check runs right after startup, not after a delay;
- after a successful check the next one runs 6 hours later;
- after an unsuccessful check the next one runs 15 minutes later, and again
  every 15 minutes until a check succeeds (`trebovaniya.md` §9, v73);
- only one check runs at a time;
- a failing check never stops the installation or the check schedule.

## AC-003 The Control Plane answers a known installation

**Given** an `Installation` registered in the Control Plane

**When** its installation calls the check with its installation id, application
version and contract version

**Then**:

- the answer carries the `Installation` status (active / suspended), the
  compatibility state, the domain and the client ID — and nothing else
  (`trebovaniya.md` §9, "Доступ Владельца к учебным данным");
- a suspended `Installation` is answered the same way, with status suspended;
- the request carries no teaching data, and the contract has no teaching-data
  type (SC-13).

## AC-004 The Control Plane decides compatibility

**Given** the Control Plane configuration may set a minimum supported and a
recommended installation application version

**When** a known installation calls the check

**Then** the compatibility state is:

- `upgrade_required` when its application version is below the minimum
  supported version, or its contract version is one the Control Plane no longer
  supports;
- otherwise `upgrade_recommended` when its application version is below the
  recommended version;
- otherwise `supported`;
- a version setting that is not configured imposes no limit
  (`trebovaniya.md` §9, v73; DC-12).

## AC-005 The Control Plane records the last check

**Given** a known installation calls the check

**When** the Control Plane answers

**Then**:

- the `Installation`'s `InstanceLicenseCheck` holds the time of this call, the
  application and contract versions it reported and the answer given (status,
  compatibility state), replacing the previous one — one record per
  `Installation`;
- the call is logged at `Information` with the `Installation` id, the versions
  and the answer;
- nothing is written to the Control Plane audit (`trebovaniya.md` §5, §9, v73).

## AC-006 The Control Plane refuses an unknown or malformed call

**Given** the Control Plane is running

**When** the check is called with an installation id that no `Installation` has,
or with a body that is missing, malformed or fails validation

**Then**:

- the answer says the installation is unknown, or the request is invalid —
  never a server error, and never a status, domain or client ID;
- no `InstanceLicenseCheck` is written;
- an unknown id is logged at `Warning`; a rejected body is not written to the
  log (SC-10).

## AC-007 A successful check updates LegitimacyState

**Given** the Control Plane answers within 30 seconds, the answer parses, the
installation is known and the compatibility state is not `upgrade_required`

**When** the check completes

**Then**:

- `LegitimacyState` holds this check's time as the last successful check, and
  the status, compatibility state, domain and client ID from the answer;
- an answer with status suspended is a successful check too;
- an answer of `upgrade_recommended` is logged at `Warning` and changes nothing
  else (DC-12).

## AC-008 An unsuccessful check keeps the last success

**Given** the Control Plane is unreachable, does not answer within 30 seconds,
answers with an error, answers something that does not parse, does not know the
installation id, or answers `upgrade_required`

**When** the check completes

**Then**:

- the time of the last successful check does not change;
- on `upgrade_required` the status, compatibility state, domain and client ID
  from the answer are still recorded; in every other case `LegitimacyState` is
  not changed;
- the reason is logged at `Error` as a category — unreachable, timeout, error
  answer, unparseable answer, unknown installation, upgrade required — without
  the response body (`trebovaniya.md` §8, §9, v73; SC-10).

## AC-009 The installation knows whether it is in read-only mode, and why

**Given** the installation's `LegitimacyState` and the current time

**When** read-only mode is determined

**Then** the installation is in read-only mode:

- while no check has ever succeeded — reason "legitimacy not yet confirmed";
- when the last known status is suspended — reason "suspended by the Owner";
- when more than 7 days have passed since the last successful check — reason
  "grace period expired", with the time of the last successful check;
- otherwise it is not in read-only mode (BR-025, BR-081);
- the determination is available in `Application` for US-007 and later screens,
  and does not depend on the UI (AD-6).

## AC-010 Mode changes are logged

**Given** a running installation

**When** it enters read-only mode, or leaves it

**Then**:

- entering is logged once at `Warning` with the reason, leaving once at
  `Information` — not on every check;
- a check whose result differs from the previous one (successful ↔
  unsuccessful, or a new status or compatibility state) is logged at
  `Information` (DC-10).

## AC-011 LegitimacyState survives a restart

**Given** an installation with a recorded `LegitimacyState`

**When** it is restarted and the Control Plane is unreachable

**Then**:

- it holds exactly one `LegitimacyState` record;
- it determines read-only mode from the stored record — a school confirmed 2
  days ago keeps working, a school last confirmed 8 days ago is in read-only
  mode;
- writing `LegitimacyState` stays allowed in read-only mode (BR-026).

## AC-012 The Owner sees the last check on the school's page

**Given** the Owner is signed in to the Control Plane

**When** they open an `Installation`'s detail page (US-002, AC-005)

**Then**:

- the page shows the last check: time, application version, contract version,
  answered status and compatibility state;
- an `Installation` that has never called shows "not called yet";
- the time is shown in the Owner's UI language format; every label comes from
  `ClassroomAgent.ControlPlane.Localization` in Ukrainian and English (NFR-073);
- the page stays Owner-only, as in US-002.

## AC-013 The check endpoint is the service channel, not the Owner UI

**Given** the Control Plane is running

**When** the check endpoint is called

**Then**:

- it accepts `POST` only, without an antiforgery token and without a signed-in
  Owner — it is on the SC-4 service-channel exemption list, and the endpoint
  enumeration test of US-001 knows it;
- it is not available through any other method;
- no other endpoint becomes anonymous.

## AC-014 Liveness and readiness on the private port

**Given** a running installation with its private port configured

**When** liveness or readiness is requested

**Then**:

- both answer on the private port only; on the public port the same paths
  answer `404` (DC-6, SC-9);
- liveness answers healthy without touching the database;
- readiness answers `Unhealthy` when the database is unreachable, `Degraded`
  (HTTP 200) when the installation is in read-only mode or the last check was
  unsuccessful while the grace period still runs, and `Healthy` otherwise
  (`trebovaniya.md` §8, DC-11);
- both answer with the state only, no detail.

## AC-015 Logs carry identifiers only

**Given** every log line this Story writes, in the installation and in the
Control Plane

**When** it is written

**Then**:

- it carries internal identifiers, versions, categories and states only — never
  a domain, client ID, email or response body (SC-10, DC-10);
- lines written inside a request carry the request id.

---

# Open Decisions

None. The questions this Story raised — when the check runs and how often after
a failure, what counts as a successful check, the state of a new installation,
an unknown installation id, how compatibility is decided, whether the Owner sees
the checks, and that checks are not audited — were decided by the Owner and
recorded in `trebovaniya.md` v73 (§3, §4, §5, §8, §9).

---

# Notes

- This is the first installation-side Story, so the Specification also fixes the
  skeleton: project references per `package-map.md`, DI registration, the
  installation `DbContext` and first migration (PC-2), and the check running as a
  `BackgroundService` in `ClassroomAgent.Web` (AD-5). The Control Plane call goes
  through `IControlPlaneClient` in `Application/Ports` (AD-4).
- Delivery order, not a requirement change: `trebovaniya.md` §5 makes the time
  zone, retention period N and default language mandatory too; they are added
  with the Stories that first use them, so each gets a test.
- The public port has no page in this Story; the installation host baseline
  (sign-in, deny-by-default, antiforgery, error page, localization) arrives with
  US-008. The Specification must still keep the public port from serving
  anything this Story adds (AC-014).
- Time must come from an injectable clock so the schedule (AC-002) and the grace
  period (AC-009, AC-011) are tested without waiting.
- No test calls a real Control Plane from the installation's tests; the port is
  substituted (TC-4 applies to Google; the same isolation keeps installation
  tests independent of the Control Plane host). The Control Plane endpoint gets
  its own integration tests against PostgreSQL (TC-2).
- `Installation` domain and client ID travel in the answer and are stored in
  `LegitimacyState` (v54); they are not personal data, but SC-10 still keeps them
  out of logs.
- The "reason shown to the Admin" of an `upgrade_required` answer
  (`trebovaniya.md` §8) is only stored here; showing it waits for an Admin screen.
