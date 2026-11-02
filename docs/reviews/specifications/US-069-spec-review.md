---
artifact_type: specification_review
story: US-069
version: 3
status: APPROVED
created_at: 2026-11-02T11:12:57Z
updated_at: 2026-11-02T11:45:11Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-069-small-interface-texts-and-show-password.md
    version: null
  - path: trebovaniya.md
    version: 106
  - path: docs/specifications/US-069-spec.md
    version: 3
  - path: docs/specifications/US-069-spec-digest.ru.md
    version: 3
  - path: docs/decisions/US-069-open-decisions.md
    version: 1
supersedes: docs/reviews/specifications/US-069-spec-review.md@v2
---

# US-069 Specification Review (v3)

## Summary

**Verdict: PASS.** No Critical or Major finding. m-3 of review v2 is fixed in the Specification and
the digest. m-1 of v1 was accepted by the Owner on 2026-11-02, and the Specification and digest record
that acceptance correctly. One new Minor finding, m-4: one sentence of digest item 6 drops the word
"normally" that the Specification uses.

Nothing was delegated; every citation and code fact below was read directly, at the working tree on
top of `f081084`. No browser was run: statements about browser behaviour are reasoning.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-069-small-interface-texts-and-show-password.md` | none (Story has no version) |
| Requirements | `trebovaniya.md`, header v106, §8 «Подписи на страницах», «Показ пароля» | 106 |
| Specification | `docs/specifications/US-069-spec.md` | 3 |
| Digest | `docs/specifications/US-069-spec-digest.ru.md` | 3 |
| Open Decisions | `docs/decisions/US-069-open-decisions.md` | 1 |
| Previous review | this file | 2 |

Also read: NFR-077, SC-2 «Showing a typed password», SC-4 closed list, SC-14; the 9 password inputs;
`<h1>` across `src`; `Database/Index`, `Home/Index`, `MeetReports/Index`; the banner link in the
installation layout, the sign-in link in Control Plane `Setup/Index`; `Journal.Form.ChooseCourse` in
both translation files; `PageSearchHintTestData` and its use; a search of `src` for programmatic form
submits.

## Fidelity

Re-read against the cited text in this review.

| Spec | Source | Result |
|---|---|---|
| FR-001 role line | §8, NFR-077, OD-002 a | Matches. `#signed-in-role` holds the role text alone today |
| FR-002 one title | §8, NFR-077 | Matches. `<h1>` in `src`: the two layouts and exactly the four named views |
| FR-003 block names | §8, NFR-077, Story Scope 3 | Matches. Two `dl` in `#database-counters`, one in `#database-synchronization`, no heading today. Wording uk, per the Story; §8 gives the Russian form |
| FR-004 prompt | §8, NFR-077 | Matches. The blank option is in `MeetReports/Index`; the key exists in uk and en with the stated text |
| FR-005 table | §8, SC-2, OD-005 a | Matches. 9 `type="password"` inputs in 6 views, as listed |
| FR-005.1 label | Owner acceptance, ledger m-1 | Records the acceptance as it is in the ledger. See the note under Consistency |
| FR-005.3 attributes | §8, SC-2 | Goes beyond «у показанного пароля»; named in digest item 4 |
| FR-005.5 script | §8, SC-15, SC-4 | Matches. Static JS is on the SC-4 closed list |
| FR-006.2 value untouched | SC-2 | Matches |
| FR-006.4 hide on leaving | §8, SC-2, SC-14, Story AC-006 | Goes beyond the sources; named in digest item 1. Examples are real: the banner link and `/sign-in` link exist. The SC-14 statement is accurate: pages are `no-store`, static files are the only exception |
| FR-007, FR-008 | §8, NFR-077, SC-15 | Matches |
| Security Requirements | SC-2, SC-4, SC-10, SC-13, SC-14, SC-15 | Each statement agrees with the cited rule |

No rule without a source and no rule contradicting its source.

`src` holds no `.submit()` or `requestSubmit` call, so every form leaves through a `submit` event, and
`pagehide` would cover a programmatic one anyway.

## Story Coverage

AC-001…AC-011 keep the Story's ids and meaning; Scope items 1–5 map to FR-001…FR-008. Nothing goes
beyond the Story except the `pagehide` and manual-walk additions, both disclosed in the digest.

## Consistency

- Permission matrix, read-only mode (BR-026), audit (SC-11): not touched.
- Localization (NFR-073): every new text in both languages of the host using it.
- SC-14: now stated in FR-006.4, the manual steps, Traceability and Security Requirements, consistently.
- Existing tests: the v2 reading stands (no test pins the changed password markup). The Traceability note
  about adapting the `PageSearchHintTestData` ordinal is right in substance: `AfterTitle` counts `</h1>`
  and the page loses one. Where the value `2` lives is for TEST_WRITING to find.
- FR-005.1 ends with "AC-011 checks the result". AC-011 checks one control per field, its position and the
  console, not the focus behaviour of a press on the label or on the control. The described focus
  behaviour follows from the HTML rules with the input first and costs nothing if a browser differs, so
  this is noted here and is not a finding.

## Open Items

`trebovaniya.md` §7 holds no item this Story depends on. OD-001…OD-005 are resolved and carried with
their ids.

## Interpretations

1. Hide on `pagehide` as well as `submit`, and hide all shown fields at once (FR-006.4). Digest item 1.
2. Edge's own button stays when scripts are off (FR-008.2). Digest item 2.
3. The wording of the accessible names (FR-005.4). Digest item 3.
4. Spell check and auto-capitalisation off always, not only while shown (FR-005.3). Digest item 4.
5. Script on the 6 views only (FR-005.5). Digest item 5.
6. The dynamic behaviour is proved by a manual walk, by the Owner, with the listeners proved by the source scan and SECURITY_REVIEW (Acceptance Criteria). Digest item 6.
7. The `label` keeps wrapping the control (FR-005.1). Digest item 7, as accepted.
8. `<h2>` and a wrapper element in `#database-counters` (FR-003). Mechanics no user could tell apart; the look is checked at AC-011. Not needed in the digest.
9. A keyed placeholder for the role word (FR-001). Mechanics. Not needed in the digest.

## Digest Fidelity

Plain Russian, sections complete, no open decision hidden, Out of Scope faithful. Item 6 now tells the
Owner plainly that the "back" steps do not prove the listeners and who does. Item 7 retells the `label`
trade-off and the acceptance fairly. Every statement traces to the Specification except the wording in m-4.

## Previous Findings

**m-3 (v2, Minor): fixed.** The Specification says steps 2 and 3 confirm the outcome only and that the
listeners rest on the source scan and SECURITY_REVIEW; the Traceability row AC-006 and Security
Requirements agree; digest item 6 says the same.

**m-1 (v1, Minor): accepted by the Owner** at `HUMAN_SPEC_APPROVAL` on 2026-11-02, closed in the ledger.
FR-005.1 and digest item 7 now state it as accepted. The reason given (the Dean-accounts grid of US-068)
stands from review v2.

**M-1 and m-2 (v1):** fixed in Specification v2; nothing in v3 reopens them.

## Findings

### m-4 (Minor) — Digest item 6 states as certain what the Specification says is usual

**Where:** digest item 6, «Они пройдут, даже если наше скрытие не сработало, потому что страница и так
загрузится заново».

**Checked against:** the Specification's manual-walk paragraph («"back" normally loads the page anew, so
both steps can pass») and FR-006.4 («a browser may still restore a page in some cases»); digest item 1
itself says «обычно».

**What is wrong:** the digest drops "normally". Read literally, it contradicts its own item 1, and it
suggests the listeners can never matter in these steps.

**Why Minor:** it changes no decision and overstates a weakness rather than hiding one; the Owner gets the
right conclusion, that the listeners are proved elsewhere.

**Possible fix:** «Обычно они пройдут…», «…потому что страница, как правило, загрузится заново». This needs
no new loop on its own; the Owner may accept it as it stands.

## Verdict

**PASS.** Next: `HUMAN_SPEC_APPROVAL`. Open for the Owner there: m-4.
