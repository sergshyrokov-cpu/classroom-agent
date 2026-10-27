---
artifact_type: open_decisions
story: US-062
version: 1
status: APPROVED
created_at: 2026-10-27T18:35:09Z
updated_at: 2026-10-27T19:06:49Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-062-data-health-banner-and-page.md
    version: null
  - path: trebovaniya.md
    version: 103
supersedes: null
---

# US-062 Open Decisions

`trebovaniya.md` section 7 holds no open item this Story depends on (items 14, 26–29 concern
documentation, re-loading a purged leaver, Meet fields and delegation with 2SV). No new Open Decision
was raised while writing the Specification; its choices where the requirements leave room are listed
as interpretations I-1…I-11 in the Specification, section 11.

## OD-001 Which pages carry the banner

Carried from the Story. "Every installation page" (BR-047); pages a signed-in user sees are meant.

- **a) (recommended)** — every page rendered for a signed-in Admin or Dean except the forced password
  change: that page exists only to change the password, and its link would lead nowhere until the
  password is changed.
- b) — every page of the signed-in layout without exception.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Applied in spec FR-003.1, AC-015.

## OD-002 The empty-list date of an installation upgraded while the flag is set

Carried from the Story. At migration a stored `LastCourseListEmpty = true` has no date.

- **a) (recommended)** — take the start of the last run as the date: the latest certain instant; it
  understates the duration, never invents a longer one.
- b) — leave it without a date until the streak ends.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Applied in spec FR-008.2, AC-016.

## OD-003 Missing text variants (SPEC_REVIEW m-3)

Raised by SPEC_REVIEW after approval of spec v1; resolved before API_DESIGN. These wordings supplement
spec FR-007 and FR-001.6 and are binding for API_DESIGN, TEST_WRITING and IMPLEMENTATION.

| Where | Ukrainian | English |
|---|---|---|
| T3, loaded course, last successful read unknown (FR-001.6) | Курс не оновлюється | The course is not being updated |
| T3, never-loaded course (FR-001.6) | Курс ще не завантажено; пропускається з <дата> | The course has not been loaded yet; skipped since <date> |
| A page section with nothing to show (FR-004.2) | Немає | None |
| Confirmation succeeded (FR-006.2 step 6) | Позначено як видалені в Classroom: N | Marked as deleted in Classroom: N |
| Confirmation refused, list changed (FR-006.2 step 3) | Список змінився — оновіть сторінку | The list has changed — reload the page |
| Never-loaded course without a name (FR-001.6) | Курс без назви (<Google course id>) | Untitled course (<Google course id>) |

- **a) (recommended)** — the wordings above; the Google course id beside "untitled" lets the Admin find
  the course.
- b) — other wordings given by the Owner.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»).

## OD-004 An archived course skipped for an unknown state on the Dean's page (SPEC_REVIEW m-1)

FR-004.4 lists X3 of archived courses in the course-grouped archived section; FR-002.3 hides X3's
course name from the Dean.

- **a) (recommended)** — in the archived section the Dean sees such a problem as X3's title with hint
  H3 «Повідомте Адміністратора» and no course name; the Admin sees it with the course name. One rule
  everywhere: technical problems reach the Dean without details (§2 note on row 749).
- b) — show the course name to the Dean there too.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). FR-002.3 prevails over FR-004.4.

## OD-005 Zero in the banner (SPEC_REVIEW m-2)

- **a) (recommended)** — keep FR-003.2: both numbers always shown, zero included («0 технічних проблем,
  3 навчальні»); the banner always reads the same way.
- b) — show only the non-zero part.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). No change to the Specification.
