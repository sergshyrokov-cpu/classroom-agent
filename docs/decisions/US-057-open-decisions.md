---
artifact_type: open_decisions
story: US-057
version: 2
status: APPROVED
created_at: 2026-10-25T18:05:00Z
updated_at: 2026-10-25T18:40:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-057-out-of-range-grades-do-not-stop-sync.md
    version: null
  - path: trebovaniya.md
    version: 98
  - path: docs/designs/database/US-057-db-design.md
    version: 1
supersedes: docs/decisions/US-057-open-decisions.md@v1
---

# US-057 Open Decisions

No Open Decision is unresolved.

- The Story's own "Open Decisions" section is empty: the Owner settled both cases in the
  requirements on 2026-10-25 (`trebovaniya.md` v98, `88183e2`).
- `trebovaniya.md` section 7 holds no open item this Story depends on.
- OD-001 below arose at DB_DESIGN (v1 BLOCKED, B-1/B-2) and was resolved by the Owner.

The interpretations the Specification makes where the requirements leave room are listed in
`docs/specifications/US-057-spec.md` §11 (I-1 … I-6) and in the Russian digest; they are confirmed
or corrected at `HUMAN_SPEC_APPROVAL`, not recorded here as Open Decisions.

## OD-001 Keep the non-negative constraints, or store negatives? — RESOLVED

**Question.** `ck_course_work_max_points_non_negative` and `ck_submission_grades_non_negative`
(US-015, re-pinned by US-055) refuse a negative maximum or grade. Spec v1 AC-010 required storing
−7.9·10²⁸, and the Story's AC-007 (carried as spec v1 AC-007) starts from a **stored** grade of
−7·10²⁸ — neither can be met under the constraints (DB design v1 B-1, B-2).

- **(a) Keep the schema (recommended by DB_DESIGN):** AC-010 tests only the upper bound; AC-007 is
  restated as a stored 0 points and a negative pair delivered to the report through a substituted
  journal port, with no stored row. No migration; DB_DESIGN is expected NOT_APPLICABLE.
- (b) Drop both constraints so negatives are stored as Google returned them (the v83 "mirror"
  rule) — a migration, reversing US-015 VR-001 and the US-055 pin.

**Resolution (the Owner, Serhii SHYROKOV, 2026-10-25T18:25:56Z, in conversation):** (a).

**Impact:** spec v2 AC-007 (restated, parts a and b), AC-010 (upper bound only), VR-002, FR-005
note, §1 code facts, §8, §10, §11 (I-6, L-1), §12. The Story's AC-007 is met in substance — the
negative rule of §4 v98 is proved at report-building level — but not with a stored negative row.

**Out of scope (recorded, not resolved):** an in-range negative value from Google (e.g. −1) is
still refused by the database and stops the run as `Unexpected` (spec L-1). A possible new
`trebovaniya.md` §7 item for the Owner.
