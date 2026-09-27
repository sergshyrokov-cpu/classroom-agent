---
artifact_type: security_review
story: US-012
version: 3
status: APPROVED
created_at: 2026-09-27T06:56:21Z
updated_at: 2026-09-27T07:25:38Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-012-manage-dean-accounts.md
    version: null
  - path: docs/specifications/US-012-spec.md
    version: 1
  - path: docs/evidence/US-012-implementation-report.md
    version: 3
  - path: docs/designs/api/US-012-api-design.md
    version: 1
  - path: docs/designs/api/US-012-openapi.yaml
    version: 1
  - path: docs/designs/database/US-012-db-design.md
    version: 1
  - path: docs/designs/database/US-012-entity-model.md
    version: 1
  - path: docs/tests/US-012-test-strategy.md
    version: 1
  - path: docs/tests/US-012-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-012-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 79
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 5
security_sensitive: true
runtime_checks: PARTIAL
---

# US-012 Security Review — Create and manage Dean accounts

**Version 3 — PASS.** Re-review of `implementation_report` v3, which fixed F-3.
The record of the two earlier passes is kept below: version 1 found F-1 and F-2,
version 2 closed F-2 and found F-3 in the fix of F-1, and this version closes
F-3. Nothing outstanding remains.

## 0. Version 3 — F-3 closed

`_timingEqualisationHash` is now `private static string?`, filled at most once
for the process by `TimingEqualisationHashOf`
(`SignInDeanUseCase.cs:42, 113-114`) — the declaration
`ControlPlane.Services.OwnerSignInService` uses for the same purpose. The
unknown-login path therefore pays exactly one verification, the same as a path
that finds an account, and the response no longer reveals whether the login
exists by its timing in either direction. **SC-2 is now satisfied on this
point.**

Re-checked for risk introduced by the static itself:

- the value is a hash of a freshly generated GUID — never a real password, never
  logged, never returned; sharing it across a process reveals nothing;
- a race between two threads makes each hash once and one value win: bounded,
  harmless, and documented in the code;
- **step 2 still does not verify at all** (`SignInDeanUseCase.cs`, the lockout
  branch), which SC-2 v66 requires, and `Step2_StillVerifiesNoPassword` pins it;
- `Step1_DoesNotRehashOnEveryAttempt` asserts two consecutive unknown logins add
  no hashing work, deterministically whichever test populated the static first.

Evidence accepted for this version: build 0 errors / 0 warnings, **2167 tests,
2167 passed, 0 failed, 0 skipped**, `dotnet format --verify-no-changes` clean.

**Verdict: PASS.** Recommended next stage: `HUMAN_PR_APPROVAL`.

---

## Earlier passes

**Version 2** — re-review of `implementation_report` v2, which fixed the two
findings of version 1. Version 1's sections 3 to 17 and 19 to 21 still describe
this Story accurately and are not repeated; this version records what changed,
what was re-verified, and one residual finding. Version 1 remains the record of
the first pass.

### v2 executive summary (historical)

Verdict at the time: CHANGES_REQUIRED, one Major finding (F-3), since closed.

- **F-2 is closed.** Both password paths now re-issue the session through one
  helper; two new tests follow the redirect and prove the session survives and
  the confirmation renders.
- **F-1 is addressed but not closed.** Step 1 now spends hashing work, and step
  2 still correctly skips verification — both pinned by tests. But the dummy
  hash is computed **per request** rather than once per process, so the
  unknown-login path now costs roughly twice a real check instead of nothing.
  The direction of the oracle is inverted and its magnitude reduced; SC-2's
  guarantee that the response "never reveals whether the login exists" is still
  not met. That residue is **F-3**.

The fix is one word, and the pattern to copy is again
`ControlPlane.Services.OwnerSignInService`, whose equivalent field is
`static readonly`.

## 2. What changed since version 1

Four files, exactly as the implementation report claims — verified against the
working tree, not taken on trust:

| File | Change | Verified |
|---|---|---|
| `Application/UseCases/SignInDeanUseCase.cs` | step 1 verifies a dummy hash and discards the result | yes — and step 2 still does **not** verify |
| `Web/Controllers/DeanPasswordController.cs` | `ReIssueSessionAsync` used by both password paths | yes — called at line 59 (forced) and line 92 (voluntary) |
| `tests/…/DeanSignInSequenceTests.cs` | `Step1_SpendsTheSameHashingWorkAsARealCheck`, `Step2_StillVerifiesNoPassword` | yes |
| `tests/…/DeanPasswordPageTests.cs` | `AfterAChange_TheSessionSurvivesAndTheConfirmationIsShown`, `AfterAChange_TheNewPasswordIsTheOneThatWorks` | yes |

Nothing else was touched. The five Informational observations of version 1
(I-1 … I-5) are unchanged and still require no action.

Evidence recorded by the implementation report and accepted here: build 0
errors / 0 warnings, **2166 tests, 2166 passed, 0 failed, 0 skipped**,
`dotnet format --verify-no-changes` clean.

## 3. Finding F-3 — Major — AUTHENTICATION — SC-2

**Where:** `src/ClassroomAgent.Application/UseCases/SignInDeanUseCase.cs`,
the `_timingEqualisationHash` field.

**Observed:** the field is a **private instance** `Lazy<string>`, and
`SignInDeanUseCase` is registered `AddScoped` (`InstallationServices.cs:104`).
A DI scope is a request, so a new instance — and a new `Lazy` — is created for
every sign-in attempt. On the unknown-login path the value is therefore
computed on first use *in that request*: the path pays one `Hash` **plus** one
`Verify`, while a request that finds an account pays one `Verify` alone.
Identity's PBKDF2 costs about the same in both directions, so an unknown login
now answers in roughly twice the time of a known one.

**Expected:** SC-2 — "every refused sign-in shows the same message … so the
response never reveals whether the login exists". A consistent 2× difference is
as usable an oracle as the original one; only its sign has changed.

**Risk:** unchanged from F-1 — an unauthenticated caller can still enumerate
which staff addresses have Dean accounts, without moving any failed-attempt
counter. Lower confidence per sample than before, but trivially recovered by
averaging a handful of requests.

**Precedent, again:** `OwnerSignInService` declares
`private static readonly Lazy<string> HashTimingHash` — computed once for the
process, so its unknown-login path pays exactly one verification, matching a
real check.

**Required correction:** make the field `static readonly` (the hash need not be
per instance — it is a hash of a value nobody knows, used only to spend work).
Keep the rest of the fix as it is.

**Loop-back:** `IMPLEMENTATION`.

**Verification after correction:** the existing
`Step1_SpendsTheSameHashingWorkAsARealCheck` already counts hasher calls
through the substituted port; extend or add an assertion that the **`Hash`**
call count on the unknown-login path is zero after the first attempt in the
same process, or assert that two consecutive unknown-login attempts produce one
`Hash` call in total.

## 4. Closed findings

**F-1 (v1, Major)** — superseded by F-3. Step 1 no longer returns without
spending work, and `Step1_SpendsTheSameHashingWorkAsARealCheck` pins it; the
part that remains unmet is the per-request recomputation, tracked as F-3.

**F-2 (v1, Minor) — CLOSED.** `DeanPasswordController.ReIssueSessionAsync`
issues a session carrying the account's new security stamp after both changes.
`AfterAChange_TheSessionSurvivesAndTheConfirmationIsShown` follows the redirect
and asserts `200` with the confirmation text; `AfterAChange_TheNewPasswordIsTheOneThatWorks`
signs in with the new password in a fresh client. Re-verified in the code: the
account is re-read after the use case committed, so the cookie carries the
rotated stamp rather than the stale one.

## 5. Re-verified controls

Spot-checked again after the change, because the sign-in path was touched:

- step 2 still refuses without verifying the password and without moving the
  counter (SC-2 v66) — asserted by a new test written for exactly this risk;
- step 4 still requires the correct password and leaves the counter alone;
- the unknown-login audit row still carries no actor, no role and no target;
- no password, hash or stamp reaches a view model, an audit row or a log line;
- the three management use cases still consult the read-only guard first;
- no Google call, no outbound traffic, no configuration change;
- the working tree still holds no secret, generated database file or export.

## 6. Verdict rationale (version 3)

**PASS.** All three findings of this review chain are closed: F-1 and F-3 (the
unknown-login timing oracle, in both of its directions) and F-2 (the session
ended by a voluntary password change). Every touched SC item is `PASS`; SC-3,
SC-6, SC-7, SC-9 and SC-12 are `NOT_APPLICABLE` with reasons recorded in
version 1. Five Informational observations (I-1 … I-5) remain and require no
correction. No blocking security Open Decision. No human security decision is
required — only the ordinary approval at `HUMAN_PR_APPROVAL`.

Limitations, unchanged: static and test-based review; the recorded suite result
was read, not re-run; timing behaviour was established from the code path rather
than measured; no penetration testing.
