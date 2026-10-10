---
artifact_type: specification
story: US-031
version: 1
status: APPROVED
created_at: 2026-10-10T05:35:31Z
updated_at: 2026-10-10T05:46:13Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-031-meet-events-pull.md
    version: null
  - path: trebovaniya.md
    version: 87
  - path: docs/specifications/US-011-spec.md
    version: 1
  - path: docs/specifications/US-013-spec.md
    version: 1
  - path: docs/specifications/US-017-spec.md
    version: 1
  - path: docs/specifications/US-037-spec.md
    version: 1
supersedes: null
---

# US-031 Specification — Pull Meet events and keep history beyond 180 days

## 1. Overview

Google keeps Meet audit events for 180 days (`trebovaniya.md` v87 §3
`MeetSession`, §6, BR-061). This Story makes the installation pull them during
every synchronization and keep them in its own database, so the Meet statistics
of US-032/US-033 can cover a whole school year.

This Story:

- adds a **Meet step** to the synchronization run of US-013/US-014/US-015, after
  the Classroom step, in the same run, schedule, button (US-019) and `SyncState`
  (§4 Epic 4 v87, AD-5);
- adds the port **`IMeetReportsReader`** (`architecture.md`, already named there)
  and its adapter in `Infrastructure/Google`, reading `call_ended` events of the
  Admin Reports audit log (`applicationName = meet`) as the technical account;
- adds the entities **`MeetSession`** and **`MeetParticipation`** (§3 v87,
  PC-12) with their EF Core migration;
- stores the **end of the last successful Meet pull** (the watermark) and shows
  it on the Admin's "Last synchronization" block (§4 Epic 6 v87);
- extends the **retention purge** (US-037) with the per-meeting expiry (§5,
  BR-066, PC-11) and its audit row with two counts.

Linking meeting codes to courses, the course's last activity counting meetings,
the leaver's Meet expiry and every Meet screen or report are out of scope
(OD-001; US-032, US-033).

Delegation: lookups of `trebovaniya.md` v87 ranges, of the existing
synchronization, purge, `SyncState` and "Last synchronization" code, and of the
business rules and conventions were done by read-only `quick-look` agents; the
facts used here were then checked against the files directly.

## 2. Business Goal

History older than 180 days is lost for good unless the installation stored it
in time. Pulling Meet with every synchronization — and deploying that as early as
possible — is the precondition for meeting-code linking (US-032) and Meet reports
(US-033) being useful over a school year (Story "Business Value", BR-061).

## 3. Business Flow

### 3.1 The first synchronization after deployment

The run imports Classroom as today. Then the Meet step asks Google for every
`call_ended` event of the last 180 days, stores the meetings organized by an
account of the school's domain with all their connections, and records the end of
the requested window as the watermark. The Admin's block shows "Meet meetings
loaded up to: <date and time>".

### 3.2 Every later synchronization

The Meet step asks for events from 3 days before the watermark to now. Meetings
and connections already stored are updated, never duplicated; connections that
arrived late are added and the meeting's start and end recomputed. The watermark
moves to the end of the new window.

### 3.3 The reports scope is missing

The Classroom step succeeds and its data stays. The Meet step meets a permission
failure: the run stops at once, with no retry; `SyncState` is `failed` with the
same diagnosis code "Check access" uses for that problem, naming the Meet step;
the watermark does not move. The next run comes on the usual schedule.

### 3.4 Google is briefly unavailable

The Meet request is retried as every Google request of a run is (US-017 FR-002);
on success the run completes.

### 3.5 Read-only mode

No run starts (US-013), so the Meet step never runs and the reader is never
called. The connection page and its block stay viewable.

### 3.6 The daily purge

Besides what it deletes today, the purge deletes every meeting whose own date is
more than N years old, with all its connections, and its audit row carries how
many meetings and connections it deleted.

## 4. Functional Requirements

### FR-001 The Meet step in a run

`RunSynchronizationUseCase` gains a second step after the Classroom import:

1. Classroom (unchanged, US-014/US-015/US-017);
2. Meet — FR-002 … FR-009.

The Meet step runs only when the Classroom step finished without stopping the
run. A run stopped by the Classroom step (US-017 FR-005) does not reach the Meet
step and does not move the watermark. The run is `completed` only when both steps
finished; `SyncState.CompleteRun` is called once, after the Meet step. The read-
only guard and the usable-connection check at the start of the run (US-013 FR-005)
cover both steps — there is no second guard call and no Meet-only run.

The run's `ProcessedCount` keeps its meaning (courses processed); the Meet step's
counts (FR-012) travel in the run outcome for logging only.

### FR-002 The window

Let *now* be the instant the Meet step begins (injected `TimeProvider`) and *W*
the stored watermark (FR-007).

- **No successful Meet pull yet** (W absent): from = now − 180 days + 1 hour
  (OD-002, I-1).
- **Otherwise**: from = W − 3 days (OD-003, BR-061).
- In both cases from is never earlier than now − 180 days + 1 hour — a
  watermark older than Google's horizon (a long outage) asks for what Google
  still keeps, and the hour keeps a long first pull, whose pages and retries
  take minutes, clear of Google's 180-day boundary (I-1).
- to = now.

The reader is asked for exactly `[from, to)`; the window is the same for every
page and every retry of the step.

### FR-003 The `IMeetReportsReader` port

Declared in `Application/Ports` (AD-4). It is given the technical account
(`WorkspaceConnection` impersonation user, BR-015) and the window, and returns the
`call_ended` events in it as an application model with **only** these values per
event (Story "What is stored", AC-013):

| Value | `call_ended` field (§7 item 28, unverified) |
|---|---|
| event time (the connection's leave time) | the activity's time |
| conference id | `conference_id` |
| meeting code | `meeting_code` |
| organizer email (may be absent) | `organizer_email` |
| endpoint id | `endpoint_id` |
| participant identifier (may be absent) | `identifier` |
| identifier type (may be absent) | `identifier_type` |
| duration in seconds | `duration_seconds` |

No other field of the event — network telemetry, device, location, display name,
`is_external`, IP — leaves the adapter. No Google SDK type crosses into
`Application` (AD-4). Whether the port streams pages or returns a list is the
design's call; it must not hold more than one page of the SDK's response in
memory at once beyond the mapped values.

### FR-004 The adapter

In `Infrastructure/Google`, next to `GoogleClassroomReader`, using the Admin
Reports client package already referenced by `GoogleAccessProbe`
(`Google.Apis.Admin.Reports.reports_v1`) — no new package.

- Reads `activities.list` for user `all`, application `meet`, event name
  `call_ended`, the window of FR-002, following every page.
- The scope is `admin.reports.audit.readonly` only (`GoogleDelegationScopes`,
  §6); nothing is written to Google.
- Every request goes through the same `GoogleRetryHandler` and failure
  classification (`GoogleFailureClassifier`) as the Classroom reader (US-017
  FR-001 … FR-004): 4 attempts, 2/8/30 s ±20 %, `Retry-After` up to 2 min; a
  retried request repeats the same page token.
- The mapping from `call_ended` parameters to the values of FR-003 lives in one
  place in the adapter, so the check of §7 item 28 at the first deployment is
  cheap (Story Notes, OD-009).
- An event whose parameters cannot be read into the model is not an adapter
  failure: it is passed up as invalid (VR-001) or skipped and counted — the run
  goes on.

### FR-005 Which meetings are stored

The decision is made **per conference, not per event** (I-4, decided at the gate):

- **a conference already stored** → every connection of it read now is stored,
  whether or not that event carries an organizer email;
- **a conference not yet stored** → it is stored, with all its connections read
  in this run, when **at least one** of its events read in this run names an
  organizer email of a **domain account** (FR-006); that email becomes the
  meeting's organizer;
- otherwise — every event of it names another domain, a subdomain, a malformed
  email or no organizer email — **nothing** of that conference is stored:
  neither the meeting nor any connection (OD-004, AC-004). Such events are
  counted as "meetings not of the school" (FR-012), not as invalid. If a later
  run reads an event of it naming a domain organizer, it is stored then, with the
  connections that run reads.

An already stored meeting is never deleted by synchronization (PC-11: only the
purge deletes).

### FR-006 Domain account

An email is a domain account's when the part after its last `@` equals the domain
of the saved `WorkspaceConnection` **exactly**, compared case-insensitively
(ordinal, invariant); a subdomain (`x@sub.school.example` for `school.example`)
is not (OD-005, §3 v87). The rule is a pure function in `Domain/Rules`, shared by
FR-005 and FR-008.

### FR-007 The watermark

The end of the last successful Meet pull (*to* of FR-002) is stored in the
installation database. It is set only when the Meet step finished — every page
read and every row written — and the run completed. A run whose Meet step fails,
is stopped by the Classroom step, is cancelled by a host stop or never starts
(read-only, unusable connection) leaves it unchanged (AC-006, AC-007). Where it is
stored (a new column of `SyncState` or its own row) is the DB design's call.

### FR-008 What is stored

**`MeetSession`** (one per conference id, PC-3, PC-12):

- conference id (natural key, unique), meeting code, organizer email as Google
  returned it;
- start = the earliest join among its stored connections; end = the latest leave
  among them (I-2).

**`MeetParticipation`** (one per (conference id, endpoint id), PC-3):

- endpoint id;
- the email, only when the identifier is a domain account's email (FR-006) —
  otherwise no email and the "other participant" mark; no name ever (OD-005,
  AC-005);
- join time = event time − duration (Story Notes, I-3); duration in seconds.

No column holds a percentage, telemetry, device, location, display name or
`is_external` (PC-12, AC-013). There is no foreign key to `ClassroomParticipant`,
`Course` or `MeetingCodeLink` (PC-12; linking is US-032). A reconnect that Google
records as another conference is another `MeetSession` (BR-063).

### FR-009 Idempotent writes

- A conference already stored is updated, not inserted: its start and end are
  recomputed over all its stored connections, old and new (AC-002). Its meeting
  code and organizer email keep their stored values (I-4).
- A (conference, endpoint) already stored is updated with the values read now;
  a new endpoint of a stored conference is added.
- Reading the same events twice leaves one row per conference and one per
  (conference, endpoint) (AC-003, PC-10).
- Rows of a page are written in transactions of the design's choosing (AD-7); a
  meeting and its connections read in one run are never left with start/end not
  matching the connections committed. What was committed before a failure stays
  (as US-017 FR-005 for courses); only the watermark records success.

### FR-010 Failures of the Meet step

Unchanged classes and behaviour of US-017 (OD-006):

- **transient** — retried inside the adapter (FR-004); final → run `failed`,
  `GoogleUnavailable`;
- **configuration** (e.g. the reports scope not authorised, the Admin SDK API not
  enabled) — no retry, run stops at once, `failed` with the same code "Check
  access" classifies for that answer (`ScopeNotAuthorized`, `ApiNotEnabled`, …);
- **unexpected** — run `failed`, `Unexpected`.

There is no "course gone" class in the Meet step. In every case the Classroom data
committed in the run stays, the watermark does not move and the next run is on the
usual schedule (US-017 FR-005).

`SyncState` additionally records **which step** stopped the run — Classroom or
Meet — so the Admin's block can say which read is not configured, as "Check
access" names the step (§4 Epic 6: "какой скоуп не разрешён в DWD"; Story "the
diagnostic names the problem the same way"). A successful run clears it. A failed
row written before this Story has no step and is shown as today (I-5).

### FR-011 The "Last synchronization" block

The Admin's connection page block (US-017 FR-007) gains:

- one line **"Meet meetings loaded up to: <date and time>"** from the watermark,
  or **"Meet meetings: not loaded yet"** when there is none (OD-007, AC-009);
- for a failed run, the step that stopped it (Classroom read / Meet events read)
  before the existing diagnosis text, which is unchanged and reused.

The watermark is shown in the school's time zone (US-025 FR-010 setting); the
block's other times stay in UTC as US-017 shipped them (OD-010 a). The block is read through the
existing `GetLastSynchronizationQuery` and its DTO `LastSynchronizationView`
(AD-8), extended; no `DbContext` in `Web` (AD-3). No new page and no new endpoint;
the page keeps its policy (US-017 FR-008: Admin only).

### FR-012 Logging

Per §8 and US-017 FR-010/FR-011, with the run id on every line:

| Event | Level |
|---|---|
| Meet step finished: window from/to, events read, meetings stored new/updated, connections stored new/updated, meetings not of the school, invalid events skipped | `Information` |
| invalid events were skipped in this run (count only, per VR-001 reason) | `Warning` |
| run stopped in the Meet step (step + diagnosis code; exception type name for `Unexpected`) | as US-017 FR-010 |
| a transient failure retried | as US-017 FR-010 |

No log line holds an email, a meeting code, an organizer, a participant
identifier, an endpoint id or any other value of an event (SC-10, AC-012). A
conference id is not logged either (I-6).

### FR-013 Retention purge

`RunRetentionPurgeUseCase` (US-037) gains a step: every `MeetSession` whose start
is more than N years before the purge's cutoff instant (the same cutoff the purge
uses for its other date rules) is deleted together with all its
`MeetParticipation` rows, whether or not its code is linked, even if a course is
still kept (§5 v23/v55, BR-066, PC-11). A meeting is never left half-deleted:
its participations and the meeting go in one transaction (AD-7); the
batching is the design's call.

The purge's single `AuditEvent` (`RetentionPurgeRun`, actor `system`) gains two
counts — meetings deleted and Meet connections deleted (OD-008, PC-11, SC-11);
`RetentionPurgeCounts` grows accordingly, with the same non-negative guard. No
other audit event is added: the pull is not a user action (SC-11, OD-008). The
purge keeps running in read-only mode (BR-026).

### FR-014 Localization

New keys in `SharedResource.uk.resx` and `SharedResource.en.resx` (NFR-073) for
the watermark line, the "not loaded yet" text and the step names of FR-010/FR-011.
The configuration texts are the existing `AccessCheck.Outcome.*` and
`LastSync.Diagnosis.*` keys, reused (US-017 FR-007). Emails and meeting codes are
never translated (AC-014) — this Story shows neither.

### FR-015 Wiring

The adapter is registered in the Web host's composition root like
`GoogleClassroomReader`. Host-level tests substitute `IMeetReportsReader` (TC-4);
no test resolves the real adapter from the composition root and calls it (US-011
F-2, Story Notes).

## 5. Acceptance Criteria

| Id | Criterion | Source |
|---|---|---|
| AC-001 | After the Classroom step, a domain-organized meeting from `call_ended` events is stored with conference id, meeting code, organizer email, start, end, and one participation per endpoint | Story AC-001 |
| AC-002 | Start = earliest join, end = latest leave; a later run reading more connections of the same meeting recomputes both and adds the new participations | Story AC-002 |
| AC-003 | The same events read in two runs leave one meeting per conference and one participation per (conference, endpoint) | Story AC-003 |
| AC-004 | A meeting organized from another domain, a subdomain or with no organizer email is not stored, nor any of its connections | Story AC-004 |
| AC-005 | Of connections from a domain account, a subdomain account, an external guest and one without an account, only the domain account's holds an email; the other three are "other participants" with no email and no name | Story AC-005 |
| AC-006 | No successful Meet pull → reader asked from now − 180 days + 1 hour; last success ended at T → asked from T − 3 days; a run whose Meet step fails leaves T unchanged | Story AC-006 |
| AC-007 | A permission failure in the Meet step stops the run without retry; `SyncState` holds the "Check access" code for it and the Meet step; Classroom data of the run stays; watermark unchanged | Story AC-007 |
| AC-008 | A transient Meet failure followed by success is retried with the US-017 parameters and the run completes | Story AC-008 |
| AC-009 | The Admin's block shows the watermark in the time zone of OD-010, or "not loaded yet" | Story AC-009 |
| AC-010 | In read-only mode (each BR-025 reason) no Meet step runs and `IMeetReportsReader` receives no call | Story AC-010 |
| AC-011 | The purge deletes a meeting older than N years with all its participations, keeps a newer one, and its audit row records both counts | Story AC-011 |
| AC-012 | An event missing a required field or carrying an out-of-range value is skipped, the pull continues, and none of its content is logged | Story AC-012 |
| AC-013 | No column holds telemetry, device, location or a display name | Story AC-013 |
| AC-014 | The new line and step names are in the UI language (uk, en) | Story AC-014 |
| AC-015 | Every test uses a substituted `IMeetReportsReader` with synthetic events; the adapter's paging and mapping are tested against synthetic recorded-shape responses, never the live API | Story AC-015 |

## 6. Validation Rules

### VR-001 Events from Google are external input (SC-10, §8)

Checked in `Application` before anything is written. An event failing any rule
is **skipped and counted** by reason; the rest of the pull continues; its values
are never logged (AC-012).

| Value | Rule (I-7) |
|---|---|
| conference id | required, non-blank, ≤ 128 characters |
| meeting code | required, non-blank, ≤ 64 characters |
| endpoint id | required, non-blank, ≤ 128 characters |
| event time | required; within the requested window extended by 1 day on each side |
| duration | required, integer, 0 ≤ d ≤ 86 400 s; join time = event time − d |
| organizer email | optional; when present ≤ 254 characters — longer or malformed counts as "not a domain account" (FR-005), not as invalid |
| participant identifier | optional; used as an email only when the identifier type says email, it is ≤ 254 characters and is a domain account (FR-006); otherwise the connection is "other participant" |

Leading and trailing whitespace is trimmed; values are otherwise stored as Google
returned them (AC-014).

### VR-002 The watermark

Never later than the instant it is written; never moved backwards by a
successful run (a run's *to* is its own *now*).

### VR-003 The domain

FR-006 compares against the saved `WorkspaceConnection` domain, which US-009
already validates and constrains to the `Installation` domain; no new input.

### VR-004 No user input

The Story adds no request body, query or route parameter; the existing
synchronization button (US-019) carries none.

## 7. Security Requirements

- **Read-only Google access** (Hard Stop, SC-8): the adapter only reads
  `activities.list`; scope `admin.reports.audit.readonly`, already in
  `GoogleDelegationScopes` and the connection instruction (US-010/US-011).
- **As the technical account** (BR-015): the impersonated user is the
  `WorkspaceConnection` technical account, never an Admin or a super-admin.
- **The key** is obtained exactly as the Classroom reader obtains it (SC-7); the
  Story adds no setting, column or UI for it.
- **Minimisation** (§3 v87, PC-12): only domain-organized meetings; only domain
  accounts' emails; no names, telemetry, device or location; no link to
  `ClassroomParticipant`.
- **Read-only mode** enforced in `Application` (AD-6, TC-5): the existing guard
  at the start of the run; a test proves no reader call per BR-025 reason.
- **Authorization**: no new endpoint; the block stays on the Admin-only
  connection page (US-017 FR-008, SC-4 unchanged).
- **Logs** (SC-10): FR-012 — no personal or Google-supplied value.
- **Audit** (SC-11): purge counts only (FR-013); no personal data.
- **Outbound flows** (SC-13): Google only; nothing new.
- Meet participation is personal data of minors (PC-9); the DB design marks the
  email column and its handling.

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| Classroom step stops the run | Meet step not reached; watermark unchanged (FR-001) |
| Meet transient failure, recovered | run continues (FR-004) |
| Meet transient failure, final | run `failed`, `GoogleUnavailable`, step Meet (FR-010) |
| Meet configuration failure | run `failed`, "Check access" code, step Meet, no retry (FR-010) |
| Meet unexpected failure | run `failed`, `Unexpected`, step Meet (FR-010) |
| invalid event | skipped, counted, run continues (VR-001) |
| not-of-the-school meeting | ignored, counted (FR-005) |
| host stop during the Meet step | as US-017: not a failed run; watermark unchanged |
| read-only / unusable connection | no run (US-013) |

Nothing from Google — message, reason, body — reaches `SyncState`, the page or a
log (SC-10, US-017 FR-006).

## 9. Non-Functional Requirements

- **Volume**: a school can produce tens of thousands of `call_ended` events in
  180 days; the first pull pages through them without holding the whole window
  in memory (FR-003) and writes in bounded transactions (FR-009).
- **Time**: injected `TimeProvider` everywhere (window, watermark, purge cutoff).
- **Indexes** (PC-7): unique on conference id; unique on (conference, endpoint);
  meeting start for the purge and future reports — the DB design decides.
- **Migration** (PC-2): Meet tables, the watermark, the failed-step column and
  the two audit counts, in one migration of this Story.
- **Bilingual UI** (NFR-073).

## 10. Out of Scope

- Meeting-code linking, the unassigned-meetings list, auto-linking — US-032.
- Course last activity counting meetings; the leaver's Meet participations
  expiring with the leaver; course purge deleting meetings through links (PC-11
  bullets that need `MeetingCodeLink`) — US-032.
- Any Meet screen or report; the 24-hour delay note — US-033.
- Database statistics — US-024.
- Telling staff meetings from lessons (Story "Out of scope").
- Verifying the field names against a live domain — §7 item 28, Owner at the
  first deployment (OD-009).

## 11. Interpretations (for the gate)

- **I-1** The earliest start ever requested is now − 180 days + 1 hour: for the
  first pull and for a watermark older than Google's horizon. Google holds
  nothing earlier, and how it answers a start at or past its boundary is
  unknown; the hour costs at most an hour of the oldest events. To be confirmed
  on the live domain with OD-009. *Resolved by the Owner at the gate,
  2026-10-10: option (a).*
- **I-2** End = latest *leave* (= join + duration). PC-12's "latest join +
  duration" is read as the same thing per connection.
- **I-3** A `call_ended` event is one per connection leaving the call, its time
  the leave time (Story Notes; unverified, item 28).
- **I-4** Whether a meeting is the school's is decided per conference (FR-005):
  a stored conference takes every later connection; an unstored one is stored
  when any of its events in the run names a domain organizer. Meeting code and
  organizer of a stored meeting are not overwritten by a later event of the same
  conference. *Resolved by the Owner at the gate, 2026-10-10: option (a).*
- **I-5** "Which step" is a new nullable `SyncState` value; old failed rows show
  the diagnosis without a step.
- **I-6** Conference ids are not logged: they are opaque, but nothing needs them
  in a log and AC-012 forbids event content.
- **I-7** The length limits of VR-001 are this Specification's choice — the
  requirements set none; 254 is the email maximum, 86 400 s the 24-hour Meet
  call limit.

## 12. Open Decisions

Full text in `docs/decisions/US-031-open-decisions.md`.

| Id | Topic | State | Impact |
|---|---|---|---|
| OD-001 … OD-008 | Story decisions | resolved 2026-10-06 (a) | as written above |
| OD-009 | `trebovaniya.md` §7 item 28: `call_ended` fields unverified | resolved 2026-10-10 (a): proceed; §7 item 28 stays open until the live check | FR-003/FR-004 mapping; does not block design or tests (synthetic fixtures) |
| OD-010 | Time zone of the watermark line vs. the rest of the block (UTC today) | resolved 2026-10-10 (a): the line in the school's time zone, the rest stays UTC | FR-011, AC-009 |

## 13. Traceability

| AC | FR / VR |
|---|---|
| AC-001 | FR-001, FR-003, FR-005, FR-008 |
| AC-002 | FR-008, FR-009 |
| AC-003 | FR-009 |
| AC-004 | FR-005, FR-006 |
| AC-005 | FR-006, FR-008, VR-001 |
| AC-006 | FR-002, FR-007 |
| AC-007 | FR-001, FR-007, FR-010 |
| AC-008 | FR-004, FR-010 |
| AC-009 | FR-011, OD-010 |
| AC-010 | FR-001, §7 |
| AC-011 | FR-013 |
| AC-012 | VR-001, FR-012 |
| AC-013 | FR-003, FR-008 |
| AC-014 | FR-014 |
| AC-015 | FR-004, FR-015 |
