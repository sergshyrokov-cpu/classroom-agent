---
artifact_type: specification
story: US-018
version: 1
status: APPROVED
created_at: 2026-10-12T06:04:04Z
updated_at: 2026-10-12T06:06:15Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-018-incremental-sync.md
    version: null
  - path: trebovaniya.md
    version: 90
  - path: docs/specifications/US-014-spec.md
    version: null
  - path: docs/specifications/US-015-spec.md
    version: null
  - path: docs/specifications/US-017-spec.md
    version: null
supersedes: null
---

# US-018 Specification — Incremental synchronization

## 1. Overview

A synchronization run today reads every course of the course list in full:
its roster, its coursework and materials, and its submissions, then commits
the course in its own transaction (US-014 OD-009, US-015). `trebovaniya.md` v90
(§3 `SyncState`, §4 Epic 1 "Инкрементальная синхронизация", BR-042) makes the
run skip an **archived** course that cannot have changed since its last
complete read, and adds a weekly run that reads everything as a safeguard.

The change is confined to the Classroom step of a run
(`RunSynchronizationUseCase`), to `SyncState` (one new instant) and to the
host's completion log line. No endpoint, page, policy, audit event or
translation changes. The Meet step (US-031) and the linking step (US-032) are
untouched.

What was established from the existing code (read, not changed):

- The course row (`Course`, including Google's `updateTime`) is upserted
  **inside** the course's transaction, after all three reads have succeeded
  (`ImportCourseAsync`). The Story's Note says the row "is written from the
  list before the course's own reads"; in the current code it is not. Where
  the remembered value lives stays a DB_DESIGN decision (Story Note); FR-002
  states the rule it must satisfy.
- A course that fails a read never reaches its transaction; a course reported
  gone (404) is skipped before it; a permission or exhausted transient failure
  stops the run (US-017). A course the age rule refuses is not written.
- The manual "Synchronize" button (US-019) enqueues a run of the same use case
  the schedule runs; there is one code path.
- `SyncState.ProcessedCount` counts courses read and committed in the run and
  never claims data the installation does not hold (US-015 I-5).

## 2. Business Goal

Every run spends less of the Google API quota shared by all schools (§6) and
finishes sooner, without the journal or a report ever showing stale data.
Archived courses accumulate for years (one course per class and year), so they
are a growing share of every run.

## 3. Business Flow

1. A run starts (scheduled or by the button) and, as today, is refused in
   read-only mode or without a usable connection.
2. At its start the run decides once whether it is the **weekly full run**
   (FR-005).
3. The course list is read in full, page by page, as today.
4. For each listed course, after the existing checks (unrecognised state,
   blank name): if the run is not full, the course's state is `ARCHIVED`, and
   its `updateTime` equals the value remembered at its last complete read, the
   course is **skipped as unchanged** — no roster, coursework, material or
   submission read, no write. Otherwise it is read and committed in full as
   today, and the commit remembers its `updateTime`.
5. The Meet and linking steps run as today.
6. On success `SyncState` records the run; a full run also records its time.
   The completion log line carries the number of courses skipped as unchanged.

## 4. Functional Requirements

**FR-001 Skip rule.** In a run that is not the weekly full run, a listed course
is skipped as unchanged when, and only when, all hold:
(a) its state in the course list is `ARCHIVED`;
(b) the course list carries an `updateTime` for it;
(c) the installation holds a remembered last-complete-read `updateTime` for
that course (FR-002); and
(d) the two are equal (VR-001).
A skipped course gets no roster read, no coursework read, no material read and
no submission read, and nothing of it is written — not the course row, not a
membership, coursework item or submission, not the remembered value.
(AC-001, AC-002, AC-003)

**FR-002 Remembered value.** Per course, the installation remembers the
`updateTime` that the course list carried at the course's last **complete
read** — roster, coursework, materials and submissions read and the course
committed in its transaction. The value is written in that same transaction
and only there, so it advances only when the course commits; a transaction
that rolls back leaves the previous value. A course committed with no
`updateTime` from Google remembers none. The value belongs to the course: when
the retention purge (US-037) deletes the course, the value goes with it.
(AC-002, AC-005)

**FR-003 Every other course is read in full.** A course that does not meet
FR-001 — any state other than `ARCHIVED`, a changed `updateTime`, no
`updateTime` from Google, no remembered value, or any course in a full run — is
processed exactly as before this Story, including the age rule for a course the
database does not hold (§5 v36, v55; US-015 FR-011), the off-roster marking
(US-014 FR-010), and the failure handling of US-017. (AC-002, AC-003, AC-004,
AC-006)

**FR-004 Course list and existing checks unchanged.** The course list is read
in full in every run. The checks that already precede a course's reads — an
unrecognised state is skipped and reported, a blank name is skipped and
reported (US-014 FR-003, US-017 FR-012) — run before FR-001 and keep their
behaviour and their log lines. A stored course absent from the list is treated
exactly as today. A skipped course is not "a roster read with nobody on it":
none of its memberships is marked off the roster (US-014 FR-010).

**FR-005 Weekly full run (OD-001 a).** At its start a run is the full run when
`SyncState` holds no last-full-run time, or when the run's start instant is at
least 7 days (7 × 24 h, instant arithmetic, no time zone and no weekday) after
the stored time, or when the stored time is later than the run's start
(VR-002). The decision is made once per run and does not change during it.
The 7-day period is a fixed constant, not a setting. In a full run FR-001
never applies. (AC-006)

**FR-006 Recording the full run (OD-002 a).** `SyncState` gains the time of
the last successful full run (§3 v90). It is written only when a full run
completes successfully — the whole run, Classroom, Meet and linking steps
(the same condition that writes `LastSuccessfulRunAt`, US-013 FR-008) — and
then carries that run's start instant (I-3). A failed or cancelled run, full or
not, and a successful run that was not full, leave it unchanged; so after a
failed full run the next run is again full. (AC-006)

**FR-007 Manual run.** A run requested with the "Synchronize" button (US-019)
applies FR-001 … FR-006 exactly as a scheduled run starting at the same moment
would. No parameter, control or request field forces or prevents a full run.
(AC-007)

**FR-008 Counting.** A skipped course is counted as skipped-as-unchanged. It is
**not** counted in `ProcessedCount` (I-1), which keeps meaning "courses read
and committed in this run" (US-015 I-5). In a full run the skipped count is 0.
(AC-001, AC-008)

**FR-009 Logging.** The run's completion log line (today: processed count and
memberships marked off roster) also carries the number of courses skipped as
unchanged. The line that reports a failed run carries it too, as the count
reached before the stop (I-2). Only the number is logged — no course id, course
name, email or any other personal data (SC-10, DC-10). No line is written per
skipped course. (AC-008)

**FR-010 Read-only mode and audit unchanged.** A run in read-only mode is
refused before it begins, as today (BR-026, AD-6). The new `SyncState` field is
written by the same run write that already writes `SyncState`; no new service
write and no new audit event is introduced. (AC-009)

## 5. Acceptance Criteria

Carried from the Story with its ids; each maps to requirements in §12.

- **AC-001** An unchanged archived course is skipped: no roster, coursework or
  submission read, stored memberships/coursework/submissions unchanged, counted
  as skipped.
- **AC-002** A changed archived course — different `updateTime`, or no stored
  value — is read in full, and on success the new `updateTime` is stored.
- **AC-003** A course in any state other than `ARCHIVED` is read in full in
  every run, even with an unchanged `updateTime`.
- **AC-004** A skipped archived course restored to `ACTIVE` in Google is read in
  full by the next run.
- **AC-005** A course whose read fails (transient failure exhausting retries,
  permission failure, course gone mid-run) does not have its stored
  `updateTime` advanced; the next run reads it in full.
- **AC-006** A run due as the weekly full run reads every course in full,
  archived ones included; on success `SyncState` records the time of this full
  run; on failure the next run is again full.
- **AC-007** A run requested with the "Synchronize" button skips and reads
  exactly as a scheduled run at that moment would.
- **AC-008** The completion log line carries the number of courses skipped as
  unchanged; no personal data and no course name is logged.
- **AC-009** In read-only mode no run starts; no service write is added beyond
  what a run already writes.
- **AC-010** Every test substitutes the Google ports; skipped reads are observed
  as calls not made on the substituted reader (TC-4).

## 6. Validation Rules

- **VR-001 Equality.** "Equal" in FR-001 (d) is equality of the two instants
  after both have passed through the same normalisation the storage applies
  (PostgreSQL `timestamptz` keeps microseconds; Google may send finer
  fractions). Comparing a freshly parsed value against a stored, truncated one
  would never match and silently disable the Story. A missing value on either
  side is never equal to anything, including another missing value.
- **VR-002 Last-full-run time.** Null means "never" and makes the run full. A
  stored value later than the run's start (clock moved back, restored backup)
  makes the run full rather than postponing the safeguard. The recorded value
  is never later than the run's finish.
- **VR-003 State.** Only the list state `ARCHIVED` (parsed as today,
  `CourseState.Archived`) qualifies; the stored state of the course row is not
  consulted. `PROVISIONED`, `DECLINED`, `SUSPENDED`, `ACTIVE` and an
  unrecognised value never skip.
- **VR-004 Google data.** The course list's `updateTime` is external input and
  is already validated as part of the course snapshot (§8, US-014); this Story
  adds no new field from Google.

## 7. Security Requirements

- All Google access stays read-only through the existing `IClassroomReader`
  port; this Story only makes fewer calls (Hard Stops, SC-6, AD-4).
- No endpoint, page or authorization policy is added or changed (SC-4, SC-5).
- Logs carry counts only — no course id or name for skipped courses, no
  personal data (SC-10, DC-10).
- No new audit event (SC-11 lists none for this); no new outbound destination
  (SC-13).
- Read-only mode is enforced in `Application` as today (AD-6, BR-026).

## 8. Error Handling

- A failed course read keeps US-017 behaviour: a gone course is skipped and the
  run continues; a permission failure or an exhausted transient failure stops
  the run; any other failure stops it as "unexpected". In none of these does
  the course commit, so its remembered value does not move (FR-002, AC-005).
- A failure inside a course's transaction rolls the course back, remembered
  value included.
- A failed run, full or not, records the failure as today and does not record a
  full-run time (FR-006). Courses committed before the stop keep their new
  remembered values; courses skipped before the stop were skipped.
- Cancellation by host stop is not a failed run (US-013 FR-010) and writes no
  full-run time.

## 9. Non-Functional Requirements

- Quota (§6, NFR on shared quota): an unchanged archived course costs no
  Classroom call beyond its share of the course-list pages.
- The skip decision needs no extra Google call and no per-course query beyond
  what loading the stored course already costs; DB_DESIGN may load the
  remembered values for a page of the list at once.
- Schema change ships with its EF Core migration (PC-2). Existing rows: a
  course has no remembered value until its next complete read — or, if the
  existing `Course.UpdateTime` is chosen as the value, the first run after
  deployment is a full run anyway because no full-run time exists (Story Note).

## 10. Out of Scope

- Skipping or narrowing reads for non-archived courses (Classroom has no
  "changed since" filter for coursework or submissions).
- The Meet pull window (US-031) and the linking step (US-032).
- Showing the skipped count or the last full run in the UI (US-024 if ever);
  `GetLastSynchronizationQuery` and every page are unchanged.
- Retry, backoff and diagnosis (US-017).
- Forcing a full run from the UI or configuration.
- The withdrawn pre-v90 BR-042 ("already-known participants are not
  refetched"): nothing is implemented for it.

## 11. Open Decisions and Interpretations

Story Open Decisions, carried with their resolutions
(`docs/decisions/US-018-open-decisions.md`):

- **OD-001** When the weekly full run happens — **resolved (a)**: full when no
  full run has succeeded yet or the last one is at least 7 days old. Impact:
  FR-005.
- **OD-002** A full run that fails — **resolved (a)**: the time is stored only
  when the whole run succeeds. Impact: FR-006.

No new Open Decision. `trebovaniya.md` §7 has no open item on synchronization
cost or incremental reads (items 13 and 14 concern leavers' submissions and are
untouched by a skip).

Interpretations for the human gate — choices the wording leaves open, each
taken the conservative way:

- **I-1** A skipped course is not counted in `ProcessedCount`: the counter keeps
  "read and committed this run". Consequence: on a run with many unchanged
  archived courses the stored counter is smaller than before this Story;
  nothing in the UI explains why (the Story forbids a UI change). Alternative:
  count skipped courses as processed.
- **I-2** The failed-run log line also carries the skipped count, as the host
  already does for skipped courses ("reported whether the run completed or
  stopped later"). The Story names only the completion line.
- **I-3** The recorded full-run time is the run's **start** instant, not its
  finish: everything the run read was read after it, so the next full run is
  never later than 7 days after the oldest data of the last one.
  `LastSuccessfulRunAt` keeps using the finish.
- **I-4** "Equal" is defined at storage precision (VR-001), not at the precision
  Google sends.
- **I-5** A stored last-full-run time in the future makes the run full (VR-002).
- **I-6** The 7-day period is a code constant (as `MeetOverlap` is), not an
  installation setting — OD-001 (a) names no setting.

## 12. Traceability

| AC | Requirements / rules |
|---|---|
| AC-001 | FR-001, FR-004, FR-008, VR-001, VR-003 |
| AC-002 | FR-001 (b)–(d), FR-002, FR-003, VR-001 |
| AC-003 | FR-001 (a), FR-003, VR-003 |
| AC-004 | FR-001 (a), FR-003, VR-003 (restoring changes `updateTime` and state) |
| AC-005 | FR-002, §8 |
| AC-006 | FR-005, FR-006, VR-002, I-3, I-5 |
| AC-007 | FR-007 |
| AC-008 | FR-008, FR-009, I-2 |
| AC-009 | FR-010 |
| AC-010 | §7 (port substitution, TC-4) — applies to every test of this Story |

## 13. Delegation

Written in the conversation without subagents: the Story, `trebovaniya.md` v90
§3/§4/§7 (located by grep), BR-042 in `business-rules.md`, and the current
`RunSynchronizationUseCase`, `SyncState` and host logging code were read
directly.
