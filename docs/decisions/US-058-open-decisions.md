---
artifact_type: open_decisions
story: US-058
version: 1
status: APPROVED
created_at: 2026-10-26T19:36:21Z
updated_at: 2026-10-26T19:49:19Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-058-sync-marks-items-gone-and-records-skipped-courses.md
    version: null
  - path: trebovaniya.md
    version: 99
supersedes: null
---

# US-058 Open Decisions

- The Story's own "Open Decisions" section is empty: the Owner decided the behaviour on
  2026-10-26 (`trebovaniya.md` v99 `ad3865e`; BR-045, BR-046 in `5f048dd`).
- `trebovaniya.md` section 7 holds no open item this Story depends on.
- One Open Decision arose while writing the Specification; the Owner resolved it the same day.

The interpretations the Specification makes where the requirements leave room are listed in
`docs/specifications/US-058-spec.md` §11 (I-1 … I-7) and in the Russian digest; they are
confirmed or corrected at `HUMAN_SPEC_APPROVAL`, not recorded here as Open Decisions.

## OD-001 Where the "last successful read" date of a course comes from

**Status:** RESOLVED — option (a), by the Owner.

**Question.** `trebovaniya.md` §4 Epic 1 ("пропущенный курс") and BR-046: a loaded course that
synchronization skips is marked in its journal and report "данные не обновлялись с <даты
последнего успешного чтения курса>". The program stores no such date today: `Course` holds
`CompleteReadUpdateTime`, which is Google's `updateTime` at that read, not the instant the
program read it. Which Story stores the date?

**Options.**

- **(a) This Story stores it** *(recommended)*: each full read of a course stores the run's
  instant on the course (spec FR-008, AC-013). This is the v99 data Story; Story 2 then only
  displays. One more column at DB_DESIGN.
- **(b) Story 2 stores it**: FR-008 and AC-013 are removed from this Specification before
  DB_DESIGN.

**Impact.** Blocks nothing in FR-001…FR-007. Decides whether FR-008 / AC-013 stay and whether
DB_DESIGN adds the column. Either way, courses stored before the migration have no date until
their next full read.

**Resolution:** (a) — this Story stores the instant of each course's last full read (spec FR-008,
AC-013). Decided by Serhii SHYROKOV in conversation, 2026-10-26T19:40:06Z.
