---
artifact_type: open_decisions
story: US-033
version: 3
status: DRAFT
created_at: 2026-10-10T19:39:19Z
updated_at: 2026-10-11T00:00:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-033-meet-reports.md
    version: null
  - path: trebovaniya.md
    version: 88
supersedes: 1
---

# US-033 Open Decisions — Built-in Meet reports for a course and period

**Twelve, all resolved.** OD-001 … OD-010 come from the Story: the Owner decided them on
2026-10-10 in discovery, before activation. TEST_WRITING raised OD-011 and OD-012,
resolved by the Owner the same day. Writing the Specification raised none:

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

## OD-011 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING. The tests reference declarations that do not exist yet:
`IMeetReportSource` and its four records, the two requests, outcomes and
results, the page DTOs and enums of `US-033-openapi.yaml`, `GetMeetReportsQuery`,
`GetMeetReportMeetingQuery`, `MeetReportSource`, the policy name
`ViewMeetReports` and the two route constants. The test-writer may not change
production code. US-025, US-027, US-031, US-032, US-037, US-039 and US-042
resolved the same gap the same way.

Options: (a) a compile-only skeleton — declarations only; members throw
`NotImplementedException`; nothing registered in DI, no migration, no existing
behaviour changed; IMPLEMENTATION owns and completes it; (b) no skeleton.

**Resolved (a)** — Owner, 2026-10-10.

## OD-012 A person without an email (contract adjustment)

Raised at DB_DESIGN (db-design §7) and recorded by TEST_WRITING.
`classroom_participant.email` is nullable, and a student without an email is
still a report-3 row (spec FR-009). `PersonRef.email` in `US-033-openapi.yaml`
v1 is required.

Options: (a) adjust the contract by this decision: `PersonRef.email` is
nullable; a person with neither a full name nor an email is shown with the
translated label `MeetReports.Person.Unnamed`, as the journal does
(`Journal.Row.Unnamed`). A resolved Open Decision outranks the API design
(`AGENTS.md`, order of authority), so no loop-back is needed; (b) loop back to
API_DESIGN for a v2 contract and re-run DB_DESIGN.

**Resolved (a)** — Owner, 2026-10-10. Impact: openapi `PersonRef` (email
nullable), spec FR-011 (fallback label); the skeleton's `PersonRef(string?
FullName, string? Email)`.

## OD-013 A host test's period contradicts spec FR-009

Raised by IMPLEMENTATION. `MeetReportsPageTests.WithoutMeetings_ReportTwoIsTheEmptyLine_AndTheStudentsAreStillListed`
asked for the period 2026-01-01 … 2026-01-31 and expected the seeded students in report 3. The seeded memberships are
first seen on 2026-09-01, so under spec FR-009 (`FirstSeenAt < end`) they are not students of a January period, and
`MeetReportsStudentsTests.OnlyStudentsOfThePeriod_AreRows` asserts exactly that. The test's data, not the code, is
wrong.

Options: (a) change the test's period to 2026-11-02 … 2026-11-30 — the students are on the roster and the course has
no meeting then, so the test keeps its meaning (AC-012); (b) loop back to TEST_WRITING.

**Resolved (a)** — Owner, 2026-10-11. Impact: one line of the test; no production change.
