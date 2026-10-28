---
artifact_type: specification_review
story: US-065
version: 1
status: APPROVED
created_at: 2026-10-28T11:11:04Z
updated_at: 2026-10-28T11:11:04Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-065-erasure-exclusion-by-email-in-advance.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/specifications/US-065-spec.md
    version: 2
  - path: docs/specifications/US-065-spec-digest.ru.md
    version: 2
  - path: docs/decisions/US-065-open-decisions.md
    version: 2
supersedes: null
---

# US-065 Specification Review

## Summary

**Verdict: PASS** — 0 Critical, 0 Major, 3 Minor.

The Specification follows `trebovaniya.md` v104 (§2 matrix row, §3
`ErasedParticipant` v104 paragraph, §4 Epic 10 «Исключение по email заранее»,
§5 audit, §9 «Перевод школы на другой домен» step 4) and the Story. Every
Story AC is specified; the ten derived ACs stay inside the Story's scope. The
one gap the requirements leave (OD-003) is raised, resolved by the Owner and
reflected consistently in FR-008, AC-017, the OD file and the digest. The
Minor findings concern how two consequences are explained, not what is built.

Delegation: none. All cited texts were read directly.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-065-erasure-exclusion-by-email-in-advance.md` | — |
| Specification | `docs/specifications/US-065-spec.md` | 2 |
| Digest | `docs/specifications/US-065-spec-digest.ru.md` | 2 (input: spec v2 — current) |
| Open Decisions | `docs/decisions/US-065-open-decisions.md` | 2 |
| Requirements | `trebovaniya.md` | 104 |

Inputs are current: the spec names the Story, v104 and OD v2; the digest names
spec v2.

## Fidelity

Checked against the cited text:

- **§3 v104** (lines 947–958): exclusion without Google id at first; sync skips
  a participant whose email in Google's answer gives the fingerprint and writes
  the id; afterwards works by id. → FR-003, FR-006, FR-007. Matches.
- **§4 Epic 10 v104** (lines 1585–1601): screen available before the Google
  connection (FR-001, AC-002); "already loaded → offer ordinary erasure"
  (FR-004); only the fingerprint stored (FR-003); lifted like any other
  (FR-010); known limitation for missing email (VR-004, Out of Scope); audit by
  exclusion id (FR-013); refused in read-only mode (FR-012). Matches.
- **§2 matrix** (line 765): Admin ✔, Dean ✘ → FR-014. No invented cell.
- **§9 domain move step 4** (lines 2812–2830) → §3.2 business flow. Matches.
- **US-038** citations checked: FR-010 salt, FR-011 enforcement/fence
  (db-design §7.3–7.4), FR-012 skip list, FR-013 audit, FR-014 read-only,
  FR-015 authorization, VR-003/VR-004, S-09, I-7, I-8, I-10–I-14, OD-001. All
  exist and say what the spec attributes to them.
- **FR-012 third bullet** (new installation read-only until the first
  successful legitimacy check): consistent with BR-025 and §9 — the legitimacy
  check talks to the Control Plane only and does not need a
  `WorkspaceConnection` (lines 2555–2563, 2735–2741), so §4's "before the
  connection is configured" and "refused in read-only mode" hold together.
- **FR-008 / OD-003 a)**: the §3 v104 sentence covers an exclusion "внесённое
  Админом по email до того, как человек загружен". The OD-003 case is a person
  already loaded under another address — outside that precondition, so the
  Owner's resolution fills a gap rather than contradicting §3. See m-1.

No rule without a source was found. No contradiction.

## Story Coverage

AC-001…AC-011 → spec §5 table and §12 traceability, each with FRs. Story scope
bullets all present (block, pre-connection use, already loaded, already
excluded, sync fingerprint match + fill, Meet unchanged with a test, lifting,
audit, read-only, bilingual texts, nullable id + immutability change). Out of
scope matches the Story, plus I-4 (owner not on a roster), which is a direct
consequence of the §4 known limitation. Derived AC-012…AC-021 each trace to an
FR and add no new behaviour beyond the Story Notes' questions.

## Consistency

- Permission matrix §2: unchanged policy `EraseParticipantData`, Admin only.
- Read-only BR-026 / AD-6: guard in `Application`; the fill is a sync write and
  never runs in read-only mode (FR-007) — correct, it is not a BR-026 service
  write and needs none.
- NFR-073: FR-015, AC-020.
- SC-11: entering audited by exclusion id; refusals as for lifting (US-038
  FR-013/I-12); no email, fingerprint or Google id in audit or log.
- US-038 invariant "no stored participant with an excluded Google id" kept by
  FR-008 and FR-007's "already held by another exclusion" rule.
- VR-001 reuses US-038 VR-003 (any domain, OD-001 a); one address per
  submission (OD-002 a).

## Open Items

`trebovaniya.md` §7 (v104) items 14, 26, 27, 28, 29 do not touch this Story —
confirmed. OD-001, OD-002, OD-003 resolved by the Owner (2026-10-28). No open
Open Decision; no `TODO`/`TBD`/`FIXME`/`???` in the spec or OD file.

## Interpretations

| # | Interpretation | In digest §2? |
|---|---|---|
| I-1 | Fingerprint matching only for exclusions without an id | yes |
| I-2a | Id already held by another exclusion → skip, leave unfilled | yes |
| I-2b | Two Google ids with one fingerprint in a course → both skipped, ordinally first fills | **no** (m-3) |
| I-3 | Entry date stored in the erasure-date field | yes |
| I-4 | Owner not on the roster cannot be matched | yes |
| I-5 | "Already loaded" checked before "already excluded" | yes |
| I-6 | Entry takes the erasure fence | yes |
| FR-008 | OD-003 a): stored Google id is not matched | yes (digest §3) |
| FR-012 | No special case for a new installation still in read-only mode | **no** (m-2) |
| FR-006 | Teachers as well as students are matched | yes |

## Digest Fidelity

Every digest statement traces to the Specification. OD-003 is shown as
resolved with both options and the trade-off; OD-001/OD-002 mentioned. The
language is plain Russian. Two consequences are missing (m-2, m-3); neither is
presented wrongly — they are simply not told.

## Findings

### m-1 (Minor) — FR-008's known limitation is a new behaviour the requirements do not record

- **Where:** spec FR-008 last bullet, §11 OD-003; digest §3.
- **Checked against:** `trebovaniya.md` §3 v104, §4 Epic 10 (known limitations).
- **What:** under OD-003 a), a person loaded under an old address and excluded
  under the new one keeps loading in Classroom while their Meet connections are
  already skipped by fingerprint, until the Admin re-enters the address. The
  spec and digest name it, and the case is outside §3's precondition ("до того,
  как человек загружен"), so it is not a contradiction. But `trebovaniya.md`
  lists Epic 10's known limitations and this one is not among them.
  Advisory: record it in the next `trebovaniya.md` version (or a follow-up note)
  so the documented limitations stay complete.

### m-2 (Minor) — digest does not tell that a new installation can enter exclusions only after its first legitimacy check

- **Where:** spec FR-012 third bullet; digest §1 (Admin, read-only line).
- **Checked against:** BR-025; `trebovaniya.md` §9 step 4.
- **What:** in the domain-move scenario the new installation is read-only until
  the first successful check with the Control Plane; until then the block is
  inactive. The spec adds no special case (consistent with the requirements),
  but the Owner reading the digest learns only "works before the Google
  connection is configured". Advisory: one digest line — «на только что
  развёрнутой инсталляции — после первой успешной проверки у Control Plane».

### m-3 (Minor) — digest omits the "two Google accounts, one email" edge case (I-2b)

- **Where:** spec FR-007 fourth bullet, I-2; digest §2.
- **Checked against:** checklist item 5 (every interpretation in digest §2).
- **What:** when two roster entries with different Google ids give the same
  fingerprint in one course, both are skipped and the exclusion gets the
  ordinally first id. Edge case within one domain, but a choice the Owner does
  not see. Advisory: one digest line.

## Verdict

**PASS.** No Critical or Major findings; m-1…m-3 are advisory and go to the
Owner at `HUMAN_SPEC_APPROVAL`.

```yaml
result:
  verdict: PASS
  stage: SPEC_REVIEW
  story: US-065
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/specifications/US-065-spec-review.md
  next_stage: HUMAN_SPEC_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "m-1: FR-008 known limitation (Classroom loads, Meet skipped until re-entry) not recorded among trebovaniya.md Epic 10 known limitations"
    - "m-2: digest omits that a new installation can enter exclusions only after its first successful legitimacy check (FR-012)"
    - "m-3: digest omits I-2b, two Google ids with one fingerprint in a course"
  closed_findings: []
```
