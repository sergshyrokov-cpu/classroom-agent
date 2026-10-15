---
artifact_type: specification_review
story: US-048
version: 1
status: APPROVED
created_at: 2026-10-15T18:45:00Z
updated_at: 2026-10-15T18:45:00Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-048-dean-lockout-counter-reset.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/specifications/US-048-spec.md
    version: 1
  - path: docs/specifications/US-048-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-048-open-decisions.md
    version: 1
supersedes: null
verdict: PASS
---

# US-048 Specification Review

## 1. Summary

**Verdict: PASS** — no Critical or Major findings; four Minor findings.

The Specification brings the Dean's sign-in to the policy `trebovaniya.md` §9
(v62) already states, and carries the Owner's resolution of OD-001 (a) into a
bounded re-run on a concurrency conflict. Every rule cites a source that says
what the Specification claims; every interpretation (I-1…I-5) is named in the
digest; the digest misstates nothing.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-048-dean-lockout-counter-reset.md` | — (authored, header aligned with v93) |
| Requirements | `trebovaniya.md` | 96 |
| Specification | `docs/specifications/US-048-spec.md` | 1 |
| Digest | `docs/specifications/US-048-spec-digest.ru.md` | 1 (input: spec v1 — current) |
| Open Decisions | `docs/decisions/US-048-open-decisions.md` | 1 |

Inputs are current: the Specification names trebovaniya v96 and the Story; the
digest names spec v1.

## 3. Fidelity

Checked against the cited text, not the citation:

- **FR-001** — §9 (lines ~2062–2064, v62): «После 5 неудачных попыток подряд вход
  блокируется на 15 минут, успешный вход сбрасывает счётчик; бессрочной
  блокировки нет»; changelog v62 says the same holds for the Dean. SC-2 states
  "The Dean's password and lockout policy is the Owner's". The code today
  (`AppUser.RecordFailedSignIn`, lines 181–191) sets `LockoutEnd` at ≥5 and
  leaves the count; confirmed defect. The Owner precedent
  (`OwnerSignInService`, "count of consecutive failures starts again (spec I-4)")
  and US-001 I-4 ("After a lockout ends, consecutive failures are counted from
  zero") match. Policy numbers stay in `SignInDeanUseCase` — consistent with
  US-012 FR-013.
- **FR-002** — SC-2 v65/v66 six-step sequence; unchanged. Verified that existing
  tests (`DeanSignInSequenceTests.Step2_DoesNotMoveTheCounter` compares before/
  after, `DeanLockoutTests.FourFailures_DoNotLock` asserts 4) do not depend on
  the count staying at 5 — AC-004 is achievable without test edits.
- **FR-003/FR-004** — source is OD-001 (a) and the Owner precedent
  (`MaxConcurrencyRetries = 3`, catch `when (attempt < MaxConcurrencyRetries)`
  → third conflict propagates). Matches I-4 exactly.
- **FR-005** — AD-3/AD-4 layering; the US-032 precedent exists
  (`UnitOfWork` catches `DbUpdateConcurrencyException` for `MeetingCodeLink`,
  detaches and throws an Application exception). `AppUser.ConcurrencyStamp` is
  an `IsConcurrencyToken()` and every `AppUser` mutator (Disable via
  `RotateSecurityStamp`, ReEnable, ResetPassword, RecordFailed/SuccessfulSignIn,
  ChooseUiLanguage) rotates it — so AC-007's conflict with a disable is real.
- **FR-006** — derived from SC-2 / §9 «по нему не понять, существует ли логин»,
  plus the existing timing equalisation on step 1. Well defined in practice:
  only steps 3 and 6 modify the account, both after a verification, so a
  conflicted run always has a prior verification to reuse. Declared as I-5.
- **FR-007, VR-001, VR-002, §7, §8** — consistent with US-012, BR-026 (sign-in
  bookkeeping), SC-10, SC-11.

No invented rule, no contradiction.

## 4. Story Coverage

AC-001…AC-005 carried verbatim with their ids; derived AC-006…AC-009 each trace
to an interpretation and to OD-001. Nothing goes beyond the Story's scope; §10
excludes the Owner sign-in, other `AppUser` writers and cross-account rate
limiting, matching the Story's out-of-scope list.

## 5. Consistency

- Permission matrix: untouched (no role or action added).
- Read-only mode (BR-026): re-runs remain `SignInBookkeeping`.
- Localization (NFR-073): no new user-visible text.
- Audit (SC-11): one row per request, categories unchanged; step 1 still
  records no login.
- US-012 / US-001 / US-032 specifications: consistent (see §3).

## 6. Open Items

`trebovaniya.md` §7 (v96) has no item on sign-in, the counter or the lockout.
OD-001 is resolved by the Owner (a); no new Open Decision needed.

## 7. Interpretations

All five are named in the Specification §12 and in digest section 2:

| Id | Choice | In digest |
|---|---|---|
| I-1 | count restarts at the start of the lockout | yes |
| I-2 | re-run on a conflict with any writer of the account | yes |
| I-3 | re-run that meets a fresh lockout is refused at step 2, not counted | yes |
| I-4 | at most three runs, then the error page | yes |
| I-5 | no second verification against an unchanged hash | yes |

No unnamed interpretation found beyond those noted as Minor below.

## 8. Digest Fidelity

Every statement traces to the Specification. OD-001 is presented as resolved,
which it is. Plain Russian, judgeable by a non-developer. The residual timing
note of spec §7 is not retold (M-4).

## 9. Findings

| Id | Severity | Location | Checked against | Finding |
|---|---|---|---|---|
| M-1 | Minor | Spec VR-002 | `AppUser.RecordFailedSignIn` today | VR-002 claims the count never exceeds four outside a lockout, but a row already left at ≥5 by the current defect (dev/demo databases; no production yet) is not normalised: its next wrong password locks once more and then zeroes. The Specification should say so (or name the residue as accepted); no data fix seems warranted before the pilot. |
| M-2 | Minor | Spec FR-006 / I-5 | `OwnerSignInService` | I-5 makes the Dean's re-run stricter than the Owner's, which verifies the password again on every run. The Specification cites the Owner as precedent for FR-003/FR-004 without saying that FR-006 departs from it. The Owner's sign-in (private network) keeps the 2–3-verification residue; worth a one-line note, not a change in this Story. |
| M-3 | Minor | Spec FR-005, §10 | `UnitOfWork.SaveChangesAsync` | Translating every `AppUser` concurrency conflict changes the exception type (and detaches entries) for all `AppUser` writers, not only sign-in. The observable outcome for Admin reset, disable, password change and language choice stays the error page, which is what §10 promises; the Specification could state this explicitly so verification checks it. |
| M-4 | Minor | Digest §1–§2 | Spec §7 "No login oracle" | The accepted residual timing difference (one extra database read under concurrent requests for the same account) is not mentioned in the digest. It is small and stated in the Specification. |

## 10. Verdict

**PASS.** Ready for `HUMAN_SPEC_APPROVAL`. Minor findings M-1…M-4 are advisory.

## 11. Delegation

Nothing was delegated; all sources were read directly.
