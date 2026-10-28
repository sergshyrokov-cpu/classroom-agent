---
artifact_type: open_decisions
story: US-066
version: 1
status: DRAFT
created_at: 2026-10-28T18:18:37Z
updated_at: 2026-10-28T18:18:37Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-066-namesakes-shown-with-email.md
    version: null
  - path: trebovaniya.md
    version: 104
supersedes: null
---

# US-066 Open Decisions

`trebovaniya.md` section 7 holds no open item this Story depends on (items 26, 27, 14, 28, 29
concern documentation, re-loading a purged leaver, submissions of a removed student, Meet fields and
delegation with blocked sign-in). No new Open Decision was raised while writing the Specification;
its interpretations are listed in spec §11 for confirmation at HUMAN_SPEC_APPROVAL.

## OD-001 How the email is written next to the name

Carried from the Story. v104 says "next to the name, in the same cell".

- **a) (recommended)** — in parentheses after the name on the same line:
  «Петренко Іван (ivan.petrenko@school.example)». One line keeps the printed row height; it reads the
  same on screen and on paper.
- b) — on a second line of the same cell: easier to read, but the printed row grows taller for those
  students only.

**Resolved 2026-10-28 by the Owner: a)** («1a»). Applied in spec FR-004.

## OD-002 Teachers with the same name

Carried from the Story. The report also lists the course's teachers («ПІБ Викладача»).

- **a) (recommended)** — students only, as v104 says; two teachers of one course with the same name
  are rare, and a teacher's line is not graded.
- b) — teachers too, by the same rule — needs a one-line amendment of `trebovaniya.md` first.

**Resolved 2026-10-28 by the Owner: a)** — after an explanation: teachers are one header line «ПІБ
Викладача» without grades, so a repeated name there misleads no one. Applied in spec FR-001, §10.
