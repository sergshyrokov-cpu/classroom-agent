---
artifact_type: specification_review
story: US-054
version: 1
status: APPROVED
created_at: 2026-10-24T20:10:00Z
updated_at: 2026-10-24T20:10:00Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-054-technical-account-superadmin-texts.md
    version: null
  - path: trebovaniya.md
    version: 97
  - path: docs/specifications/US-054-spec.md
    version: 1
  - path: docs/specifications/US-054-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-054-open-decisions.md
    version: 1
supersedes: null
verdict: PASS
---

# US-054 Specification Review

## Summary

**PASS** — 0 Critical, 0 Major, 2 Minor. The Specification is faithful to
`trebovaniya.md` v97 §1 ("Админ ≠ super-admin"), §9 "Технический аккаунт" (the
super-admin role, its reason and the five mandatory measures), §4 Epic 6
("Инструкция по подключению", "Состояние синхронизации") and §7 item 29. It
covers all eight Acceptance Criteria, stays within the Story's scope, carries
OD-001…OD-004 with the Owner's resolutions, and the digest tells the Owner the
truth. Both Minor findings concern stale comments and a shared test list that
FR-006 / I-2 do not enumerate completely; neither changes behaviour.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-054-technical-account-superadmin-texts.md` | (unversioned) |
| Requirements | `trebovaniya.md` | v97 |
| Specification | `docs/specifications/US-054-spec.md` | 1 (DRAFT) |
| Spec digest | `docs/specifications/US-054-spec-digest.ru.md` | 1 (DRAFT) |
| Open Decisions | `docs/decisions/US-054-open-decisions.md` | 1 (DRAFT) |

Inputs are current: the Specification names `trebovaniya.md` v97 and Open
Decisions v1; the digest names Specification v1.

## Fidelity

Checked against the cited text, not the citation:

- **FR-001 #3 `SuperAdminRole`** — §9 lines 2439–2445 and §1 lines 540–542:
  super-admin role; delegated Classroom administrator roles give no view of the
  domain's courses; access is bounded by the DWD scopes, not by roles. Matches.
- **FR-001 4.1–4.4** — §9 lines 2446–2451, item by item: random password stored
  nowhere; enforced 2SV with no enrolment grace period, no factor, no backup
  codes, recovery email or phone, "так что войти невозможно"; a separate OU with
  these settings; an alert rule on any sign-in, mailed to a domain
  administrator. Matches in both languages of §4.1.
- **FR-001 4.5 `RoleGrantedLast`** — §9 says "непосредственно перед первой
  синхронизацией". The Specification places the grant immediately before the
  access check; this is the Owner's resolution OD-002 (a), cited, and the access
  check precedes the first synchronization, so the literal rule still holds.
- **FR-001 #2, #5, AC-004** — §9 lines 2437–2438, 2452–2454 ("за аккаунтом не
  стоит человек", "в программу под ним никто не входит", email entered by the
  Admin, domain match). Matches.
- **FR-003 hint** — §9 + §2 role table line 570 / §2 line 593 (not the Admin's
  account). Matches.
- **FR-004 diagnosis** — §4 Epic 6 "Проверка подключения" (intelligible message
  on a rights error) and "Состояние синхронизации" (v80, one problem one name);
  `Views/WorkspaceConnection/Index.cshtml` line 121 maps every other
  `SyncDiagnosis` to `AccessCheck.Outcome.<name>`, so the single entry serves
  both, as FR-004 claims. §9 line 2459 ("если у него отняли роли, синхронизация
  получает ошибку прав") supports advising the role.
- **FR-002** — the removed keys are the two that contradict §9 v97 (verified
  current values: "і не мати нічого понад це", "акаунт super-admin … не
  підходять"). The removed "the program writes nothing to Google Workspace"
  survives in substance in `SuperAdminRole` ("програма однаково лише читає").
- **F-1** — verified: `trebovaniya.md` line 837 (§3 `WorkspaceConnection`)
  still says "не аккаунт Админа и не super-admin", contradicting §1/§9 v97. The
  Specification correctly follows §9 and routes the fix to a new requirements
  version. (Line 430 is the v30 changelog entry — history, not a rule.)

No rule without a source; no rule contradicting its source.

## Story Coverage

AC-001…AC-008 are each represented (spec §5, §13), each mapped to an FR. The
Story's scope items — instruction statements, removed keys, hint, diagnosis,
copied instruction, rewritten tests — are all present. Nothing goes beyond the
Story: the `Index.cshtml` nesting change (I-3) and the comment fixes (I-4) are
supporting changes the Story's own Notes anticipated.

## Consistency

- US-010: one key per statement (I-8) kept; the copy affordance copies the
  `<pre>` `textContent` (`copy-instruction.js` verified), so FR-005 needs no
  script change; HTML encoding (VR-004) and no-JS readability (I-5) unchanged.
- US-009 hint, US-011/US-017 outcome/diagnosis: keys kept, values changed only.
- Permission matrix (§2): no cell touched; policies unchanged (spec §7).
- BR-026: viewable in read-only mode, unchanged. NFR-073: both languages, VR-001.
- BR-015 (`docs/product/business-rules.md`, aligned in `1469569`) already states
  the v97 meaning; the Specification agrees with it.
- SC-11/SC-13: no audit or outbound change.

## Open Items

- §7 item 29 (DWD with blocked sign-in) — the Story depends on it; it is
  OD-001, resolved (a) by the Owner, with the follow-up path recorded and a
  manual check named (spec §12). Not silently resolved.
- §7 item 10 — closed by v97; no longer an open item.
- No other §7 item is touched.

## Interpretations

Places where the Specification chose one reading:

1. **I-1** — `NoPersonBehindIt`, `ImpersonationUser.Hint`,
   `TechnicalAccountCannotRead` reworded under their kept keys; only keys whose
   name no longer fits are removed. → digest §2, bullet 1.
2. **I-3** — measures as a nested list under a lead statement (`MeasuresLead`)
   rather than five top-level statements. → digest §2, bullet 2.
3. **I-2** — role-name guard kept, "Super Admin" dropped from it. → digest §2,
   bullet 3.
4. **I-4** — two stale comments outside the instruction brought into scope as
   supporting changes. → digest §2, bullet 4.
5. **I-5** — AC-003 tested by old-value absence as well as key absence. →
   digest §2, bullet 5.
6. **OD-002 (a)** — role granted before the access check, not literally before
   the first synchronization. Owner-resolved; stated in digest §1 (fifth
   measure) and §3.
7. **Binding wording §4.1** — the exact uk/en values are the writer's; the digest
   (§1 end) says they can be corrected at approval.

All are named in the digest.

## Digest Fidelity

Every digest statement traces to the Specification (FR-001…FR-005, I-1…I-5,
F-1, §10, §11). No interpretation or Open Decision is hidden or softened; §7
item 29 is shown as still open, with why it does not block. The language is
plain Russian a non-developer can judge. No finding.

## Findings

### m-1 (Minor) — FR-006 does not list every stale comment

Location: spec FR-006, I-4. Checked against: ripgrep of `src/` and `tests/`.
FR-006 enumerates the comments to rewrite, but two more state the pre-v97
meaning and are not listed:

- `tests/ClassroomAgent.Tests/TestInfrastructure/WorkspaceConnectionTestData.cs:19`
  — "no person behind it, read-only roles only (BR-015)";
- `tests/ClassroomAgent.Tests/Web/Localization/AccessCheckTranslationTests.cs:53`
  — "§7 item 10 is open".

Two further comments cite §7 item 10 as unverified in contexts outside this
Story (`RunSynchronizationUseCase.cs:792`, `CourseImportTests.cs:145`); they are
informational, not contradictions of a statement this Story changes. Because
the Git Policy scopes changes to what the Specification traces, an implementer
following FR-006 literally would leave the first two contradicting v97.
Advisory: treat them under FR-006's "comments … that pin the old meaning" at
TEST_WRITING/IMPLEMENTATION, or the Owner may ask for a spec correction.

### m-2 (Minor) — the guard list is shared with an access-check test I-2 does not name

Location: spec I-2, VR-002. Checked against:
`AccessCheckTranslationTests.NoMessage_NamesAWorkspaceAdminRole` (lines 56–69),
which iterates `ConnectionInstructionTestData.ForbiddenRoleNames`. Dropping
"Super Admin" from that list therefore also changes what the access-check test
guards. The effect is consistent with v97 (the new `TechnicalAccountCannotRead`
value names the super-admin role), and the comparison is a case-insensitive
substring, so "super-admin" never matched "Super Admin" anyway; but I-2 presents
the change as instruction-only. Advisory: the test writer should be aware the
list is shared.

## Verdict

**PASS** — no Critical or Major finding. m-1 and m-2 go to the Owner as
non-blocking findings at `HUMAN_SPEC_APPROVAL`.

Delegation: nothing was delegated; every cited passage of `trebovaniya.md`, the
resource files, views, script and tests was read directly in this stage.
