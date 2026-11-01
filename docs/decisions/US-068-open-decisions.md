---
artifact_type: open_decisions
story: US-068
version: 1
status: DRAFT
created_at: 2026-11-01T12:51:01Z
updated_at: 2026-11-01T12:51:01Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-068-restrained-look-with-bootstrap.md
    version: null
  - path: trebovaniya.md
    version: 105
supersedes: null
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
