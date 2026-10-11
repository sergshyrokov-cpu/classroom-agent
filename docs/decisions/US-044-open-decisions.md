---
artifact_type: open_decisions
story: US-044
version: 2
status: DRAFT
created_at: 2026-10-11T06:39:47Z
updated_at: 2026-10-11T07:30:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-044-meet-reports-export.md
    version: null
  - path: trebovaniya.md
    version: 88
supersedes: null
---

# US-044 Open Decisions — Export the built-in Meet reports to Excel

**Fourteen, all resolved.** OD-001 … OD-003 were decided by the Owner in US-033
discovery (2026-10-10), OD-004 … OD-012 in US-044 discovery (2026-10-11), all
before activation. Writing the Specification raised none:

- `trebovaniya.md` v88 §2 grants Admin and Dean the export of Meet statistics,
  §5 audits "экспорт журнала или отчёта", §8 makes an export a `POST`.
- §7 has no open item on Meet reports or export. Item 28 (`call_ended` field
  names) concerns US-031's ingestion; this Story's tests use synthetic data.

The wording leaves some choices open. They are interpretations I-1 … I-10 in
`docs/specifications/US-044-spec.md` §11; any of them can be turned into an
Open Decision at `HUMAN_SPEC_APPROVAL`.

## OD-001 Export format

**Resolved**: Excel only; Word later with US-029.
Impact: §10 Out of Scope.

## OD-002 Layout

**Resolved**: a built-in, fixed layout, not a school template.
Impact: FR-005, FR-006; §10.

## OD-003 Course workbook

**Resolved**: one workbook per course and period with the sheets Summary,
Meetings and Student participation («Участь студентів»); report 4 exported
separately from the meeting's page.
Impact: FR-005, FR-006.

## OD-004 Selecting rows

**Resolved (a)**: in the Excel file — an autofilter on each table's header row
and a frozen header. No on-screen filter or search, and no new requirement.
Impact: FR-007; §10.

## OD-005 What is exported

**Resolved (a)**: exactly what the page shows (the same course and period). The
button appears only when reports are built.
Impact: FR-002, FR-003.

## OD-006 Report 4

**Resolved (a)**: its own button and a one-sheet file; not added to the course
workbook.
Impact: FR-006; §10.

## OD-007 Header block

**Resolved (a)**: course, period, export date and time in the school's time
zone, and the Meet data-delay line, as in the journal export.
Clarified at `HUMAN_SPEC_APPROVAL` (Owner, 2026-10-11): "as in the journal
export" refers to the manner of the header block; the export time (the instant
the file is built) and the delay line are added for the Meet export only, and
the journal export is unchanged.
Impact: FR-004; finding F-2.

## OD-008 Durations and minutes

**Resolved (a)**: numeric cells; the total duration is in minutes.
Impact: FR-005, FR-006, FR-007.

## OD-009 Orientation

**Resolved (a)**: chosen at export, landscape by default, stored nowhere.
Impact: FR-001, FR-002, FR-007, VR-003.

## OD-010 File name

**Resolved (a)**: US-028's rule with a `Meet` prefix; no person's name or email.
Impact: FR-008.1.

## OD-011 Audit

**Resolved (a)**: one row per export, report 4 included (course id, meeting id,
row count).
Impact: FR-013; finding F-1.

## OD-012 Language of the file

**Resolved (a)**: the user's UI language; Google data untranslated.
Impact: FR-010, FR-015.

## OD-013 How a person appears in the file

Raised at `HUMAN_SPEC_APPROVAL` from interpretation I-1 of the Specification.

Options: (a) one text cell composed as on screen (name with email), keeping the
Meetings sheet at the nine columns the Story names; (b) name and email in
separate columns everywhere a person appears — the organizer of report 2 (name,
email, mark), the student of report 3, the person of report 4 and the organizer
in report 4's header — so that each can be sorted and filtered in Excel.

**Resolved (b)** — Owner, 2026-10-11: more convenient for the user's later work
in Excel. Impact: spec FR-003, FR-004, FR-005.2, FR-005.3, FR-006, FR-007
("Persons"), FR-015.

## OD-014 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING. The tests reference declarations that do not exist yet:
the two requests, commands and outcomes, `MeetExportFileName`, the two
`AuditAction` values and `AuditEvent` factories and properties, the opt-in
workbook-model members of entity model §3, and the `Export` members of the two
page models. The test-writer may not change production code. US-025 … US-042
resolved the same gap the same way.

Options: (a) a compile-only skeleton — declarations only; members throw
`NotImplementedException`; nothing registered in DI, no migration, no existing
behaviour changed; IMPLEMENTATION owns and completes it; (b) no skeleton.

**Resolved (a)** — Owner, 2026-10-11.
