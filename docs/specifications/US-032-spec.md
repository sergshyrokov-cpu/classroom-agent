---
artifact_type: specification
story: US-032
version: 1
status: APPROVED
created_at: 2026-10-10T09:34:59Z
updated_at: 2026-10-10T13:38:20Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-032-meet-code-linking.md
    version: null
  - path: trebovaniya.md
    version: 88
  - path: docs/specifications/US-031-spec.md
    version: 1
  - path: docs/specifications/US-037-spec.md
    version: 1
  - path: docs/specifications/US-019-spec.md
    version: 1
  - path: docs/specifications/US-027-spec.md
    version: 1
supersedes: null
---

# US-032 Specification — Link meeting codes to courses

## 1. Overview

US-031 stores every Meet meeting organized by a school account, but no meeting
belongs to a course yet: the Classroom API does not return a course's Meet link
(`trebovaniya.md` §4 Epic 4, v23), so the meeting code is the only key
(§3 `MeetingCodeLink`, BR-065, PC-8). This Story:

- adds the **`MeetingCodeLink`** entity: a meeting code linked to one course, or
  carrying a person's **"not a course" mark** (§3 v55/v88, BR-083);
- adds an **automatic linking step** to the synchronization run, right after the
  Meet step (§4 Epic 4, OD-007);
- adds a **"Meet meetings" page** in the Google Workspace section, for Dean and
  Admin, with the lists Unassigned, Linked and Not a course, and the actions pick
  a course, confirm, re-link and mark "not a course" (§2 matrix, OD-002);
- adds two **installation settings** — the linking thresholds (OD-006);
- **extends the retention purge** of US-037/US-031 with the PC-11 rules that
  need links (§5, PC-11);
- audits every link change (SC-11, OD-008).

Delegated to `quick-look` (read only): facts about the current code — the Meet
entities, `CourseMembership`, `RunSynchronizationUseCase`,
`RunRetentionPurgeUseCase` / `RetentionPurgeStore`, `AuditEvent`,
`IReadOnlyModeGuard`, `InstallationSettingsReader`, the Google Workspace
controllers and policies, the localization files. The Specification itself was
written here.

## 2. Business Goal

Let the Meet reports of US-033 show each course exactly its own lessons, with
as little manual work as possible: one link per code usually covers a whole
school year, because a Classroom course keeps its Meet link (§4 Epic 4). Codes
that belong to no course — staff meetings, parents' meetings, multi-group
consultations — are set aside so they neither pollute a course nor reappear
(BR-083).

## 3. Business Flow

### 3.1 A run links what is clear

A synchronization run (scheduled or by hand) reads Classroom, then Meet
(US-031). When the Meet step succeeds, the linking step scores every code that
is neither linked nor marked and links each unambiguous one to its course. The
link takes effect at once.

### 3.2 A person resolves the rest

A Dean or Admin opens "Meet meetings". The Unassigned list shows each remaining
code with its organizer(s), dates, counts and candidate courses with shares. They
pick a course, or mark the code "not a course". In the Linked list they may
confirm an automatic link, re-link a code to another course, or mark it "not a
course". In the Not a course list they may pick a course, which removes the mark.

### 3.3 Read-only mode

The lists stay viewable; every action is refused in `Application` with `409`
and the reason (BR-025, API-5). No run happens at all, so no linking step runs
(US-013).

### 3.4 The daily purge

The purge (US-037) counts a course's linked meetings in its last activity,
deletes an expiring course's links and linked meetings, deletes a leaver's Meet
participations in the course's meetings, and deletes "not a course" marks whose
code has no meeting left (PC-11).

## 4. Functional Requirements

### FR-001 The `MeetingCodeLink` entity

One row per meeting code that is linked or marked; a code with neither has no
row and is "unassigned" (PC-8, PC-12). A row holds:

- the **meeting code** — unique across the table (a code belongs to at most one
  course, §3); several rows may name the same course (a reset Classroom link
  gets a new code);
- **either** a course **or** the "not a course" mark — never both, never neither;
- for a course link: **how it was made** — automatically, or by a person (the
  person's `AppUser` id) — and **when**; and whether it was **confirmed** — by
  whom and when (§3 v55);
- for a mark: **who** set it and **when** (§3 v88).

Account references are internal ids without a foreign key, so deleting an
`AppUser` neither blocks nor cascades (PC-11). The course reference is a foreign
key with `Restrict` (PC-8); the purge deletes links before their course
(FR-013). How the mark is stored — a state of the row or a row without a
course — and the exact columns are DB_DESIGN's call (Story Notes); the
invariants above are not. The `MeetSession` table is not changed: a meeting
reaches its course only through the link (PC-12).

### FR-002 "On the roster on a date"

A person is a **student** (or **teacher**) **of course C on date D** when C has a
`CourseMembership` for that person with role `student` (`teacher`) whose
observed interval covers D (BR-051, §3 v31):

- `first_seen_at`, taken as a date in the school's time zone, is on or before D;
- and either `on_roster` is true, or `last_seen_at`, as a date in the school's
  time zone, is on or after D.

D is the meeting's start date in the school's time zone (NFR-074). The person is
matched to the participation or the organizer by email, case-insensitively
(PC-12). This rule is a pure Application function, shared by FR-003 and later by
US-033 (I-1, I-2).

### FR-003 Scoring a code

For a code, over **all its stored meetings**:

1. **Organizers** = the distinct organizer emails of those meetings.
2. **Candidates** = every course for which at least one organizer was a teacher
   of the course on the date of a meeting they organized (FR-002). No candidate
   → the code has **no candidates**.
3. **Counted accounts** = the distinct emails of domain-account participations
   (an "other participant" has no email and is not counted) across those
   meetings, **excluding every organizer** of item 1 (I-3).
4. For each candidate C, **share(C)** = the number of counted accounts that were
   students of C on the date of at least one meeting of this code they joined,
   divided by the number of counted accounts. With no counted account, every
   share is 0.

Shares are compared exactly (as fractions), never after rounding (I-4). The page
shows a share as a whole percent, rounded **down**, so a shown 60 % is never
below the threshold.

### FR-004 Deciding a code

With thresholds `MinSharePercent` (S) and `MinGapPoints` (G) (FR-005), a code is
**unambiguous** when it has at least one candidate and

- best share ≥ S %, and
- best share − next-best share ≥ G percentage points, where the next-best share
  of a code with one candidate is 0 (I-5).

Two candidates with equal best shares are never unambiguous (G ≥ 1).

### FR-005 Thresholds (installation configuration)

Two optional settings, read and validated at startup like `Sync:IntervalMinutes`
(DC-3, OD-006):

| Key | Type | Allowed | If unset |
|---|---|---|---|
| `MeetLinking:MinSharePercent` | whole number | 1 … 100 | 60 |
| `MeetLinking:MinGapPoints` | whole number | 1 … 100 | 30 |

A present but invalid value (not a whole number, out of range, empty) stops the
installation from starting with a startup error naming the key, exactly as an
invalid `Retention:Years` does (`InstallationSettingsReader`); the value itself
is not logged (SC-10). The thresholds are shown or edited nowhere in the UI
(OD-006). `deployment-conventions.md` DC-3 gains both keys in its optional list
(Story Notes).

### FR-006 The linking step in a run

`RunSynchronizationUseCase` gains a third step:

1. Classroom (unchanged);
2. Meet (US-031, unchanged);
3. **Linking** — runs only when the Meet step finished without stopping the run
   (OD-007). For every code that has at least one stored meeting and no
   `MeetingCodeLink` row, it scores (FR-003) and decides (FR-004); each
   unambiguous code gets a link: automatic, unconfirmed, to the best candidate,
   created at the run's clock time.

Each new link and its audit row (FR-014) are written in one transaction (AD-7);
a failure leaves no link without its audit row and no audit row without its link.
A code that already has a row — linked by anyone, in any state, or marked — is
**never scored or changed** by the step: an automatic link is never revised by
the system (BR-065), a mark is never touched (BR-083). An unassigned code is
re-scored on every run, so new meetings or roster changes can make it
unambiguous later (AC-004).

The read-only guard and usable-connection check at the start of the run
(US-013) cover the step; there is no linking outside a run and no second guard
call. `SyncState.CompleteRun` is called once, after the linking step.

A failure of the linking step (it calls no Google API, so only an unexpected
failure — e.g. the database) stops the run as `failed`, diagnosis `Unexpected`,
step **Linking** — a new `SyncStep` value with its own translated name in the
Admin's "Last synchronization" block (US-031 FR-010/FR-011). The meetings stored
by the Meet step and the watermark stay; links already committed in the step
stay; the next run re-scores the remaining codes. A host stop during the step is
not a failed run (US-017).

The run outcome carries, for the log only, the number of codes scored and of
links created (FR-014).

### FR-007 The "Meet meetings" page

A new page in the Google Workspace section of the installation UI, reachable
from that section's navigation by **Dean and Admin** (§2 matrix; the section's
other entries stay Admin-only). Three lists, each paginated separately per
API-8 (default size 20, max 100), each with its own empty state:

**Unassigned** — codes with at least one stored meeting and no link row. Per
code: the code; the organizer email(s); first and last meeting date (school time
zone); number of meetings; number of distinct participants (counted accounts of
FR-003 item 3 plus the number of "other participant" connections — I-6); the
candidate courses, each with name and share (FR-003), best first. A code with no
candidate shows "no candidates". Order: codes with candidates first, then codes
with none; within each group, the latest last-meeting first; then by code.
Actions: **Pick a course** (FR-008), **Mark "not a course"** (FR-011).

**Linked** — codes with a course link. Per code: the code; the course name; how
the link was made — "automatically" or the person (I-7) — and when; confirmed or
not, and if confirmed by whom and when; number of meetings; last meeting date.
Order: the latest last-meeting first, then by code (a code whose meetings have
all expired sorts last). Actions: **Confirm** (FR-009, only for an automatic
unconfirmed link), **Re-link** (FR-010), **Mark "not a course"** (FR-011).

**Not a course** — codes with the mark. Per code: the code; who marked it and
when; number of meetings; last meeting date. Order: latest mark first, then by
code. Action: **Pick a course** (FR-012).

Shares on the page are computed by FR-003 over the data as it is now (I-8);
whether the design stores them or computes them on read is DB_DESIGN's /
API_DESIGN's call, as long as the shown values equal FR-003's.

A person is shown by the name the installation knows for that `AppUser`
(display name or email, as other pages show accounts); an account deleted by the
purge is shown as a translated "deleted account" (I-7). Dates and times are in
the school's time zone (NFR-074). Codes, emails and course names are shown as
stored, never translated (NFR-073, AC-017).

The exact routes, the split between Razor page and `/api/v1` endpoints, and the
request shapes are API_DESIGN's call within API-2 … API-9.

### FR-008 Pick a course for an unassigned code

The action offers **any course stored in the installation** (OD-004): the code's
candidates first, best share first, each with its share; then every other
course by name. On success the code gets a link to the chosen course, made by
the acting person, at the current time, **unconfirmed** (I-9). From that moment
all its meetings belong to the course (§3 v55). Audited (FR-014).

### FR-009 Confirm an automatic link

Allowed only on an automatic, unconfirmed link. Records the acting person and
the current time as the confirmation. Nothing else changes (AC-009). Audited.

### FR-010 Re-link

Allowed on any course link, to any stored course other than the current one
(chosen as in FR-008: candidates first). The link now names the new course,
is made by the acting person at the current time, and its confirmation is
cleared (I-9). All the code's meetings move at once, because a meeting reaches a
course only through the link (PC-12). Audited with the old and the new course
(OD-008).

### FR-011 Mark "not a course"

Allowed on an unassigned code and on any course link — automatic or by a
person, confirmed or not (BR-083). The code's row then carries the mark, who set
it and when; it carries no course, no confirmation and no "made by". Its meetings
count in no course's reports or last activity; the code appears only in the Not
a course list; the linking step never scores it (FR-006). Audited, with the
previous course when there was one.

### FR-012 Pick a course for a marked code (removes the mark)

Allowed only on a marked code; offers courses as in FR-008. The mark is removed
and the code is linked to the chosen course, made by the acting person at the
current time, unconfirmed. One audit row records the removal of the mark and the
new course (AC-012). There is no other way to remove a mark and no plain unlink
(OD-003, BR-083).

### FR-013 Concurrent changes and expected state

Every action carries the state the person saw: the code, and — for confirm,
re-link and mark on a link — the course the link names; for pick on a marked
code — that it is marked; for pick or mark on an unassigned code — that it is
unassigned. If the code's current state differs (another person or a run changed
it in between), the action changes nothing, writes no audit row and returns
`409` with a translated message asking to reload the page (API-5, I-10). The
state check and the write happen in one transaction (AD-7); the unique code
(FR-001) guarantees that two concurrent picks for one code cannot both succeed —
the loser gets the same `409`.

### FR-014 Audit

New `AuditAction` values, one per change (SC-11, §5 v55/v88, OD-008):

| Action | Actor | Records |
|---|---|---|
| automatic link | `system` | code, course |
| course picked for an unassigned code | person | code, course |
| link confirmed | person | code, course |
| re-linked | person | code, old course, new course |
| marked "not a course" | person | code, previous course if any |
| mark removed (course picked for a marked code) | person | code, new course |

Courses are recorded by internal id. Each row carries the usual timestamp, actor
id and role, outcome and request id (SC-11); no email, name or other personal
data. How the code and course ids are stored on `AuditEvent` (columns, target
type) is DB_DESIGN's call; the audit row is never updated.

A person's action refused in read-only mode writes one **refused** row of the
same action with category `ReadOnlyMode` and the code, as refused template
writes and refused manual synchronizations do (US-019, US-027; I-11). A `409`
for a stale state (FR-013), a `404` and a validation failure write no row.

### FR-015 Read-only mode

Every action of FR-008 … FR-012 calls `IReadOnlyModeGuard` in `Application`
before any change (AD-6, TC-5); in read-only mode, for each BR-025 reason, it
returns `409` with the translated reason (API-5), changes nothing and writes only
the refused row of FR-014. The lists stay viewable. The page may disable the
action controls in read-only mode for convenience, but enforcement never depends
on that (AD-6).

### FR-016 Retention purge

`RunRetentionPurgeUseCase` (US-037, US-031 FR-013) changes as follows (PC-11,
§5):

1. **Last activity** of a course also includes the start of every `MeetSession`
   whose code is linked to the course. Meetings of a marked or unassigned code
   count for no course.
2. **An expiring course** is deleted with its `MeetingCodeLink` rows and every
   `MeetSession` reached through them, with their `MeetParticipation` rows, in
   the course's own transaction (AD-7), child-first (PC-11). Links and meetings
   are deleted before the course row.
3. **A leaver's expiry** (a membership off the roster with `last_seen_at` more
   than N years ago) also deletes that person's `MeetParticipation` rows —
   matched by email, case-insensitively — in meetings reached through the
   course's linked codes, in the same transaction as the membership (I-12).
4. **A "not a course" mark** whose code has no `MeetSession` left after the
   meeting-date rule (US-031 FR-013) is deleted. A course link with no meeting
   left is **not** deleted — it goes with its course.
5. The purge's single audit row gains one count — **links removed** (course
   links with expiring courses plus orphaned marks, I-13). Meetings and
   participations deleted by rules 2 and 3 add to the existing
   `PurgedMeetSessions` / `PurgedMeetParticipations` counts; `RetentionPurgeCounts`
   keeps its non-negative guard.

The purge still runs in read-only mode (BR-026). Order within a purge run is the
design's call, provided rule 1 is applied before a course is judged expired and
rule 4 after the meeting-date rule.

### FR-017 Localization

New keys in `SharedResource.uk.resx` and `SharedResource.en.resx` (NFR-073) for:
the page title and navigation entry, the three list titles and empty states,
column headings, "no candidates", "automatically", "deleted account", the action
labels and the course picker, success messages, the stale-state `409` message,
the "Linking" step name of FR-006, and any validation message. The read-only
messages reuse the existing keys. Codes, emails and course names are never
translated.

### FR-018 Wiring and migration

The linking step and the page use cases are registered in the Web host's
composition root. One EF Core migration of this Story adds `MeetingCodeLink`,
the new audit columns and counts, and the `SyncStep` value if it is stored as a
constrained value (PC-2). The purge, the linking step and the page need indexes
on the link's code and course and on `MeetSession`'s meeting code (PC-7) — the
DB design decides.

## 5. Acceptance Criteria

The Story's criteria, unchanged in meaning (ids kept):

| AC | Summary |
|---|---|
| AC-001 | An unambiguous code (85 % / 20 %) is linked automatically, unconfirmed, leaves Unassigned, audit row with actor `system`, code and course |
| AC-002 | Share and candidates follow the roster on the meeting date; organizers not counted; a course with no organizer-teacher on the date is no candidate |
| AC-003 | 70 % / 55 %, or a best share below 60 %: stays unassigned, shown with candidates and shares |
| AC-004 | An ambiguous code that becomes unambiguous after new meetings is linked by the next run |
| AC-005 | An automatic link stays when shares later favour another course |
| AC-006 | Thresholds from configuration; 60 / 30 when unset; invalid values stop startup |
| AC-007 | No automatic link in a run whose Meet step fails |
| AC-008 | Picking any loaded course for an unassigned code links it; all its meetings belong to the course; audited |
| AC-009 | Confirming records who and when; nothing else changes; audited |
| AC-010 | Re-linking A → B moves all meetings at once; audit row records A and B |
| AC-011 | Marking "not a course" (unassigned or linked) — no course, only in Not a course with who/when, never auto-linked again, audited |
| AC-012 | Picking a course for a marked code removes the mark and links it; audited |
| AC-013 | A code with no candidate is unassigned, "no candidates", after codes with candidates |
| AC-014 | Dean and Admin can view and act; unauthenticated refused |
| AC-015 | Read-only: lists viewable; every action refused in `Application`, no change, no link-change audit row |
| AC-016 | Purge: expired course's links/meetings/participations deleted; leaver's participations deleted; recent linked meeting keeps the course; orphaned mark deleted; counts audited |
| AC-017 | UI language for page, actions, statuses, errors; codes, emails, course names as Google holds them |
| AC-018 | Tests use substituted Google ports and synthetic data |

## 6. Validation Rules

### VR-001 Meeting code in a request (SC-10, §8)

Required, non-blank, at most 64 characters (US-031 VR-001), compared exactly as
stored. A code with no `MeetingCodeLink` row and no `MeetSession` → `404`. A
malformed value → `400`. The value is never logged.

### VR-002 Course in a request

Required; a positive whole number (the internal `Course` id). A course not
stored in the installation → `404` for the course, nothing changed. Re-link to
the course the link already names → `400` with a translated field error.

### VR-003 Expected state (FR-013)

Required for every action; one of the values the design defines (unassigned /
linked to course X / marked). Missing or malformed → `400`; well-formed but not
matching → `409`.

### VR-004 Paging (API-8)

`page` ≥ 0, `size` 1 … 100, defaults 0 and 20, per list. Out of range →
`400` with a field error. Any other query parameter is ignored or refused as the
design states; a repeated parameter is malformed (`400`), as on other pages.

### VR-005 Configuration (FR-005)

As the FR-005 table. Checked once at startup.

### VR-006 Data from the database

Meeting codes, emails and course names come from rows already validated at
ingestion (US-014, US-031 VR-001); they are HTML-encoded on output like any
text, never trusted as markup.

## 7. Security Requirements

- **Authorization** (SC-4, API-9, §2 matrix): one policy for viewing the page and
  its lists and one for the actions — both allow **Admin and Dean**. Every
  endpoint declares its policy; anonymous access is refused (`401` for
  `/api/v1`, redirect to sign-in for pages). The section's other pages keep their
  Admin-only policies.
- **Forbidden-role test** (TC-5): both v1 roles are allowed, so each endpoint's
  forbidden case is the unauthenticated request (AC-014).
- **CSRF** (API-7): every action is `POST` / `PUT` / `PATCH` / `DELETE` with the
  antiforgery token; no `GET` changes state.
- **Read-only mode** in `Application` (AD-6, FR-015), tested per BR-025 reason.
- **No Google call** anywhere in this Story: scoring and the page use stored data
  only (SC-8, SC-13).
- **Personal data** (PC-9, BR-070): organizer and participant emails are shown to
  Dean and Admin only; they appear in no audit row (FR-014), no log (SC-10) and no error message. The page sends `no-store` like
  other pages showing personal data (US-040).
- **No personal data in the query string**: list paging uses `page` / `size`
  only; a code travels in the route or body, not in a query string (I-14).
- **Audit** (SC-11): FR-014; rows never updated.
- **Outbound flows** (SC-13): none.

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| Meet step stops the run | linking step not reached; no link (FR-006, AC-007) |
| linking step fails | run `failed`, `Unexpected`, step Linking; committed links stay (FR-006) |
| host stop during linking | not a failed run; next run re-scores (FR-006) |
| read-only mode, any action | `409`, reason, refused audit row, no change (FR-015) |
| stale state | `409`, reload message, no change, no row (FR-013) |
| unknown code / course | `404`, no change, no row (VR-001, VR-002) |
| malformed input | `400` with field errors (VR-001 … VR-004) |
| re-link to the same course | `400` (VR-002) |
| invalid threshold setting | installation does not start (FR-005) |
| unauthenticated | `401` / sign-in redirect (§7) |

`/api/v1` errors use the API-6 body; pages show the translated error or message
(API-6, US-040).

## 9. Non-Functional Requirements

- **Volume**: a school may have thousands of codes and tens of thousands of
  participations; scoring every unassigned code on each run must not load all
  participations of the installation at once — the design scores per code or in
  bounded batches.
- **Time**: injected `TimeProvider` for link, confirmation, mark times and the
  purge cutoff; the school's time zone for meeting dates (FR-002, NFR-074).
- **Indexes** (PC-7): FR-018.
- **Migration** (PC-2): one, FR-018.
- **Bilingual UI** (NFR-073): FR-017.
- **Logging** (DC-10, SC-10): the linking step logs counts only (codes scored,
  links created) — no code, email or course name.

## 10. Out of Scope

- Any report or screen showing a course's meetings — US-033.
- Showing or changing the thresholds in the UI.
- Plain unlink back to "unassigned" (OD-003, v88).
- Telling staff meetings apart automatically.
- Using a course's Classroom Meet link (v23).
- Re-scoring or revising existing links.
- Viewing the audit rows (EPIC-9).

## 11. Interpretations (for the gate)

- **I-1** "On the roster on date D" (FR-002) compares whole dates in the school's
  time zone, with `first_seen_at` / `last_seen_at` as observed by synchronization
  (BR-051). Consequence of §3 v31 ("all before the first synchronization counts
  as starting on its day"): **meetings older than the first synchronization find
  nobody on the roster**, so a code used only before deployment has no
  candidates and waits for a person. A code still in use gets candidates from its
  newer meetings, and its share counts any account that was a student on the date
  of at least one of its meetings (BR-065).
- **I-2** The same FR-002 rule is the one US-033 will use for "student / teacher
  of the course on the meeting's date" (§4 Epic 4); it is defined once here.
- **I-3** "Organizers excluded" excludes an account that organized any meeting
  of the code from the whole count, not only from the meetings it organized.
- **I-4** Thresholds compare exact fractions; display rounds down.
- **I-5** A single candidate's next-best share is 0, so it links when its share
  is at least S % and at least G points. Equal best shares never link.
- **I-6** "Number of participants" on the Unassigned list = distinct domain
  accounts (organizers excluded) + "other participant" connections; other
  participants have no identity to deduplicate.
- **I-7** A person in the lists is shown as the installation shows accounts
  elsewhere; a purged account shows "deleted account".
- **I-8** Shares shown on the page reflect current data (they may differ from
  what the last run saw if the roster changed since).
- **I-9** A link made by a person (pick, re-link, mark removal) is **unconfirmed**
  and shows who made it and when; "confirmed" stays a separate check, offered
  only on automatic links (§3: status "created automatically / confirmed"). A
  person's link is not shown as needing confirmation. Re-linking clears an
  earlier confirmation.
- **I-10** Every action carries the expected state, so a run or another person
  acting in between gives `409`, not a silent overwrite.
- **I-11** A read-only refusal writes a refused audit row, following US-019 and
  US-027. AC-015's "no audit row of a link change" holds: the row records a
  refused attempt, not a change. (Alternative: no row at all.)
- **I-12** A leaver's Meet participations are deleted only in meetings reached
  through the course's **linked** codes; in unassigned or marked meetings they
  stay until the meeting's own date expires (BR-066).
- **I-13** One "links removed" count covers course links and marks.
- **I-14** The "Meet meetings" page puts no personal data in a query string
  (US-039 INFO-1).

## 12. Open Decisions

Full text in `docs/decisions/US-032-open-decisions.md`.

| Id | Topic | State | Impact |
|---|---|---|---|
| OD-001 … OD-008 | Story decisions (scope, page, unlink, picker, no-candidate codes, thresholds, when linking runs, audit content) | resolved by the Owner 2026-10-10 | as written above |

No new Open Decision. `trebovaniya.md` §7 has no open item on code linking
(question 22, 23 closed in v55); item 28 (`call_ended` field names) concerns
US-031's ingestion and does not block this Story (synthetic data, TC-4). The
interpretations I-1 … I-14 may be turned into Open Decisions at
`HUMAN_SPEC_APPROVAL`.

## 13. Traceability

| AC | FR / VR |
|---|---|
| AC-001 | FR-001, FR-003, FR-004, FR-006, FR-014 |
| AC-002 | FR-002, FR-003 |
| AC-003 | FR-004, FR-007 |
| AC-004 | FR-006 |
| AC-005 | FR-006 |
| AC-006 | FR-005, VR-005 |
| AC-007 | FR-006 |
| AC-008 | FR-008, FR-013, FR-014, VR-001, VR-002 |
| AC-009 | FR-009, FR-014 |
| AC-010 | FR-010, FR-014, VR-002 |
| AC-011 | FR-011, FR-006, FR-014 |
| AC-012 | FR-012, FR-014 |
| AC-013 | FR-003, FR-007 |
| AC-014 | §7, FR-007 |
| AC-015 | FR-015, FR-014 |
| AC-016 | FR-016 |
| AC-017 | FR-017, FR-007 |
| AC-018 | §7 (no Google call), TC-4 |
