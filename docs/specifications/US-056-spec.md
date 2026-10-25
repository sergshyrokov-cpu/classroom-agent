---
artifact_type: specification
story: US-056
version: 3
status: APPROVED
created_at: 2026-10-25T14:45:00Z
updated_at: 2026-10-25T16:30:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-056-membership-starts-at-course-creation.md
    version: null
  - path: trebovaniya.md
    version: 98
  - path: docs/decisions/US-056-open-decisions.md
    version: 1
  - path: docs/reviews/specifications/US-056-spec-review.md
    version: 2
supersedes: docs/specifications/US-056-spec.md@v2
---

# US-056 Specification — Members seen when a course is first loaded count from the course's creation

**v2 (2026-10-25)** addresses SPEC_REVIEW v1: M-1 and m-1 — a stored record of a
course's first load exists (`course.created_at`), so memberships stored before the
upgrade are recognised exactly and the v1 limitation I-3 is withdrawn (§1, FR-005,
I-3); m-2 — FR-001 b states that consumers compare dates.

**v3 (2026-10-25)** addresses SPEC_REVIEW v2: M-1 — the candidate test that orders
the unassigned Meet codes list is a fifth member-on-date decision (§1, FR-003.5,
FR-006, AC-004); m-1 — the two operating assumptions behind exact recognition are
named (§1, FR-005, I-3) and the Acceptance Criteria state that runs happen at
increasing instants.

## 1. Overview

Classroom gives no join or leave dates, so a `CourseMembership` stores what
synchronization observed: first seen, last seen, on the roster now (`trebovaniya.md`
§3, v31; BR-051). Until v98 the first-seen date was also the start of the
membership, so on a new installation every period before the first
synchronization had no members (deployment rehearsal P-14, 2026-10-23: empty
«Викладачі:» line in the report for 01.09–31.12.2025; in the Meet report for
01.09–21.10.2026 the organizer was "не викладач цього курсу" on all 24 meetings;
Meet codes could not link automatically).

`trebovaniya.md` v98 (§3 «Начало участия», Epic 3 «Кто в журнале за период»,
Epic 4 «Состав курса — на дату встречи»; BR-051; PC-8) separates the **start of a
membership** from its first-seen date:

- whoever is on the roster or found through submissions in the run that **first
  loaded the course** is a member from the course's Google `creationTime`, or from
  that run's date when Google gave none;
- whoever appears later is a member from the day synchronization first saw them;
- the membership ends on its last-seen date (unchanged);
- the rule covers every course, including courses first loaded after the
  installation's first synchronization, and memberships stored before v98.

This Story makes every place that decides "a member of the course in a period" or
"on a date" use that start. It changes no endpoint, no screen markup, no text and
no observed date; what the pages list, and the order of the unassigned Meet codes
list, follow the new start (FR-006).

Code facts this Specification relies on (read 2026-10-25 by a delegated read-only
lookup; not re-designed here):

- `CourseMembership.FirstSeenAt` / `LastSeenAt` (`DateTimeOffset`, required),
  `OnRoster`; no property names the run that created a membership.
  `Course.CreationTime` is `DateTimeOffset?`, set from Google.
- `course.created_at` and `course_membership.created_at` are stamped from the
  injected clock by `TimestampInterceptor` only when the row is inserted, never
  changed afterwards (PC-6). A `Course` row is inserted only by synchronization
  (`RunSynchronizationUseCase.cs:591`), inside the course's transaction, so
  `course.created_at` is the save-time instant of the course's first committed load
  (FR-002). The `course` table has had `created_at` since it was created
  (migration `AddCoursesAndRosters`).
- Synchronization sets `FirstSeenAt` to one instant per run (`observedAt`, taken
  at the start of the run, `RunSynchronizationUseCase.cs:460`) for roster members
  (`:614`) and for submitters never seen on the roster (`:817-823`, `FirstSeenAt =
  LastSeenAt = observedAt`, off the roster). Hence a membership created by the
  course's first load has `first_seen_at` ≤ `course.created_at`, and a membership
  created by any later run has `first_seen_at` > `course.created_at` (that run
  started after the course was committed). This holds under two conditions of the
  system as built: synchronization runs of an installation do not overlap (a later
  run starts after the earlier one finished), and the clock that stamps
  `observedAt` and `created_at` does not step backwards between runs.
- The start is read today as `FirstSeenAt` in: `JournalCellRule.IsOfPeriod`
  (used by `GetJournalQuery.cs:124`, `ReportBuilder.cs:97` and `:156`,
  `GetMeetReportsQuery.cs:156`), `RosterOnDate.Covers` (`RosterOnDate.cs:12`, used
  by `MeetReportRules.cs:148` and Meet code scoring), fed by `JournalSource`,
  `JournalFieldSource`, `MeetReportSource`, `MeetCodeScoringSource`; and in raw SQL
  in `MeetCodesReadSource.GetUnassignedPageAsync` (`MeetCodesReadSource.cs:54`):
  `has_candidate` — "an organizer of the code has a teacher membership covering the
  meeting's day", the same rule as `RosterOnDate` — orders the unassigned Meet codes
  list (`ORDER BY has_candidate DESC, …`, US-032: codes with candidates first).
  `rg "FirstSeenAt|first_seen_at" src` outside `Migrations` finds no other reader.
- Leaver expiry, retention purge and the course card use `LastSeenAt` only.

## 2. Business Goal

A Dean of a newly installed school sees the course's real teachers and students
in journals, template reports and Meet reports for the current school year,
including the months before the first synchronization, and Meet codes of those
months link to courses automatically. The pilot cannot show a meaningful report
without it (Story, Business Value).

## 3. Business Flow

1. A synchronization run loads a course for the first time. Everyone it finds on
   the roster, and everyone it finds only through submissions, gets a membership
   recorded as **seen in the course's first load**. First-seen and last-seen are
   the run's instant, as today.
2. A later run that finds a new person creates a membership **not** seen in the
   first load, as today.
3. When a journal, template report, Meet report, Meet code scoring or the order of
   the unassigned Meet codes list needs the members of a course for a period or on
   a date, it uses each membership's
   **start** (FR-001) and its last-seen date / on-roster flag (unchanged).
4. After the upgrade that ships this Story, memberships already in the database
   follow the same rule without a new synchronization (FR-005).

## 4. Functional Requirements

### FR-001 Start of a membership

The start of a membership is:

- (a) the course's `creationTime`, when the membership was seen in the run that
  first loaded its course and the course has a `creationTime`;
- (b) the instant of the run that first loaded the course, when the membership was
  seen in that run and the course has no `creationTime`;
- (c) the membership's first-seen instant otherwise.

In (b) the instant equals the membership's first-seen instant, because that run
recorded it (§1). `trebovaniya.md` §3 says "со дня этого прогона"; every consumer
compares the start at date granularity in the school's time zone (FR-003), so the
run's instant and the run's day give the same result. "Seen in the run that first loaded the course" covers both the
roster and the submissions-only path (OD-001 a). Whether the start is stored or
derived is the database design's decision (PC-8); the observable result is the
same either way. See I-1 for a `creationTime` later than the first load.

### FR-002 "Seen in the first load" is a fixed property of the membership

Whether a membership was seen in the run that first loaded its course is decided
once, by that run, and never changes afterwards: not by later runs, not by the
retention purge, the leaver purge or an erasure (US-038) removing other
memberships or the course's other data, and not by a change of the course's
`creationTime` in Google (the start follows the course's current stored
`creationTime`; see I-2).

A course's first load is the run in which the course's data was first committed to
the installation's database. A run that failed before committing the course does
not count (US-053: the failed course's transaction is discarded), so the next run
that commits it is the first load.

### FR-003 Every member-in-period / member-on-date decision uses the start

The start of FR-001 replaces the first-seen instant, with no other change to the
rule, in:

1. the journal's student list for a period (Epic 3; `GetJournalQuery`, the
   journal field source);
2. the template report: the teacher line «ПІБ Викладача» / «Викладачі:» and the
   student list, on screen and in both exports (Epic 3; `ReportBuilder`);
3. Meet reports: the marks organizer / course teacher / course student / not on the
   course list on a meeting's date, and report 3's list of course students for the
   period (Epic 4; `MeetReportRules`, `GetMeetReportsQuery`);
4. Meet code scoring for automatic linking: "student of the course on the date of
   at least one of their meetings" and "an organizer is a course teacher on the
   date of their meeting" (Epic 4, v41, v55; `MeetCodeScoringSource`,
   `RosterOnDate`);
5. the order of the unassigned Meet codes list: a code "has a candidate" when one of
   its organizers is a course teacher on the date of one of their meetings (Epic 4;
   US-032; `MeetCodesReadSource.GetUnassignedPageAsync`). The list keeps its order
   rule — codes with a candidate first — and its candidate test agrees with the
   scoring of item 4 for the same code.

The period and date rules themselves stay as they are: a member for a period if
start < period end and (on the roster or last seen ≥ period start); a member on a
date if the start's date ≤ that date and (on the roster or last-seen's date ≥ that
date), dates in the school's time zone (NFR-074).

### FR-004 Observed dates and dependent rules are unchanged

`first_seen_at`, `last_seen_at` and `on_roster` keep meaning what synchronization
observed and are not rewritten by this Story, its migration or later runs (Story
Out of Scope). The leaver expiry (PC-11), the retention purge and the course card
keep using the last-seen date. Who counts as a "personal account" in Meet reports
(on the roster of any course, v58) does not depend on dates and is unchanged.

### FR-005 Memberships stored before the upgrade

After the upgrade, before any new synchronization, every membership already in
the database has a start per FR-001: those its course's first load recorded count
from the course's `creationTime` (or that run's instant), the others from their
first-seen instant (`trebovaniya.md` §3: "и на участия, сохранённые до v98").

Recognition of a pre-upgrade membership as seen in its course's first load is
exact: a membership is recognised if and only if its course's first load created
it. The stored data allows this in every case (§1): the course's `created_at`
marks its first committed load, first-load memberships were first seen no later
than it, later arrivals after it. In particular a later arrival is never
recognised as a first-load member — also when the first load found nobody, or when
all the first load's memberships were deleted before the upgrade (leaver purge,
erasure). Recognition by the course's earliest surviving first-seen instant is not
acceptable, because in those two cases it would count later arrivals from
`creationTime`, contrary to §3. The database design states the exact rule and its
cost; it is verified on a synthetic pre-upgrade database (AC-009).

Exactness rests on the two conditions named in §1: runs of an installation do not
overlap, and the clock does not step backwards between runs. Both hold for the
system as built (`SyncRunCoordinator` starts a run only when none is running; the
clock is the server's UTC clock). The rule is not required to be exact for stored data that
violates them, and no such data is expected.

### FR-006 No other change

No endpoint, user-visible text, translation entry, audit event, log event or
Google call is added or changed, and no page's markup changes. What the journal,
report and Meet pages list, and the order of the unassigned Meet codes list
(FR-003.5), follow the new start; that is the purpose of the Story, not a page
change. Synchronization reads nothing new from Google.

## 5. Acceptance Criteria

Ids follow the Story. Dates are calendar dates in the school's time zone; run
instants and `creationTime` values are synthetic (TC-4); the database is real
PostgreSQL (TC-2). Every run in a criterion happens at a later instant than the
previous run (FR-005 conditions); two runs never share an instant.

### AC-001 Students of the first load count from the course's creation

Course created 2025-09-01, roster student S, first loaded by the installation's
first run on 2026-10-22, S has no submissions → S is listed in the course journal
for 01.09–31.12.2025. (FR-001 a, FR-003.1)

### AC-002 Teachers of the first load appear in reports for earlier periods

The course of AC-001 with roster teacher T at the first load → the template report
for 01.09–31.12.2025 names T in the teacher line on screen and in both exports
(table and the shipped blank form). (FR-001 a, FR-003.2)

### AC-003 Meet reports know the course's members before the first synchronization

The course of AC-001/AC-002, a Meet code linked to it, a meeting on 2025-10-15
organized by T and joined by S → T is the organizer and a course teacher, S a
course student, and report 3 lists S among the course's students for a period
containing the meeting. (FR-001 a, FR-003.3)

### AC-004 Automatic Meet code linking uses the same start

The course of AC-001/AC-002 and an unlinked Meet code whose meetings all predate
2026-10-22, organized by T and attended by the course's students → the code is
scored and linked automatically exactly as for meetings after the first run.
(FR-001 a, FR-003.4)

The same course and another code whose meetings all predate 2026-10-22, organized
by T, not linked (its score stays below automatic linking, or it is left
unassigned) → on the unassigned Meet codes list the code is in the group of codes
with a candidate, before codes with none, ordered within the group as today.
(FR-001 a, FR-003.5)

### AC-005 Someone found through submissions at the first load counts from creation

Course created 2025-09-01; its first load on 2026-10-22 finds submissions of
student L, who is not on the roster → L is listed in the journal for
01.11–30.11.2025, where L has no submission; L's membership runs from 2025-09-01
to 2026-10-22. (FR-001 a, FR-003.1, OD-001)

### AC-006 Without a creation date the first load's date is used

A course with no `creationTime`, first loaded on 2026-10-22 with roster student S,
no submissions → S is not listed for 01.09–31.12.2025 and is listed for
01.10–31.10.2026. (FR-001 b, OD-002)

### AC-007 Later arrivals count from the day they were seen

Course created 2025-09-01 and first loaded on 2026-10-22; student N joins the
roster later and is first seen by the run of 2026-11-05, no submissions → N is in
neither the journal for 01.10–31.10.2026 nor the Meet report for a meeting of
2026-10-30 (not a course student there); N's start is 2026-11-05.
(FR-001 c, FR-002)

### AC-008 A course created after the first synchronization follows the same rule

The installation's first run is on 2026-10-22; a course created 2026-10-20 is not
loaded by it and is first loaded by a run on 2026-11-05 with roster student S →
S is listed in the journal for 20.10–31.10.2026. (FR-001 a, FR-002)

### AC-009 Memberships stored before the upgrade follow the rule

A database at the schema before this Story holds a course with `creationTime`
2025-09-01, memberships stored by the run that first loaded it on 2026-10-22, and
one membership first seen by a later run on 2026-11-05 (synthetic) → after the
upgrade and before any new run, the journal for 01.09–31.12.2025 lists the first
load's members and not the one first seen on 2026-11-05. (FR-005)

The same holds for a second synthetic pre-upgrade course whose first load on
2026-10-22 found nobody (or whose first-load memberships were all deleted before
the upgrade) and which gained student M in a run on 2026-11-05: M is not listed for
01.09–31.12.2025. (FR-005; derived from §3 "Кто появился позже")

### AC-010 The observed dates and the leaver expiry are unchanged

For the memberships of AC-001…AC-009, after another run and the leaver expiry
evaluation → each membership's first-seen and last-seen values are what
synchronization observed (the migration and the start rule changed neither), and
a leaver's expiry still counts from the last-seen date. (FR-004)

### AC-011 "Seen in the first load" survives deletions (derived)

Derived from FR-002 (`trebovaniya.md` §3: the rule is a property of being seen in
the first-loading run). The course of AC-001 with first-load members S1, S2 and
later arrival N (first seen 2026-11-05) → after S1's membership is removed by an
erasure (US-038) and another run, S2 still counts from 2025-09-01 and N still
from 2026-11-05.

## 6. Validation Rules

### VR-001 `creationTime`

Taken as stored by the existing course synchronization (US-015/US-017 rules,
unchanged). Absent → FR-001 b. No new validation of Google data is added.

### VR-002 Start never moves after a first load

A membership's "seen in the first load" status (FR-002) is not changed by any
later write. A later run that sees the person again (`SeenAgain`) or marks them off
the roster changes only the observed fields, as today.

## 7. Security Requirements

- No new endpoint, page, role, permission or policy; the permission matrix (§2)
  and authorization of the journal, report and Meet pages are unchanged (SC-4,
  SC-5).
- No personal data is added to the database, logs or audit; the start is a date
  derived from data already stored (SC-8, SC-11). The upgrade migration writes no
  personal data to logs.
- Read-only mode: unaffected. Synchronization is already blocked in read-only mode
  (AD-6, BR-026); viewing journals and reports keeps working and now uses the new
  start. The upgrade migration is deployment, not a user action.
- No Google call is added (SC-6, SC-13).

## 8. Error Handling

No new failure. A course with no `creationTime` is FR-001 b, not an error. The
migration (if the database design needs one) runs at deployment like every
migration (DC); its failure is a failed deployment, as today.

## 9. Non-Functional Requirements

- Journal, report and Meet queries keep filtering in the database by course and
  membership dates; the database design keeps the existing index purpose (PC-7,
  PC-8: "Journal and Meet queries filter by these dates, so they are indexed with
  the course") for the start it uses.
- The upgrade must not rewrite `first_seen_at` / `last_seen_at`; on an
  installation with millions of memberships it completes within the deployment
  (DB design states the cost, as US-055 did).
- Dates in the school's time zone (NFR-074), UTC storage (§5 «Часовой пояс школы»).

## 10. Out of Scope

- The observed dates «впервые / последний раз замечен»: unchanged and not
  rewritten.
- Leaver expiry (PC-11) and the retention purge: keep counting from the last-seen
  date.
- The known v98 limitation: in a multi-year course current members count from the
  course's creation; people who left before the first load are known only through
  their submissions (OD-003).
- The v98 extreme-value rules (skip a course with points too large to store; clamp
  the scale percentage to 0–100 %) — Story US-057 (OD-005).
- Any interface change: no screen shows the start of a membership.
- Course list, course card, teachers page (Epic 2): they use the on-roster flag
  and last-seen date, not the start.

## 11. Open Decisions

None open. OD-001 … OD-005 are carried from the Story with the Owner's resolutions
(`docs/decisions/US-056-open-decisions.md`). `trebovaniya.md` §7 holds no open item
this Story depends on; item 27 (reload of a purged leaver) is consistent with
FR-001 c — a person recreated by a later run counts from that run.

## 12. Interpretations

- **I-1 `creationTime` later than the first load.** Google returns a course only
  after creating it, so `creationTime` is earlier than the run that first loads it.
  If stored data ever shows otherwise, the start is still the course's
  `creationTime` (FR-001 a, the rule as written); no correction is made. Tests do
  not cover this impossible case.
- **I-2 `creationTime` changes in Google.** The start of a first-load membership
  follows the course's `creationTime` as currently stored (the mirror, v83), not a
  copy taken at the first load. Google does not change a course's creation time in
  practice; the rule only says which value is authoritative.
- **I-3 Pre-upgrade recognition (FR-005).** The course's `created_at` (save-time of
  its first committed load) is taken as the stored record of the first load, and
  recognition must be exact. v1 of this Specification wrongly assumed no such
  record existed and kept a first-seen start for two edges; v2 withdraws that
  limitation — there is no edge where a pre-upgrade membership gets a different
  start than it would after the upgrade, given the two conditions of FR-005 (runs
  do not overlap; the clock does not step backwards).
- **I-4 First load = first committed load (FR-002).** A course whose first attempt
  failed and was discarded (US-053) is first loaded by the run that commits it.
- **I-5 Erasure and re-admission.** A person erased (US-038) and loaded again after
  the Admin lifts the exclusion appears in a later run, so counts from that run
  (FR-001 c), even if they were in the course's first load before erasure.

## 13. Traceability

| AC | FR / VR | Requirement |
|---|---|---|
| AC-001 | FR-001 a, FR-003.1 | §3 «Начало участия»; Epic 3 «Кто в журнале за период» |
| AC-002 | FR-001 a, FR-003.2 | §3; Epic 3 report «ПІБ Викладача» |
| AC-003 | FR-001 a, FR-003.3 | §3; Epic 4 «Состав курса — на дату встречи» |
| AC-004 | FR-001 a, FR-003.4, FR-003.5 | §3; Epic 4 «как считается доля»; US-032 order of unassigned codes |
| AC-005 | FR-001 a, FR-003.1 | §3 (roster or submissions); OD-001 |
| AC-006 | FR-001 b, VR-001 | §3 "если Google её не отдал — со дня этого прогона"; OD-002 |
| AC-007 | FR-001 c, FR-002 | §3 "кто появился позже" |
| AC-008 | FR-001 a, FR-002 | §3 "в том числе созданный после первой синхронизации" |
| AC-009 | FR-005 | §3 "и на участия, сохранённые до v98", "Кто появился позже"; OD-004 |
| AC-010 | FR-004, VR-002 | §3 v31, v56; §5 leaver expiry |
| AC-011 | FR-002, VR-002 | §3 (derived) |

## 14. Delegation note

Code facts in section 1 were collected by a read-only `quick-look` lookup and
spot-checked here (`JournalCellRule.IsOfPeriod`, `RosterOnDate.Covers`). The v2
facts on `created_at` (`TimestampInterceptor.cs`, `RunSynchronizationUseCase.cs:591`,
migration `AddCoursesAndRosters`) were read directly, nothing delegated. The v3
fact on `MeetCodesReadSource.cs:41-67` and the ripgrep for other readers of
`first_seen_at` were also done directly.
