---
artifact_type: open_decisions
story: US-027
version: 1
status: DRAFT
created_at: 2026-10-04T18:57:01Z
updated_at: 2026-10-04T18:57:01Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-027-report-templates.md
    version: null
  - path: trebovaniya.md
    version: 84
supersedes: null
---

# US-027 Open Decisions — Report templates and the on-screen report

Three decisions (OD-001 … OD-003) were written into the Story by its author and
**resolved by the Owner on 2026-10-04, before activation**. They are carried here
with their ids and resolutions unchanged.

Writing the Specification raised **one new Open Decision, OD-004**, which is
**unresolved** and must be resolved at `HUMAN_SPEC_APPROVAL`: the publication
date that `trebovaniya.md` v84 makes the lesson date cannot be computed from
what the program stores today.

The other choices the Story and `trebovaniya.md` leave open are stated as
interpretations I-1 … I-16 in `docs/specifications/US-027-spec.md` §11; each
can be corrected at `HUMAN_SPEC_APPROVAL`, and any of them may be turned into an
Open Decision there.

No item of `trebovaniya.md` §7 blocks this Story: item 26 (documentation) and
item 27 (re-import of a purged leaver) do not concern templates or the report,
and item 14 (submissions of a removed student) is already absorbed by BR-051's
off-roster membership, which this Story only reads.

---

## Carried from the Story

### OD-001 Which date is the lesson date, and which items fall in the period

The Owner chose the publication date as the lesson date (discovery 7.1). The
journal of US-025 places an item in a period by the BR-052 cascade
`scheduledTime` → `dueDate` → `updateTime` → `creationTime`, where the due date
comes second. An item published on 28 September and due on 2 October is in
October by BR-052 but its lesson date is 28 September.

Options: (a) **(recommended)** in a template report both the lesson date and the
period membership use the publication date (`scheduledTime` if set, otherwise
`creationTime`); the US-025 journal keeps BR-052 — the report is consistent with
itself; (b) the lesson date is the publication date, membership stays BR-052 —
a column dated outside the chosen period can appear; (c) both use BR-052 — the
"lesson date" is then often a due date.

**Resolution:** (a), by the Owner on 2026-10-04; written into `trebovaniya.md`
v84 §4 Epic 3. Its implementation depends on OD-004.

### OD-002 The ranges of the preset scales

There is no single official "percent → 12 points" table; the presets must carry
the ranges the school uses (discovery 4.2a). The Owner provides the ranges for
the 12-, 5- and 100-point scales and ECTS.

**Resolution:** by the Owner on 2026-10-04 — (1) the even 12-point table
becomes the preset, the percent rounded to a whole number, 0.5 up: 1 — 0–8 %,
2 — 9–16, 3 — 17–25, 4 — 26–33, 5 — 34–41, 6 — 42–50, 7 — 51–58, 8 — 59–66,
9 — 67–75, 10 — 76–83, 11 — 84–91, 12 — 92–100 %; (2) v1 ships no other preset:
the 5- and 100-point scales and ECTS are entered by hand, and a preset is added
when a school asks for one. Written into `trebovaniya.md` v84 §4 Epic 3.

### OD-003 The built-in template's marks and scale

The built-in template cannot be edited, so its settings are fixed by the
program.

Options: (a) **(recommended)** it shows only grades, converted by the 12-point
preset of OD-002; every other state is empty, except "not assigned", shown as a
dash (BR-056 print rule); (b) the same, with "no conversion" — raw points, so it
is correct for any school, and a school's scale lives only in a copy; (c) other
marks the Owner names.

**Resolution:** (a), by the Owner on 2026-10-04. It depends on OD-002 for the
12-point ranges.

---

## Raised by the Specification

### OD-004 `scheduledTime` is not stored, so the publication date cannot be computed

**Status: OPEN — blocking.** Affects FR-005 (lesson date and period membership),
AC-004, AC-012 and DB_DESIGN.

**The gap.** `trebovaniya.md` v84 §4 Epic 3 and OD-001 (a) define the lesson
date as `scheduledTime` if set, otherwise `creationTime`. The synchronization of
US-015 **reads** `scheduledTime` from Google (`GoogleClassroomReader`, to compute
`ItemDate` by the BR-052 cascade) but **does not store it**: `CourseWork` holds
`ItemDate`, `DueAt`, `CreationTime` and `UpdateTime` only. `ItemDate` cannot
stand in for it — when `scheduledTime` is absent it is the due date, and the two
cannot be told apart reliably.

The Story puts "any change to synchronization or to the Google mirror" out of
scope, so the Specification cannot add the field on its own authority.

**Options:**

- **(a) (recommended)** This Story stores `scheduledTime`: `CourseWork` gains a
  nullable `ScheduledTime`, the synchronization writes it for coursework and
  materials alike from the value it already reads, with an EF Core migration
  (PC-2). The Story's out-of-scope line is narrowed to "no other change to
  synchronization or the mirror". Items imported before the change have no
  `ScheduledTime` until the next synchronization run updates them; until then
  their lesson date is `creationTime`, which differs only for items that were
  scheduled for later publication. Nothing new is read from Google and no scope
  changes (SC-6).
- **(b)** The report uses `creationTime` only in v1. A scheduled item then gets
  the date its draft was created, which can be weeks before the lesson. This
  contradicts v84 and needs a new version of `trebovaniya.md` first.
- **(c)** A separate Story extends US-015 to store `scheduledTime`; US-027 waits
  for it. Same result as (a), one more Story to run.

**Resolution:** _open — to be decided by the Owner at `HUMAN_SPEC_APPROVAL`._
