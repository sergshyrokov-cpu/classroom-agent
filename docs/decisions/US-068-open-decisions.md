---
artifact_type: open_decisions
story: US-068
version: 2
status: DRAFT
created_at: 2026-11-01T12:51:01Z
updated_at: 2026-11-01T13:40:12Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-068-restrained-look-with-bootstrap.md
    version: null
  - path: trebovaniya.md
    version: 105
supersedes: docs/decisions/US-068-open-decisions.md@v1
---

# US-068 Open Decisions

`trebovaniya.md` section 7 holds no open item this Story depends on. Both Open Decisions below are
carried from the Story with the Owner's resolution. No new Open Decision was raised while writing the
Specification; its interpretations (I-1…I-8) are listed in spec §11 for confirmation at
`HUMAN_SPEC_APPROVAL`.

## OD-001 How the Bootstrap files enter the repository

Carried from the Story.

- **a) (recommended)** — the implementor downloads the official Bootstrap 5.3 release archive (the
  latest 5.3 patch at least two weeks old) from the project's GitHub releases, checks the stylesheet
  against the integrity hash Bootstrap publishes for that version, and commits `bootstrap.min.css`
  and `LICENSE` unchanged; the version note records version, URL, date and hash. No package manager,
  no build step, nothing fetched at run time.
- b) — a .NET tool (LibMan) restores the files at build time: adds a tool and a network fetch to
  every build, against the "no new tool" rule.

Impact: spec FR-001, AC-002.

**Resolved 2026-11-01 by the Owner: a)** («всё по умолчанию»).

## OD-002 Colour palette

Carried from the Story.

- **a) (recommended)** — Bootstrap's default light palette: blue for links and the main button, grey
  tables and borders, the existing warning colours for marks; familiar and calm.
- b) — a monochrome grey palette with colour only for marks: even quieter, but links and buttons are
  harder to spot for first-time users.

Impact: spec FR-003.1, FR-005, FR-006, AC-007.

**Resolved 2026-11-01 by the Owner: a)** («всё по умолчанию»).

## OD-003 Which confirmation buttons get the danger colour (SPEC_REVIEW m-2)

Raised by SPEC_REVIEW v2 after approval of spec v2; resolved before TEST_WRITING. Supplements spec
FR-005.3 and I-3 and is binding for TEST_WRITING and IMPLEMENTATION; where FR-005.3 says
"installation status change", this decision narrows it.

FR-005.3 named "installation status change" among actions that "cannot be undone"; that includes
resuming an installation, which takes nothing away.

- **a) (recommended)** — the danger outline marks the submit button of a **consequential** action,
  reversible or not: Erasure confirmation («Видалити»), report-template deletion, revocation of an
  allowed Admin («Відкликати доступ»), and **suspension** of an installation («Призупинити»).
  Resumption («Відновити») uses the ordinary main-action style. Cancel links/buttons stay
  secondary outline.
- b) — only truly irreversible actions (Erasure, report-template deletion) are red; suspension and
  revocation use the ordinary style.
- c) — only Erasure and suspension, as drawn in the mockup.

Impact: spec FR-005.3, I-3; AC-007 (manual look check).

**Resolved 2026-11-01 by the Owner: a).**
