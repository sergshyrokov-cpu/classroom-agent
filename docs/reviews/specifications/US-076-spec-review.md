---
artifact_type: specification_review
story: US-076
version: 3
status: APPROVED
created_at: 2026-11-07T06:41:35Z
updated_at: 2026-11-07T10:56:23Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-076-page-help.md
    version: null
  - path: trebovaniya.md
    version: 112
  - path: docs/specifications/US-076-spec.md
    version: 3
  - path: docs/specifications/US-076-spec-digest.ru.md
    version: 3
  - path: docs/decisions/US-076-open-decisions.md
    version: 1
supersedes: 2
---

# US-076 Specification Review (v3)

## Summary

Third review, after the loop-back from `IMPLEMENTATION` (finding US-076/IMPLEMENTATION/B-1, Owner's
decision 2026-11-07, option 1). Specification v3 changes only AC-006, section 9 and adds Interpretation
I-8; the digest v3 adds one paragraph naming I-8. Everything review v2 checked (help texts of section 4.1,
FR-001…FR-010, AC-001…AC-007, I-1…I-7) is textually unchanged — verified by diffing v2 against v3 — and
review v2's conclusions stand. No Critical or Major finding. One Minor finding on the digest's wording.
Verdict **PASS**.

Nothing was delegated. The reviewer read the v2→v3 diff of both documents, the Story, review v2, and the
test the change is about (`tests/ClassroomAgent.Tests/Web/Pages/JournalMarksPageTests.cs:157–181`,
`TestInfrastructure/DataMarksWorld.cs:83–86`) directly.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-076-page-help.md` | — |
| Requirements | `trebovaniya.md` | 112 |
| Specification | `docs/specifications/US-076-spec.md` | 3 |
| Digest | `docs/specifications/US-076-spec-digest.ru.md` | 3 |
| Open Decisions | `docs/decisions/US-076-open-decisions.md` | 1 |

Inputs are current: the Specification names `trebovaniya.md` v112 and Open Decisions v1; the digest names
Specification v3; both `supersedes` their v2.

## Fidelity

The v3 change is about test scope, not product behaviour: it adds no rule, no endpoint, no text and no
permission. It is grounded in a recorded human decision (B-1 option 1, history event 2026-11-07T10:52:08Z)
and cited as such (I-8). Checked against the test: `AssertHeaderLine` finds the variant's header line once,
above the table, and asserts that the stems of the other header lines (`Курсу немає в Classroom з`,
`Дані не оновлювалися з …`) occur nowhere else in the page text. Journal help line 2 (unchanged since v2,
approved wording) names both stems, so the test fails on a correct page. The diagnosis in I-8 is accurate.

"The assertion itself is not weakened" holds: excluding the help's static `<details>` keeps every claim the
test makes about the page's own content; the help block is covered by the Story's own help tests. Product
AC-006 of the Story ("what they show … unchanged") is not touched — the page content outside the help is
still asserted unchanged.

## Story Coverage

Unchanged from review v2: AC-001…AC-007 each represented; Scope item 5 is FR-010. The v3 addition stays
within AC-006 and section 9 (testing). Nothing beyond scope.

## Consistency

No change to permissions, read-only mode, localization, audit, SC-15 or NFR-075. Section 9 assigns
finding every affected test to `TEST_WRITING`, consistent with stage ownership (`stage-map.yaml`) and with
the Story Notes. The Specification correctly keeps help texts approved at v2 unchanged.

## Open Items

No new `trebovaniya.md` section 7 dependency. OD-001 resolved and carried. Finding B-1 stays OPEN — it is
an `IMPLEMENTATION` finding and is closed only once `TEST_WRITING` adjusts the tests; this stage does not
close it.

## Interpretations

I-1…I-7 as in review v2. New: **I-8** — existing page tests that search the whole page for a phrase the
help also contains read the page outside the help block. Named in Specification section 13 and in digest
section 2 with the alternative not taken. No further unnamed interpretation found in the delta.

## Digest Fidelity

The digest v3 header paragraph and the new first bullet of section 2 trace to Specification v3 (I-8,
AC-006, section 9); the decision is presented as the Owner's, the alternative is named, and the "what the
test checks does not change" promise matches the Specification. Plain Russian. One imprecision — m-5.

## Findings

### m-5 (Minor) — digest simplifies what the journal test checks

Digest section 2, first bullet: «старый тест журнала проверяет, что на всей странице такая строка ровно
одна». The test (`JournalMarksPageTests.cs:157–181`) also checks that the *other* header line does not
appear anywhere, and in the variants with no header line that neither appears — that is the part the help
breaks most often. The digest's conclusion is still right; only the description is narrower than the test.
Advisory: no wording change needed for approval; TEST_WRITING should find failures on both checks.

### Carried

Review v2 m-1 (ledger US-076/SPEC_REVIEW/m-4, capitalised «Вибув(-ла)» at the start of journal line 2)
is still worded so in section 4.1, but it was closed FIXED at API_DESIGN: the resource key
`PageHelp.Journal.Line2` begins «Позначка «вибув(-ла)» з датою…» as the Owner decided at
HUMAN_SPEC_APPROVAL (`docs/designs/api/US-076-api-design.md` 3.1). Not re-raised. Specification findings
m-1/m-2 of section 14 unchanged.

## Verdict

**PASS** — no Critical or Major finding. Ready for `HUMAN_SPEC_APPROVAL`.
