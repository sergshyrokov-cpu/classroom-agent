---
artifact_type: specification_review
story: US-070
version: 3
status: APPROVED
created_at: 2026-11-02T19:56:30Z
updated_at: 2026-11-02T20:14:59Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-070-search-in-lists-and-pickers.md
    version: null
  - path: trebovaniya.md
    version: 109
  - path: docs/specifications/US-070-spec.md
    version: 4
  - path: docs/specifications/US-070-spec-digest.ru.md
    version: 4
  - path: docs/decisions/US-070-open-decisions.md
    version: 2
supersedes: docs/reviews/specifications/US-070-spec-review.md@v2
---

# US-070 Specification Review

## Summary

**Verdict: PASS.** No Critical, Major or Minor finding. The three Minor findings of review v2, which the Owner sent back at
`HUMAN_SPEC_APPROVAL` with the decision to disable the button, are fixed in Specification v4 and digest v4. The fixes were checked
against the text rather than the ledger's notes.

Nothing was delegated. Specification v3 is not on file (the artifact has not been committed), so this is not a diff: v4 and its digest
were read in full.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-070-search-in-lists-and-pickers.md` | no version field |
| Requirements | `trebovaniya.md` (Epic 2 "Поиск", §5 hint) | 109 |
| Specification | `docs/specifications/US-070-spec.md` | 4 |
| Digest | `docs/specifications/US-070-spec-digest.ru.md` | 4 |
| Open Decisions | `docs/decisions/US-070-open-decisions.md` | 2 |

Also read in this pass: BR-087, SC-14, `Views/MeetCodes/CourseChoice.cshtml`. Sources not re-read in this pass, relied on from reviews v1 and v2: the
remaining code locations, NFR-075, NFR-078, SC-10, SC-15, US-032 I-14, US-039 INFO-1.

## Fidelity

- **FR-001 to FR-004** match Epic 2 "Правила совпадения" and "Поиск в списках" and BR-087: the exact v108 labels, searchable fields, three apostrophes,
  hyphens in Meet codes only, and the Organizers column by the recorded resolution of OD-001.
- **FR-008** is the first half of the §5 text, as Story Scope item 7 says.
- **FR-009 item 8, I-15** (new in v4) are sourced from the Owner's decision at the gate, recorded in `history.jsonl` on 2026-11-02 («исправить R2-m-1…3,
  кнопку отключать»). Checked against the view: radios carry `required`, the submit button and "Cancel" sit in the same form, so the rule closes the case found
  in review v2. It does not disturb FR-009 item 4: a selected radio always stays, so the button stays enabled while a choice exists, and item 6 may show "nothing
  found" beside it, as that item says.
- **Section 7** cites SC-14 for the browser-history acceptance and "logs no query string"; SC-14 says both.
- **Invented rules:** none.

## Story Coverage

AC-001 to AC-010 are represented with the Story's ids, and Scope items 1 to 7 map to FR-001 to FR-009. Beyond the Story, all named: the Organizers column (OD-001 a), I-6, I-7, I-15.
Every FR now has a criterion: FR-006 is named in AC-003 and AC-004 and listed for both in section 13.

## Consistency

No change from review v2 in substance: US-032 I-14 and US-039 INFO-1 are reconciled openly, US-020's validation is reused, read-only mode, localization and the audit list are
respected, and no matrix cell is invented. Item 8 keeps the disabled state in the script: with scripts off the radios stay and `required` works as today.

Checked, not a finding: a course-choice page with no course at all has no field (FR-009 closing paragraph), so nothing changes there. Its button behaves as it does today.

## Open Items

OD-001 is resolved by the Owner with its options kept. No Open Decision is open. §7 items 26 and 30 do not block this Story and are not silently resolved.

## Interpretations

**Named in section 11 and digest section 2:** I-1, I-2, I-3, I-4, I-5, I-6, I-7, I-8, I-9, I-11, I-12, I-13, I-15. I-10 is withdrawn in the text. I-14 is skipped, and the text says why.

**Left in the text only, not a missing interpretation:**

| Place | Choice | Why it needs no digest line |
|---|---|---|
| FR-001.6 | A word of hyphens only does not match a code | The other reading would match every code, which nobody could want |
| VR-004 | A valid search still applies on the fallback list | Reached only by editing an address by hand |
| FR-009.5 | The current course is not narrowed | It is not a row and cannot be chosen there |
| FR-009.6 | Enter in the field does not submit | Follows from "the form submits as before" |

**Found by this review and not named:** none.

## Digest Fidelity

Every statement traces to Specification v4, each interpretation above is named with its id, and OD-001 is reported truly. The new lines are accurate: the button rule (I-15) is
stated as the Owner's own decision with its reason, the person's check says plainly that automated tests see only the markup and the script's text (AC-009), and the I-6 line now
reads as a page number past the last page. The language is plain Russian.

## Findings

None.

## Verdict

**PASS.** Ready for `HUMAN_SPEC_APPROVAL`.

## Record of Review v2 (Specification v3, PASS, sent back by the Owner)

Replaced by this version. Its findings, with the check made here:

| Id | Finding | Checked in Specification v4 |
|---|---|---|
| R2-m-1 | Narrowing could remove every radio on the course-choice page; the form then sent no course and landed on the error page | Fixed: FR-009 item 8, I-15, AC-009, digest |
| R2-m-2 | The digest omitted the person's check; the I-6 line was hard to read | Fixed: digest, both lines |
| R2-m-3 | FR-006 had no Acceptance Criterion and was not in the traceability table | Fixed: AC-003, AC-004, section 13 |

## Record of Review v1 (Specification v2, CHANGES_REQUIRED)

| Id | Finding | Checked in Specification v3 |
|---|---|---|
| M-1 | Candidates did not narrow, unnamed; "nothing found" could show beside a matching candidate | Fixed: FR-009 items 3, 5, 6, I-11, digest |
| m-1 | The absence mark shown but not matched, against I-3 and the digest | Fixed: FR-009.3, I-3, digest |
| m-2 | Small unnamed choices: hyphen-only word, teacher row link, refused text hides counters, search on the fallback list | Fixed: I-12, I-13 and digest; the other two stay in the text, as v1 allowed |
| m-3 | I-10 not in the digest | Fixed: I-10 withdrawn |
| m-4 | FR-009 named only `NoCoursesStored` for "no field"; SC-10 cited alone | Fixed: FR-009 closing paragraph, section 7 |

```yaml
result:
  verdict: PASS
  stage: SPEC_REVIEW
  story: US-070
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/specifications/US-070-spec-review.md
  next_stage: HUMAN_SPEC_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings: []
  closed_findings: []
```
