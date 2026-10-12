---
id: US-018
epic: EPIC-1
title: Incremental synchronization
slug: incremental-sync
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v90.
# OD-001 and OD-002 were resolved by the Owner on 2026-10-12, before
# activation.
---

# User Story

As the **system**

I want a synchronization run to skip archived courses that cannot have changed
since I last read them in full

So that every run spends less of the Google API quota shared by all schools
(§6) and finishes sooner, without the journal or the reports ever showing stale
data.

---

# Business Value

`trebovaniya.md` §4 Epic 1 (v90): "Инкрементальная синхронизация". Google does
not let anyone change an `ARCHIVED` course — no coursework, no grades, no roster
change — until it is restored, and archiving or restoring changes the course's
`updateTime`. An archived course whose `updateTime` is the one stored at its
last complete read therefore holds exactly what the installation already has.
Schools keep archived courses for years (one course per class and year), so
these are a growing share of every run.

The pre-v90 wording of BR-042 ("already-known participants are not refetched")
is withdrawn: Google returns names and addresses inside the roster, there is no
separate per-person request to save. US-014 OD-007 left BR-042 to this Story;
v90 is what it now means.

Courses in any other state are read in full every run: a new grade or a new
coursework item does not change the course's `updateTime`, so skipping them
would silently stale the journal.

---

# Scope

## In scope

- Per course, the installation remembers the Google `updateTime` seen at the
  course's **last complete read** — roster, coursework, materials and
  submissions read and committed in that course's transaction (US-014 OD-009,
  US-015).
- In a run, an `ARCHIVED` course whose current `updateTime` from the course list
  equals the remembered one is **skipped**: no roster, coursework or submission
  read, no write to its data.
- Every other course — any other state, a changed `updateTime`, no remembered
  value yet — is read in full as today.
- The course list itself is read in full every run (the course row of a skipped
  course is still matched against the list; a course gone from Google is
  handled exactly as today).
- **Weekly full run:** once a week a run reads every course in full, archived
  ones included (OD-001). `SyncState` stores the time of the last full run
  (§3 v90).
- The "Synchronize" button (US-019) follows the same rules as a scheduled run.
- The run's log line states how many courses were skipped as unchanged. No UI
  change, no audit change.
- The course-age rule (§5, v36, v55) is unchanged: a new course older than the
  retention period is still not imported.

## Out of scope

- Skipping or narrowing reads for non-archived courses (e.g. by
  coursework/submission `updateTime`): Classroom has no "changed since" filter
  for them.
- The Meet pull (US-031): its window rule stays as it is.
- Showing skipped counts or the last full run anywhere in the UI (US-024 if
  ever).
- Retry, backoff and diagnosis (US-017) — unchanged.

---

# Acceptance Criteria

## AC-001 An unchanged archived course is skipped

**Given** an `ARCHIVED` course whose last complete read stored `updateTime` T,
and a run that is not the weekly full run

**When** the course list returns that course with `updateTime` T

**Then** no roster, coursework or submission read is made for it, its stored
data (memberships, coursework, submissions) stays exactly as it was, and it is
counted as skipped.

## AC-002 A changed archived course is read in full

**Given** an `ARCHIVED` course whose stored `updateTime` differs from the one
in the course list — or which has no stored value (never completely read)

**Then** it is read in full as before this Story, and on success the new
`updateTime` is stored.

## AC-003 Non-archived courses are always read in full

**Given** a course in any state other than `ARCHIVED`, even with an unchanged
`updateTime`

**Then** it is read in full in every run.

## AC-004 Restoring a course from the archive brings it back

**Given** a skipped archived course that is restored to `ACTIVE` in Google

**Then** the next run reads it in full.

## AC-005 A failed course read is not remembered as complete

**Given** a course whose read fails (a transient failure that exhausts retries,
a permission failure, or a course gone mid-run, BR-043)

**Then** its stored `updateTime` is not advanced, so the next run reads it in
full.

## AC-006 The weekly full run reads everything

**Given** a run that is due as the weekly full run (OD-001)

**Then** every course is read in full, archived ones included; when the run
succeeds, `SyncState` records the time of this full run; when it fails, the
next run is again a full run (OD-002).

## AC-007 A manual run follows the same rules

**Given** a run requested with the "Synchronize" button (US-019)

**Then** it skips and reads exactly as a scheduled run would at that moment.

## AC-008 The skipped count is logged

**Then** the run's completion log line carries the number of courses skipped as
unchanged; no personal data and no course name is logged (SC-10).

## AC-009 Read-only mode is unchanged

**Given** an installation in read-only mode

**Then** no run starts, as before (BR-026); this Story adds no service write to
the read-only list beyond what a run already writes.

## AC-010 Tests never reach Google

Every test substitutes the Google ports; skipped reads are observed as calls
not made on the substituted reader (TC-4).

---

# Open Decisions

Both were resolved by the Owner on 2026-10-12, before activation (each as the
recommended option). Resolutions are kept next to the question.

## OD-001 When the weekly full run happens

Options: (a) a run is full when no full run has succeeded yet, or the last
successful full run is at least 7 days old — no weekday setting, survives
downtime; (b) the first run after midnight (school time zone,
`Installation:TimeZone`) on a fixed weekday; (c) as (b) with the weekday set by
the Admin.

**Resolution:** (a).

## OD-002 A full run that fails

Options: (a) the full-run time is stored only when the whole run succeeds, so
the next run is again full; (b) the full-run time is stored at the start of a
full run, so a failed one is not repeated until next week.

**Resolution:** (a).

---

# Notes

- A skipped course must not be treated as "roster read with nobody on it":
  US-014 marks memberships off roster only from a successful roster read; a
  skipped course has no read at all.
- The retention purge (US-037) deletes by stored dates; a skipped course's
  dates do not move, so purge behaviour is unchanged.
- The first run after this Story is deployed has no stored `updateTime` for any
  course and no full-run time, so it reads everything.
- Where the per-course `updateTime` lives (the mirrored `Course` row already
  holds Google's `updateTime`, but it is written from the list before the
  course's own reads) is for DB_DESIGN; the rule is that the stored "last
  complete read" value advances only when the course's transaction commits.
