---
artifact_type: open_decisions
story: US-028
version: 1
status: DRAFT
created_at: 2026-10-05T13:36:00Z
updated_at: 2026-10-05T13:52:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-028-excel-export.md
    version: null
  - path: trebovaniya.md
    version: 86
supersedes: null
---

# US-028 Open Decisions — Export a journal to Excel using a school template

Two carried from the Story, both resolved before activation; two raised at
`HUMAN_SPEC_APPROVAL` (OD-003 by the Specification, OD-004 by the Owner), both
resolved there. No item of `trebovaniya.md` §7 concerns this Story.

## OD-001 The Excel library

Carried from the Story. `AGENTS.md` requires an approved Open Decision to add a
NuGet package; `trebovaniya.md` §8 names ClosedXML and EPPlus.

Options: (a) ClosedXML — MIT licence, no cost; (b) EPPlus — paid licence for
commercial use since version 5.

**Resolution:** (a) ClosedXML, by the Owner on 2026-10-05 (discovery 2).
The Specification fixes **ClosedXML 0.105.1**, referenced by
`ClassroomAgent.Infrastructure.csproj` only (spec FR-011).

## OD-002 Who supplies the Excel layout

Carried from the Story. §3 and §8 ask for a school's own Excel layout attached
to a template; that needs upload, validation, storage and a field mapping.

Options: (a) the program produces the layout from the report; the upload is a
later Story; (b) US-028 includes the upload.

**Resolution:** (a), by the Owner on 2026-10-05 (discovery 1). The upload is
US-043.

## OD-003 Which "Grading" cells are numbers

Raised by the Specification. Resolved (a).

AC-003 asks for two things at once: every cell holds the same value as the
screen, and "a grade is a number Excel can calculate with, not text". They
agree for a 12-point grade alone (`12` is both). They conflict where the screen
shows more than a number:

- **"No conversion"** — the screen shows raw points out of the maximum, e.g.
  `8 / 10` (US-027 FR-015). That is not one number.
- **A grade with additions** — the late mark (when the template shows it), and
  in the full view the draft grade and the turn-in date (US-027 FR-005.4), e.g.
  `10 late`.
- **A scale label the school wrote** that is not a number, e.g. `зар.` or `A`.

The built-in "Academic journal" is unaffected in practice: short view, late mark
hidden, 12-point labels — every grade is a plain number.

Options:

- **(a) (Рекомендую)** A cell is a **number** when it holds a grade alone and the
  grade's text is a whole number (a scale label like `12`). Everything else —
  raw points `8 / 10`, a grade with a late mark, draft grade or date, a
  non-numeric label — is **text exactly as on screen**. The file never differs
  from the screen; the built-in journal and every numeric scale are calculable;
  only "no conversion" and the full view stay text, as they are on screen.
- **(b)** As (a), but under "no conversion" the cell holds the **points as a
  number** (`8`) and the maximum moves into the column header
  (`… (max 10)`). Calculable for "no conversion" too, but the cell differs from
  the screen (`8` vs `8 / 10`) — a departure from FR-003 that the screen must
  then follow or explicitly not follow.
- **(c)** Every grade is a number; additions (late, draft, date) go into an
  Excel **note** on the cell. Most calculable, but the printout loses the
  additions (notes do not print by default) and the layout departs further from
  the screen.

Impact: spec FR-004.5 and the AC-003 tests.

**Resolution:** (a), by the Owner on 2026-10-05 at `HUMAN_SPEC_APPROVAL`.

## OD-004 Page orientation of the printout

Raised by the Owner at `HUMAN_SPEC_APPROVAL`: a paper academic journal is
normally portrait, while spec v1 FR-004.8 fixed landscape. The orientation can
always be changed in Excel's print dialog; the question is the file's own
setting and whether the user chooses it.

Options:

- **(a)** Portrait by default; a portrait / landscape choice next to "Export to
  Excel", sent with the export, stored nowhere — no template or database change,
  within the Story's scope.
- **(b)** Orientation as a stored template setting — a new column and form
  field; the Story excludes changes to templates and per-school print
  configuration, so it would need a Story change (a later Story).
- **(c)** Always portrait, no choice; landscape is set in Excel when printing.

Note: in portrait, "Grading" with many columns fits one page wide only by
scaling down.

Impact: spec FR-001, FR-002, FR-004.8, VR-001, FR-013.

**Resolution:** (a), by the Owner on 2026-10-05 at `HUMAN_SPEC_APPROVAL`.
