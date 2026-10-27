---
artifact_type: specification_review
story: US-062
version: 1
status: APPROVED
created_at: 2026-10-27T18:56:10Z
updated_at: 2026-10-27T18:56:10Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-062-data-health-banner-and-page.md
    version: null
  - path: trebovaniya.md
    version: 103
  - path: docs/specifications/US-062-spec.md
    version: 1
  - path: docs/specifications/US-062-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-062-open-decisions.md
    version: 1
supersedes: null
verdict: PASS
---

# US-062 Specification Review

## Summary

PASS. The Specification is faithful to `trebovaniya.md` v103 (§2 rows 749–750 and their notes,
§4 Epic 1 "Состояние данных" lines 1136–1190, §5 audit line 1745) and to BR-025, BR-026, BR-027,
BR-043, BR-045…BR-049, BR-067, BR-081. Every Story AC is adopted; both Open Decisions are resolved
and applied. No Critical or Major finding; three Minor findings (m-1…m-3).

Delegation: none. The reviewer read every cited passage itself.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-062-data-health-banner-and-page.md` | (no version field) |
| Specification | `docs/specifications/US-062-spec.md` | 1 |
| Digest | `docs/specifications/US-062-spec-digest.ru.md` | 1 (input: spec v1 — current) |
| Open Decisions | `docs/decisions/US-062-open-decisions.md` | 1 |
| Requirements | `trebovaniya.md` | 103 |

## Fidelity

- **Teaching problems T1–T4** match BR-047 / Epic 1 line 1139–1143: points above the maximum (BR-067,
  "a grade or draft grade" supports I-10's drafts), items "not in Classroom" (BR-045), skipped for
  out-of-range or negative values (BR-043) with the teacher hint, course "not in Classroom" (BR-048)
  with the deleted-or-hidden hint. Hint wordings H1, H2 are faithful Ukrainian renderings.
- **Technical problems X1–X6** match the BR-047 list one to one. X2's read-only exclusion and "none
  yet" variant: BR-047 last sentence, Epic 1 line 1160. X4's date and its replacing staleness: BR-048
  ("Such a run counts as successful… this problem takes its place"); FR-008.3 restates it. X5 counted
  once with archived courses included: BR-047, BR-049. X6: 24 h strictly, grace end = last success +
  7 days, disappears in read-only mode, Owner hint for the Admin, `upgrade_required` reason (BR-027,
  BR-081 — an `upgrade_required` answer does not move the last success time, so X6 covers it).
- **Counting** (FR-001.5): "one course × one kind", problems without a course count one, archived not
  counted — Epic 1 lines 1163–1166, 1176–1178.
- **Visibility** (FR-002): §2 row 749 both roles; note on row 749 "Декан видит, что техническая
  проблема есть… Диагностика только у Админа" — FR-002.3 and §7 "Detail restriction".
- **Confirmation** (FR-006): §2 row 750 Admin only; Epic 1 lines 1131–1134 (mark dated by the
  confirmation, BR-048 thereafter, audited, blocked in read-only mode); BR-026 lists it; §5 line 1745
  audit with course ids.
- **Empty-list date** (FR-008): BR-048 "the run since which the lists have come back empty in a row";
  OD-002 a) for the migration; NFR-033 for keeping the flag.
- Cited code facts (§1.1) were taken as the spec writer's lookup; they constrain design, not
  requirements, and DB_DESIGN/TEST_WRITING verify them by compiling.

## Story Coverage

AC-001…AC-014 adopted with readings consistent with the Story's Given/When/Then; the AC-001 reading
fixes X2's absence by fixture, which the Story's expected count implies. AC-015 (OD-001 a), AC-016
(OD-002 a), AC-017 (all-or-nothing, I-5) are additions traced to resolved decisions or a listed
interpretation. Out of scope matches the Story and adds two items (undoing the confirmation;
synchronization changes beyond FR-008), both implied by BR-049 and the Story scope.

## Consistency

- Permission matrix: no invented cell; the page and banner both roles, the confirmation Admin only.
- Read-only (BR-026, AD-6): views work, the confirmation is guarded in Application (§7).
- Localization (NFR-073): all texts in translation files; Google names untranslated.
- Audit (SC-11, §5 v101): one event, ids only.
- US-060 OD-002 a) (no date when the last read is unknown) reused in FR-001.6 — consistent.
- NFR-075 extended to the new page (I-7) — the NFR's list names pages that existed when it was
  written; a page with teaching data falls under its rule.

## Open Items

`trebovaniya.md` §7 has no open item this Story depends on; OD-001 and OD-002 resolved a) by the
Owner on 2026-10-27 and applied (FR-003.1, FR-008.2). No `TODO`/`TBD`/`FIXME` marker.

## Interpretations

All listed in spec §11 and digest §2:

| Id | Choice |
|---|---|
| I-1 | X1 = `Status = Failed`; hidden while the next run is in progress |
| I-2 | Dean: titles without details; X6 keeps its two dates |
| I-3 | Read-only message on «Стан даних» only, no new site-wide read-only banner |
| I-4 | Banner failure leaves the page without the banner |
| I-5 | Confirmation all-or-nothing |
| I-6 | At most 1000 ids |
| I-7 | Page-search hint on «Стан даних» |
| I-8 | H4 for the Dean only |
| I-9 | `upgrade_required` is a cause of X6, no own problem before 24 h |
| I-10 | Drafts, items "not in Classroom", leavers count for T1 |
| I-11 | Flag kept beside the date |

Not listed (see m-2, m-3): the banner always shows both numbers, zero included (FR-003.2); a
never-loaded skipped course without a name is shown by its Google course id (FR-001.6).

## Digest Fidelity

Every digest statement traces to the Specification; all eleven interpretations and both Open
Decisions are present and none is softened. Plain Russian. The two unlisted choices of m-2/m-3 are
small and visible in the spec; they are advisory.

## Findings

- **m-1 (Minor) — FR-004.4 vs FR-002.3.** FR-004.4 puts X3 of archived courses into the archived
  section "in the same layout as FR-004.3", which groups by course name; FR-002.3 says the Dean sees
  no course name for X3. The Specification does not say which wins for an archived X3 course on the
  Dean's page. Checked against §2 note on row 749 (Dean sees technical problems without details).
  Suggested: state that for the Dean an archived X3 appears as a title with H3 and no course name,
  or move archived X3 to the technical section uncounted.
- **m-2 (Minor) — FR-003.2, digest §1.** "Both numbers always shown, zero included" («0 технічних
  проблем, 3 навчальні») is a visible choice; `trebovaniya.md` line 1162–1163 gives the template
  «N технических проблем, M учебных» and allows it, but the digest's example has two non-zero numbers
  and does not mention the zero case. Advisory: tell the Owner at the gate.
- **m-3 (Minor) — FR-007 / FR-001.6.** The texts table lacks the wording of several variants the
  Specification requires: T3 without a date (loaded course, last read unknown), T3 of a never-loaded
  course ("not loaded yet, skipped since"), the empty-section line, the confirmation success line,
  and the refusal «Список змінився — оновіть сторінку» in English. FR-001.6 also shows a nameless
  never-loaded course by its Google course id, a fallback no source states. Advisory: TEST_WRITING
  will pin texts; fixing the wording in the spec first avoids pinning invented literals.

## Verdict

PASS — no Critical or Major finding. Minor findings m-1…m-3 go to the human gate.
