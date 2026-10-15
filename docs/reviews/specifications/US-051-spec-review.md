---
artifact_type: specification_review
story: US-051
version: 1
status: APPROVED
created_at: 2026-10-15T20:10:00Z
updated_at: 2026-10-15T20:10:00Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-051-google-sign-in-admin-accounts-only.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/specifications/US-051-spec.md
    version: 1
  - path: docs/specifications/US-051-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-051-open-decisions.md
    version: 1
supersedes: null
---

# US-051 Specification Review

## Summary

The Specification is faithful to `trebovaniya.md` v96 (§2 "Одна учётная запись —
одна роль", §5 refusal category, §9), SC-3, SC-11 and BR-011. Every Story AC is
covered; the derived AC-007…AC-009 are justified. No Critical or Major finding.
Four Minor findings. Verdict: **PASS**.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| story | docs/stories/US-051-google-sign-in-admin-accounts-only.md | — |
| requirements | trebovaniya.md | 96 |
| specification | docs/specifications/US-051-spec.md | 1 |
| spec_digest | docs/specifications/US-051-spec-digest.ru.md | 1 |
| open_decisions | docs/decisions/US-051-open-decisions.md | 1 |

Inputs are current: the Specification names trebovaniya v96 (the current header
version); the digest names Specification v1.

## Fidelity

Checked against the cited text, not the citations:

- FR-001/FR-002 — §2 v96: «Вход через Google пускает только учётную запись с ролью
  Админ … отклоняется, даже когда этот email есть в `AllowedAdmin` … в Админа не
  превращается и не меняется»; SC-3 "One account, one role"; BR-011. Match.
- FR-003 — §5 v96: actor is the account's id when it exists; category «учётная
  запись не Админа»; no personal data. SC-11 repeats it. BR-026 audit write. Match.
- FR-004 — §2 v96 order «сначала `AllowedAdmin` … затем роль, затем отключение».
  Match, including the disabled Dean case.
- FR-006 — §2 v96 message content («адрес — учётная запись Декана: входить по
  логину и паролю, а для роли Админа нужен другой адрес»); NFR-073. Match.
- FR-007 — US-008 FR-020 (decisions at `Warning`), SC-10; consistent with
  `SignInLog.Refuse` today (event ids 2200–2204).
- FR-008 — PC-2; `ck_audit_event_refusal_category_value` in
  `AuditEventConfiguration.cs` is a closed list, so a migration is required.
- FR-005 — US-008 spec I-10 (the `UniqueEmailViolationException` re-read in
  `CompleteGoogleSignInUseCase.AdmitAsync`); extension is I-3, see below.
- Out of scope "reverse case" — §2 v96 «Обратный случай уже закрыт». Match.

No invented rule found.

## Story Coverage

Story AC-001…AC-006 map one-to-one to spec AC-001…AC-006; traceability table
complete. Derived AC-007 (race), AC-008 (constraint), AC-009 (log) stay inside
the Story's scope items. Out-of-scope list matches the Story's and adds two
items that follow from SC-11 and §2.

## Consistency

- Permission matrix (§2): no cell changes; only the Google sign-in channel.
- BR-026: the refusal uses the existing `PermittedServiceWrite.AuditEvent`; list
  not widened.
- NFR-073: new key in uk and en.
- SC-11: one row per outcome, rows never updated.
- US-008 / US-012 / US-048 behaviour declared unchanged (FR-009).

## Open Items

§7 v96 holds no item on Google sign-in, roles or audit categories. The Story has
no Open Decisions; none was raised. Agreed.

## Interpretations

Named in the Specification and in digest §2:

1. I-1 `Role != Admin` rather than `Role == Dean` — digest §2 ✓.
2. I-2 disabled Dean → "not an Admin account" — digest §2 ✓ (this is in fact
   prescribed by the Story AC-003 and §2, not a free choice).
3. I-3 race path gets the role check — digest §2 ✓ (see M-2 for the part not told).
4. I-4 message text and no address shown — digest §2 ✓.
5. I-5 actor role Dean, action Admin sign-in — digest §2 ✓.
6. I-6 not a failed Dean attempt — digest §2 ✓.

Found by the reviewer and not named:

7. FR-005 also adds the **disabled** check to the race path (today a disabled
   account found on the re-read would be stamped and admitted). → M-2.
8. FR-007 fixes the log level at `Warning` (not `Error`). Technical, follows
   FR-020; does not need the Owner. No finding.

## Digest Fidelity

Every digest statement traces to the Specification. No Open Decision exists to
hide. The language is plain Russian. The migration and the log line are omitted
from the digest — technical, acceptable. See M-2 for one partial omission.

## Findings

| Id | Severity | Location | Checked against | Finding |
|---|---|---|---|---|
| M-1 | Minor | spec §1 "two translation entries per language" | spec FR-006, NFR-073 | FR-006 defines one new key (`SignIn.Refused.NotAdminAccount`) per language; §1 says two. Fix the count or name the second entry. |
| M-2 | Minor | spec FR-005 / digest §2 "Закрыта редкая гонка" | §2 v96 order of checks | FR-005 also applies the disabled check on the race path — a behaviour change for any disabled account, not only a Dean. Correct per §2 order and practically unreachable, but the digest mentions only the Dean case. |
| M-3 | Minor | spec FR-005, §8 | `CompleteGoogleSignInUseCase.AdmitAsync` | The refusal on the race path happens inside the open transaction after the rejected insert; the spec should say that the refusal's audit row is committed (not rolled back with the transaction) — AC-007 requires exactly one row. Check at IMPLEMENTATION_VERIFICATION. |
| M-4 | Minor | spec FR-007 | `SignInLog.cs` | "its own event id" is not given; the next free id is 2205. Leave to implementation, but tests (AC-009) need the id fixed — name it in the implementation report. |

## Verdict

**PASS** — no Critical or Major finding; M-1…M-4 are advisory.

## Delegation

Nothing was delegated. Direct reads: the Story, Specification, digest and Open
Decisions; `trebovaniya.md` header and lines 585–606, 1462–1480, 2408–2418;
`security-conventions.md` SC-3 and SC-11 (lines 145–185, 436–446);
`business-rules.md` BR-011; `CompleteGoogleSignInUseCase.cs`,
`AuditRefusalCategory.cs`, `SignInRefusal.cs`, `SignInLog.cs`,
`AuditEventConfiguration.cs`, `SharedResource.{uk,en}.resx`; US-008 spec FR-020
and I-10 presence.
