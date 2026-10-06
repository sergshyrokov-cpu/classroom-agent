---
id: US-031
epic: EPIC-4
title: Pull Meet events and keep history beyond 180 days
slug: meet-events-pull
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v87.
# Scope decided with the Owner on 2026-10-06 in discovery (OD-001 … OD-008 below).
---

# User Story

As a **Dean** or an **Admin**

I want the installation to pull the school's Google Meet meetings during every
synchronization and keep them in its own database

So that the Meet statistics of later Stories cover the whole school year, not
only the last 180 days that Google keeps.

---

# Business Value

`trebovaniya.md` §4 Epic 4, §3 `MeetSession` / `MeetParticipation`, §6 (Admin
Reports API): Google keeps Meet audit events for 180 days only. Whatever the
installation has not stored by then is lost for good, so this is the one part of
the first version where waiting costs data. The pull has to run — and be
deployed — before linking (US-032) and reports (US-033) are useful.

---

# Scope

## In scope

- **The Meet pull is a step of synchronization** (AD-5, `package-map.md`): the
  same run, the same schedule, the same "Synchronize" button (US-019), the same
  `SyncState`. Within a run **Classroom goes first, then Meet** (§4 Epic 4 v87).
- **Source**: `call_ended` events of the Admin Reports audit log
  (`applicationName = meet`), read as the technical account through the
  `IMeetReportsReader` port (AD-4); the scope
  `admin.reports.audit.readonly` is already part of the delegation and of
  "Check access" (US-011).
- **Which meetings are stored** (§3 v87): only meetings whose **organizer is an
  account of the school's domain**. A meeting with an external organizer, or
  with no organizer email, is not stored at all — neither the meeting nor any
  of its connections.
- **Domain account** (§3 v87): an email whose domain after `@` equals the
  `WorkspaceConnection` domain exactly (case-insensitive); subdomains are not
  the school's domain. Only a domain account's email is stored on a
  participation; every other connection — external guest, connection without
  an account, other domain — is stored as an "other participant" with no email
  and no name.
- **What is stored** (§3): `MeetSession` — conference id, meeting code,
  organizer email, start and end computed from its connections (earliest join,
  latest leave); `MeetParticipation` — endpoint id, domain email or the
  "other participant" mark, join time, duration in seconds. Every other field of
  the event — network telemetry, device, location, display names — is dropped
  at the adapter and never reaches `Application`.
- **Keys and idempotence** (PC-3): `MeetSession` on `conference_id`,
  `MeetParticipation` on (`conference_id`, `endpoint_id`). Reading the same
  event again updates, never duplicates; a meeting's start and end are
  recomputed when new connections of it arrive. A reconnect that Google records
  as another conference stays another meeting (BR-063).
- **Window** (§4 Epic 4 v87):
  - **first pull** — everything Google still keeps: from 180 days before the
    run;
  - **every later pull** — from 3 days before the end of the last successful
    Meet pull, to cover events that arrive up to 24 hours late (BR-061);
  - the end of the last successful Meet pull is stored; a failed Meet step does
    not move it.
- **Failures** follow Epic 1 / US-017 unchanged: transient failures are retried
  with the same parameters; a permission failure on the Meet step (for example
  the reports scope missing from the delegation) stops the run with no retry;
  what Classroom already loaded in that run stays; the next run is on the usual
  schedule. The diagnostic names the problem the same way "Check access" does.
- **Admin's "Last synchronization" block** (§4 Epic 6 v87) gains one line:
  "Meet meetings loaded up to: <date and time>", in the installation's time zone,
  or "not loaded yet".
- **Read-only mode** (AD-6): the Meet step never runs and `IMeetReportsReader`
  is never called — synchronization as a whole is already refused.
- **Retention purge** (PC-11, BR-066): every `MeetSession` whose own date is
  more than N years old is deleted with all its `MeetParticipation` rows, in the
  existing daily purge. The purge's audit row gains the counts of meetings and
  participations deleted; no new audit event is introduced for the pull itself
  (§4 v87, SC-11).
- **Validation of Google data** (SC-10, §8): an event missing a conference id,
  endpoint id or meeting code, an out-of-range duration or an over-long value is
  skipped and counted, never written to the log with its content.
- **Translations** for the new Admin line and the Meet diagnostics (NFR-073).
- **The EF Core migration** for the Meet tables, the stored pull watermark and
  the new audit columns (PC-2).

## Out of scope

- Linking meeting codes to courses, the "unassigned meetings" list — US-032.
- A course's last activity counting its linked meetings, and a leaver's Meet
  participations expiring with the leaver — US-032 (they need the link).
- Any screen or report that shows meetings — US-033. Database statistics — US-024.
- Distinguishing staff meetings from lessons: they are stored like any other
  domain-organized meeting and stay unlinked until they expire.

---

# Acceptance Criteria

## AC-001 Meetings are pulled during synchronization

**Given** a usable connection and Google returning `call_ended` events of a
meeting organized by a domain account

**When** synchronization runs

**Then** after the Classroom step, the meeting is stored with its conference id,
meeting code, organizer email, start and end, and one participation per endpoint.

## AC-002 Start and end are computed from connections

**Then** a meeting's start is its earliest join and its end its latest leave;
when a later run reads further connections of the same meeting, start and end
are recomputed and the new participations added.

## AC-003 Repeated reading does not duplicate

**Given** the same events are read in two runs

**Then** there is still one meeting row per conference and one participation
row per (conference, endpoint).

## AC-004 Only domain-organized meetings are stored

**Given** a meeting whose organizer is outside the school's domain — another
domain, a subdomain, or no organizer email

**Then** neither the meeting nor any of its connections is stored.

## AC-005 Only domain accounts keep an email

**Given** a stored meeting with connections from a domain account, a subdomain
account, an external guest and a connection without an account

**Then** only the domain account's participation holds an email; the other three
are "other participants" with no email and no name.

## AC-006 First pull and later pulls

**Given** no Meet pull has ever succeeded

**Then** the reader is asked for events from 180 days before the run.

**Given** the last successful Meet pull ended at T

**Then** the next run asks from T minus 3 days; a run whose Meet step fails
leaves T unchanged.

## AC-007 Classroom first, permission failure stops the run

**Given** the Meet step meets a permission failure

**Then** the run stops without retry, `SyncState` holds the same diagnostic
"Check access" shows for that problem, the Classroom data loaded in this run
stays, and the Meet watermark does not move.

## AC-008 Transient failures are retried

**Given** the Meet reader fails transiently and then succeeds

**Then** the call is retried with the Epic 1 parameters and the run succeeds.

## AC-009 The Admin sees how far Meet is loaded

**Then** the connection page's "Last synchronization" block shows the end of the
last successful Meet pull in the installation's time zone, or "not loaded yet".

## AC-010 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** no Meet step runs and `IMeetReportsReader` is never called.

## AC-011 Meetings expire after N years

**Given** a meeting whose date is more than N years old and one that is not

**When** the retention purge runs

**Then** the old meeting and all its participations are deleted, the newer one
stays, and the purge's audit row records the numbers deleted.

## AC-012 Invalid events are skipped

**Given** an event missing a required field or carrying an out-of-range value

**Then** it is skipped, the rest of the pull continues, and its content is not
written to the log.

## AC-013 Nothing else of the event is kept

**Then** no column holds network telemetry, device, location or a display name.

## AC-014 Bilingual

**Then** the new line and diagnostics are in the UI language; emails and meeting
codes appear as Google holds them.

## AC-015 Tests never reach Google

Every test uses a substituted `IMeetReportsReader` and synthetic events; the
adapter's paging and field mapping are tested against recorded-shape synthetic
responses, not the live API (TC-4).

---

# Open Decisions

All resolved by the Owner on 2026-10-06, before activation:

- **OD-001** Scope — (a) pull, storage and per-meeting expiry only; linking is
  US-032, reports US-033.
- **OD-002** First pull — (a) everything Google keeps, 180 days back.
- **OD-003** Later pulls — (a) re-read the last 3 days before the end of the last
  successful Meet pull.
- **OD-004** Which meetings — (a) only meetings organized by a domain account
  (`trebovaniya.md` v87).
- **OD-005** Domain account — (a) exact match of the connection domain, no
  subdomains (`trebovaniya.md` v87).
- **OD-006** Order and failures — (a) Classroom first, then Meet; a Meet
  permission failure stops the run like any permission failure.
- **OD-007** Admin view — (a) one line "Meet meetings loaded up to" in the
  "Last synchronization" block.
- **OD-008** Audit — (a) no separate pull event; the purge's audit row gains
  meeting and participation counts.

---

# Notes

- The field names of `call_ended` (`meeting_code`, `organizer_email`,
  `identifier`, `identifier_type`, `is_external`, `endpoint_id`,
  `duration_seconds`, `conference_id`) come from Google's documentation and are
  unverified on a live domain — `trebovaniya.md` §7 item 28. The adapter keeps
  the mapping in one place so the check at the first deployment is cheap.
- A `call_ended` event is one per connection leaving the call; its time is the
  leave time, so a connection's join time is the event time minus its duration.
  The Specification confirms this reading.
- Domain membership is tested against the domain of `WorkspaceConnection`, which
  US-009 already constrains to the `Installation` domain.
- Once this Story ships, history is only kept from the first deployed pull
  onward; the earlier it reaches the school, the more history it saves.
- US-011 F-2 (no test resolves a real Google adapter from the composition root)
  applies to the new adapter too — the first Story that adds a host-registered
  Google adapter call is where "safe by accident" host tests break.
