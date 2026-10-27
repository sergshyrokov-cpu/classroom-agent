---
artifact_type: open_decisions
story: US-060
version: 1
status: APPROVED
created_at: 2026-10-27T12:57:37Z
updated_at: 2026-10-27T13:12:30Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-060-marks-in-course-list-journal-report-and-export.md
    version: null
  - path: trebovaniya.md
    version: 102
supersedes: null
---

# US-060 Open Decisions

`trebovaniya.md` section 7 holds no open item this Story depends on (items 26, 27, 14, 28, 29
concern documentation, re-loading a purged leaver, Meet fields and delegation). Item 27 concerns
leavers, but their purge, not their display.

## OD-001 Marks in the «Бланк для групових занять» layout

Carried from the Story. The export has two layouts; the requirements say "in the export" without
naming one.

- **a) (recommended)** — the same marks wherever the form has the cell: colour and note on an
  above-maximum grade cell, note on a «выбыл» name cell; the header and item marks only if the form
  has a place for them, otherwise not. The form's printed look stays unchanged — a note is not
  printed.
- b) — program layout only; the form stays without marks.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Applied in spec FR-008; how the
header and item marks find their place is interpretation I-4.

## OD-002 "Not updated since" when the last successful read is not known

Carried from the Story.

- **a) (recommended)** — show «данные не обновлялись» without a date, as v101 does for «выбыл»
  when the date is unknown.
- b) — show the date the skip streak began or the mark's date — a later date than the real one.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Applied in spec FR-003.1,
AC-011.

## OD-003 Telling a submission-only student from a student who left

Raised while writing the Specification; resolved at HUMAN_SPEC_APPROVAL (see below).

BR-068 marks a student off the roster "left <last-seen date>", and one known only through
submissions "left" with no date. Synchronization stores both alike: a submission-only student gets a
membership created and taken off the roster in the same run, so its first and last sightings are
equal (`RunSynchronizationUseCase.cs:1030-1033`). A student seen on the roster in exactly one run
and gone by the next looks the same. No stored field tells them apart.

- **a) (recommended)** — no schema change: an off-roster membership whose first and last sightings
  are equal shows "left" with no date; any other shows "left <last seen>". Why: it never shows a
  wrong date; the only loss is the date of a student who appeared on one roster read and left
  before the next (within one synchronization interval), who gets "left" without it.
- b) — store from now on whether a membership was ever seen on the roster (a new field and its
  migration, set by synchronization). Exact for new cases, but existing rows still need rule a), so
  the gain is limited to students who arrive and leave after this Story — and it changes
  synchronization, which the Story keeps out of scope.

**Resolved 2026-10-27 by the Owner at HUMAN_SPEC_APPROVAL: a)** (`/so:approve` comment «OD-003 a)»).
Applied in spec FR-006.1 and VR-005; no schema change.
