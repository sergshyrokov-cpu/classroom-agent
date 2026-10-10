---
artifact_type: open_decisions
story: US-033
version: 1
status: DRAFT
created_at: 2026-10-10T19:39:19Z
updated_at: 2026-10-10T19:39:19Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-033-meet-reports.md
    version: null
  - path: trebovaniya.md
    version: 88
supersedes: null
---

# US-033 Open Decisions — Built-in Meet reports for a course and period

**Ten, all resolved.** All ten come from the Story: the Owner decided them on
2026-10-10 in discovery, before activation. Writing the Specification raised
no new one:

- `trebovaniya.md` v88 (§2, §3, §4 Epic 4) fixes the four reports, their
  columns, the counting rules (v58) and the roster on the meeting's date (v31).
- §7 has no open item on the reports (question 16 was closed in v58). Item 28
  (`call_ended` field names) concerns US-031's ingestion; this Story reads only
  stored data, and its tests use synthetic data (TC-4).

The wording leaves some choices open. They are written down as interpretations
I-1 … I-12 in `docs/specifications/US-033-spec.md` §11. At
`HUMAN_SPEC_APPROVAL` any of them can be turned into an Open Decision.

## OD-001 Scope

**Resolved (a)**: on-screen reports only; export moves to a new Story, US-044.
Impact: §10 Out of Scope.

## OD-002 Export format

**Resolved (a)**: Excel only; Word later with US-029. Carried to US-044; no
effect on this Specification.

## OD-003 Export file

**Resolved (a)**: one Excel workbook per course and period, with the sheets
"Summary", "Meetings" and "Student participation" («Участь студентів»). Report 4
is exported separately from the meeting's page. The layout is built in and
fixed, not a school template. Carried to US-044. Its only effect here is the
Ukrainian label of report 3 (FR-009, FR-016).

## OD-004 Which period a meeting belongs to

**Resolved (a)**: a meeting belongs to the day of its start in the school's time
zone, and wholly to that day.
Impact: FR-003, FR-004.

## OD-005 A person's minutes in one meeting

**Resolved (a)**: the sum of the person's connections, capped at the meeting's
duration; shown as "N of M min", with no percentage.
Impact: FR-006, FR-009, FR-010.

## OD-006 How a person is shown

**Resolved (a)**: the name from the Google profile, with the email beside it.
Impact: FR-011.

## OD-007 Which courses can be chosen

**Resolved (a)**: every synchronized course, archived ones included, through
the journal's drop-down. Search comes with US-020.
Impact: FR-002.

## OD-008 Data delay notice

**Resolved (a)**: the notice, plus the time up to which Meet meetings are
loaded, shown to both Dean and Admin. No change to `trebovaniya.md`: the §2
matrix keeps only sync *diagnostics* Admin-only.
Impact: FR-012.

## OD-009 Unconfirmed automatic links

**Resolved (a)**: such links are not marked in the reports.
Impact: FR-004.

## OD-010 Navigation and default period

**Resolved (a)**: a "Meet reports" menu item; reports 1–3 on one page; report 4
opens from a row of report 2. On first opening, the period is the current month.
Impact: FR-001, FR-003, FR-010.
