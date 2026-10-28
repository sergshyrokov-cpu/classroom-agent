---
artifact_type: open_decisions
story: US-063
version: 1
status: DRAFT
created_at: 2026-10-28T16:56:01Z
updated_at: 2026-10-28T16:56:01Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-063-generated-and-data-as-of-in-journal-and-export.md
    version: null
  - path: trebovaniya.md
    version: 104
supersedes: null
---

# US-063 Open Decisions

`trebovaniya.md` section 7 holds no open item this Story depends on (items 26, 27, 14, 28, 29
concern documentation, re-loading a purged leaver, submissions of a removed student, Meet fields and
delegation with blocked sign-in). No new Open Decision was raised while writing the Specification;
its interpretations are listed in spec §11 for confirmation at HUMAN_SPEC_APPROVAL.

## OD-001 «Данные из Google на» when the last read is not known

Carried from the Story.

- **a) (recommended)** — show the line with «невідомо» / «unknown»: the line is always there, so its
  absence is never mistaken for fresh data; the same answer as US-060 OD-002.
- b) — leave the line out for such a course.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Applied in spec FR-002.3, AC-006.

## OD-002 Which instant is «Данные из Google на» for an unchanged archived course

Carried from the Story.

- **a) (recommended)** — the last complete read (`Course.LastCompleteReadAt`), as v103 says
  literally and as US-060 uses for «данные не обновлялись с».
- b) — the later of the last complete read and the last listing that confirmed the course unchanged;
  would need a sentence in a new requirements version first.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Applied in spec FR-002.1, VR-002.

## OD-003 The course-name cell of a blank with several copies

Carried from the Story.

- **a) (recommended)** — the note goes on every course-name cell: whichever copy or sheet the Dean
  looks at, the note is there; it is not printed anyway.
- b) — only on the first course-name cell of the file.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Applied in spec FR-005.3 and,
by the same reasoning, to both sheets of the program layout (FR-004.2, interpretation I-4).
