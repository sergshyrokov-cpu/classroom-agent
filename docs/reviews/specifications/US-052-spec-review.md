---
artifact_type: specification_review
story: US-052
version: 1
status: APPROVED
created_at: 2026-10-16T13:49:25Z
updated_at: 2026-10-16T13:49:25Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-052-forbidden-role-tests.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/specifications/US-052-spec.md
    version: 1
  - path: docs/specifications/US-052-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-052-open-decisions.md
    version: 1
supersedes: null
verdict: PASS
---

# US-052 Specification Review

## Summary

**PASS** — 0 Critical, 0 Major, 3 Minor. The Specification is a faithful,
test-only rendering of the Story: every AC is carried, every rule is cited and
the citations hold against `trebovaniya.md` §2 (v96), SC-4, SC-11 and TC-5, and
against the code facts it quotes. The digest names every interpretation. The
Minor findings are precision points for TEST_WRITING.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-052-forbidden-role-tests.md` | (unversioned) |
| Requirements | `trebovaniya.md` | 96 |
| Specification | `docs/specifications/US-052-spec.md` | 1 |
| Digest | `docs/specifications/US-052-spec-digest.ru.md` | 1 (input: spec v1 — current) |
| Open Decisions | `docs/decisions/US-052-open-decisions.md` | 1 |

Inputs are current; none superseded.

## Fidelity

Checked against the cited text, not the citation:

- **§2 «Матрица прав» (v96, lines 636–656).** Every row of the FR-010 table
  matches the matrix cell for cell: the eight ✔/✔ rows, the five Admin-only
  rows (connection, instruction, check access, Dean accounts, erasure), «Смена
  собственного пароля» — / ✔, legitimacy status ✔/✔. The v80 row
  (sync-state diagnostics on the connection page) is correctly folded under
  `ConfigureWorkspaceConnection` — it has no endpoint of its own.
- **Registered policies** (`InstallationSecurityServices.cs:48–167`): the role
  sets of all 15 role policies equal FR-010's table; `ManageDeanAccounts` and
  `EraseParticipantData` Admin only, `ChangeOwnPassword` Dean only;
  `AuthenticatedUser` is the fallback (line 167, as FR-002 / §2 says).
- **Controller attributes:** `DeanAccountsController.cs:23` class-level
  `ManageDeanAccounts`; `DeanPasswordController.cs:65,70` `ChangeOwnPassword`;
  `Forced` GET `AuthenticatedUser` (:33), `CompleteForced` POST
  `CompleteTemporaryPasswordChange` (:40); `SignOutController.cs:16` and
  `UiLanguageController.cs:26` `AuthenticatedUser` — FR-011 is exact.
- **SC-4 anonymous list** (security-conventions lines 193–205): FR-012's
  handling of the fallback catch-all (part of "Error page"), `signin-google`
  (no routed endpoint) and static files (middleware, not endpoint) agrees.
- **SC-11:** creating, disabling, re-enabling, resetting a Dean account and a
  Dean changing their own password are all audited — FR-008's "exactly one
  audit row" per allowed request is grounded.
- **TC-5:** allowed- and forbidden-role test per protected endpoint; forbidden
  page role gets `403` with the translated page — FR-003/FR-006 agree; SC-4 v66
  `AccessDenied` → `403` agrees.
- **Audit 2** F-1, I-1, I-2 (`docs/reviews/audits/2026-10-pre-pilot-cross-cutting-audit.md`
  lines 83, 85, 170–192, 221–227) are quoted correctly; journal export is under
  `UseReportTemplates` (`JournalExportController.cs:28`).

No invented rule, no contradiction.

## Story Coverage

AC-001…AC-006 are all present (§5) and traced (§13). FR-007 (normal mode, to
keep a `409` from standing in for `403`) and VR-001…VR-004 (valid inputs, so a
validation refusal cannot stand in) are supporting rules inside the Story's
intent, not scope growth. Out of scope matches the Story.

## Consistency

- Permission matrix: no invented cell; the table restates §2.
- Read-only mode (BR-026): not touched; FR-007 keeps it out of the way.
- Localization (NFR-073): only "translated error page" assertions; consistent.
- Audit (SC-11): no new category; "authorization refusal writes no row" agrees
  with SC-11 (no such category is listed).
- Earlier Stories: US-012 (Dean accounts, own password, forced change),
  US-038 (erasure), US-051 (no new endpoint) — consistent.

## Open Items

`trebovaniya.md` §7 (v96) has no item this Story depends on. No Open Decision
is silently resolved. FR-015 correctly routes a §2-vs-code contradiction to a
new Open Decision and BLOCKED rather than bending the test or fixing production.

## Interpretations

The Specification names I-1…I-7. Found on review:

1. Two tables (endpoint → policy, policy → §2 row → roles) — named I-1.
2. Sign-out, UI language, forced change as "any signed-in user" — named I-2.
3. Journal export pinned under `UseReportTemplates` — named I-3.
4. "Dean" = full session after the forced change — named I-4.
5. "Succeed" = `302` + DB effect + one audit row — named I-5.
6. Red phase via a temporarily removed attribute — named I-6.
7. Expected N/A design stages — named I-7 (procedural, not shown in the digest; acceptable).
8. AC-002's "a valid state change" is tested in the disable direction only
   (FR-002) — not named; see m-2. Same endpoint and attribute, so it does not
   change what is proven; advisory.

No unnamed interpretation that changes behaviour or what the Owner approves.

## Digest Fidelity

Every digest statement traces to the Specification (§1 → FR-001…FR-014; §2 →
I-1…I-6, FR-015; §3 → §11; §4 → §10). No interpretation is softened or presented
as settled; FR-015 is stated plainly. The language is plain Russian. I-7 is
omitted — it concerns workflow stages, not product behaviour.

## Findings

- **m-1 (Minor) — FR-010's "every registered policy is in this table" rule
  contradicts FR-009/FR-011 as written.** `AuthenticatedUser` and
  `CompleteTemporaryPasswordChange` are registered (`InstallationSecurityServices.cs:53,163`)
  but are not §2 rows and are absent from the FR-010 table; read literally, the
  test fails against today's code (and could even trigger FR-015's BLOCKED path
  spuriously). Intent is clear from FR-009's "any signed-in user" category.
  TEST_WRITING should treat the two as the enumerated "any signed-in user"
  policies — the registered-policy check covers role policies plus exactly
  these two, and an unknown third policy still fails.
- **m-2 (Minor) — re-enable direction not exercised.** AC-002 is titled "disable,
  re-enable or reset"; FR-002 sends only a disable. It is one endpoint under one
  class attribute, so the role boundary is proven; the test may add the
  re-enable case cheaply if the fixture already holds a disabled Dean.
- **m-3 (Minor) — the endpoint table does not record each endpoint's §2 row.**
  FR-009 rows carry the policy only; the link to §2 goes through FR-010. So the
  matrix test catches a policy *change* but cannot itself show that the policy
  first chosen for an endpoint is the one its §2 row names (the journal export,
  I-3, is exactly such a case, disclosed). Suggest each endpoint row carry its §2
  row name as a comment or field, so a reader of the table can check it against
  §2 without leaving the file. Advisory; behaviour unchanged.

## Verdict

**PASS.** No Critical or Major finding. m-1…m-3 are non-blocking and go to the
Owner at `HUMAN_SPEC_APPROVAL`; m-1 should be honoured at TEST_WRITING.

Delegation: none — all sources and code were read directly by the reviewer.
