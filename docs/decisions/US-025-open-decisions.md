---
artifact_type: open_decisions
story: US-025
version: 1
status: DRAFT
created_at: 2026-10-04T09:15:50Z
updated_at: 2026-10-04T09:15:50Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-025-journal-view-for-period.md
    version: null
  - path: trebovaniya.md
    version: 82
supersedes: null
---

# US-025 Open Decisions — Journal view for a period

Seven decisions (OD-001 … OD-007) were written into the Story by the author and
**resolved by the Owner on 2026-10-04, before activation**, each as the
recommended option (a). They are carried here with their ids and resolutions
unchanged.

Writing the Specification raised **no new Open Decision**. The choices the
Story and `trebovaniya.md` leave open are stated as interpretations I-1 … I-10
in `docs/specifications/US-025-spec.md` §11; each can be corrected at
`HUMAN_SPEC_APPROVAL`, and any of them may be turned into an Open Decision
there.

No item of `trebovaniya.md` §7 blocks this Story: item 26 (documentation) and
item 27 (re-import of a purged leaver) concern neither the journal's rows nor
its cells, and item 14 (submissions of a removed student) is already absorbed by
BR-051's off-roster membership, which this Story only reads.

---

## Carried from the Story

### OD-001 How the course is chosen

There is no course list yet (US-020).

Options: (a) the journal page itself has a simple drop-down of all synchronized
courses by name, with no filters or search — US-020 later adds the real list
and links into the journal; (b) do US-020 first and open the journal only from
it; (c) the course is typed as an id.

**Resolution:** (a) — Owner, 2026-10-04.

### OD-002 Where the US-025 / US-026 boundary lies

BR-056 defines the cells of graded work, ungraded work and materials in one
rule, and the epic map gives US-026 "distinguish graded work, ungraded work and
materials".

Options: (a) US-025 implements all three kinds — the cell is one computation and
testing it in two Stories duplicates work; US-026 is then closed in the catalog
as covered by US-025; (b) US-025 shows graded work only, and ungraded-work and
material columns appear with US-026.

**Resolution:** (a) — Owner, 2026-10-04. US-026 is removed from the catalog as
covered by this Story.

### OD-003 Full and short view on screen

BR-057: the short journal shows only assigned grades; the full journal also
shows draft grades. Layout belongs to templates (US-027).

Options: (a) one switch on the page, "full / short", that only changes whether
draft grades (and the last turn-in date) are shown; (b) the full view only —
the short form arrives with templates.

**Resolution:** (a) — Owner, 2026-10-04.

### OD-004 What the `NEW` state means in a cell

US-015 stores six recognised states, including `NEW`, which the Classroom API
documents but BR-056 did not mention (US-015 Specification VR-004).

Options: (a) before activation, a new version of `trebovaniya.md` adds `NEW` to
§4 Epic 3 as "not turned in, then by due date", like `CREATED`; (b) the same,
but `NEW` is shown as the "unrecognised state" until checked on a live domain;
(c) leave it open and block the Story.

**Resolution:** (a) — Owner, 2026-10-04. Done in `trebovaniya.md` v82 (§4 Epic
3) and BR-056.

### OD-005 A student without a name

A participant created from submissions alone has no name or email (US-015
OD-006).

Options: (a) the row shows a translated label "student without a name" and is
ordered after the named students; (b) the row also shows the Google user id so
two such students can be told apart; (c) such rows are hidden.

**Resolution:** (a) — Owner, 2026-10-04.

### OD-006 The default period

Options: (a) the current calendar month, editable; (b) no default — both dates
must be entered; (c) the last 30 days.

**Resolution:** (a) — Owner, 2026-10-04.

### OD-007 Row and column order

Options: (a) rows by full name in the alphabetical order of the UI language,
unnamed last; columns by date, then by title; (b) the order Classroom shows.

**Resolution:** (a) — Owner, 2026-10-04.
