---
artifact_type: specification_review
story: US-077
version: 2
status: APPROVED
created_at: 2026-11-08T11:55:13Z
updated_at: 2026-11-08T12:08:06Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-077-diagnostics-before-pilot.md
    version: null
  - path: trebovaniya.md
    version: 113
  - path: docs/specifications/US-077-spec.md
    version: 2
  - path: docs/specifications/US-077-spec-digest.ru.md
    version: 2
  - path: docs/decisions/US-077-open-decisions.md
    version: 1
supersedes: docs/reviews/specifications/US-077-spec-review.md
---

# US-077 Specification Review

## Summary

**Verdict: PASS**, 0 Critical, 0 Major, 2 Minor. Every finding of review v1 (M-1, M-2, m-1…m-4; the v1 text is in git,
`5baa244`) is fixed in Specification v2 and digest v2. Two new Minor findings concern wording that the test and
implementation stages should read carefully.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-077-diagnostics-before-pilot.md` | — |
| Requirements | `trebovaniya.md` | 113 (sections 6, 7, 8) |
| Specification | `docs/specifications/US-077-spec.md` | 2 |
| Digest | `docs/specifications/US-077-spec-digest.ru.md` | 2 |
| Open Decisions | `docs/decisions/US-077-open-decisions.md` | 1 |

Also read: `GoogleClassroomReader` (credential, `GuardAsync`, transport), `GoogleRetryHandler`,
`GoogleFailureClassifier`, `GoogleSignInEvents`, `SignInLog`; searched `RefusalLog`, `WorkspaceConnectionRepository`,
`GoogleMeetReportsReader`, `InstallationServices`, every `.cshtml` in `src`. The five framework messages of FR-006 were
confirmed in the installed ASP.NET Core 10.0.12 assemblies.

## Fidelity

- v1 findings, checked against the v2 text and the code:
  - M-1: FR-003 now says a failed token request ends the read and no Classroom retry repeats it. FR-004 bounds one read
    to one failed token request of four HTTP attempts, which matches `GoogleRetryHandler.MaxAttempts`.
  - M-2: FR-006 states that network, time-out and unreadable-answer failures are `Other`. It is named as I-10 and in
    digest section 2.
  - m-1…m-4: VR-002a, S-04, digest I-4 and I-2 read as required.
- FR-005's classification matches `GuardAsync`: `GoogleReadFailedException` passes through, so `KeyUnavailable` raised
  at token time keeps today's kind and diagnosis. Today it is raised in `CreateService`, outside `GuardAsync`.
- `GoogleRetryHandler`'s 100 s per-attempt bound and the sequence length of about 12.7 minutes (I-2) are as stated.
- No rule in `trebovaniya.md` or `security-conventions.md` forbids holding a delegated token in memory, and SC-7's key
  rule is kept (S-02). `no-store` (section 8) is not touched.
- VR-001 has no direct source but follows from AC-005 and S-01 (nothing unusable is remembered).

## Story Coverage

AC-001…AC-011 are all represented, each with a trace. AC-004's bound is fixed (see m-1 for its one gap). Nothing goes
beyond the Story's scope.

## Consistency

- FR-009's inventory was recounted: 16 references in Web and 5 in the Control Plane, 21 in all, matching the table. The
  only two pages with their own document skeleton are the two `_Layout` files, so the error page is covered.
- No `@addTagHelper` exists in `src`, as FR-009 says. The two named tag helpers act only on elements carrying their
  `asp-` attributes, so I-7's claim holds.
- FR-007: 5290 is free in `src`, `tests`, `docs/architecture` and `docs/product`. 5201 occurs twice, as stated.
- FR-008: both repository methods read without ordering; `uq_workspace_connection_singleton` exists.
- The reader is a singleton (`InstallationServices.cs:132`), as FR-001 assumes.
- No permission, read-only, localization or audit behaviour changes.

## Open Items

No `trebovaniya.md` section 7 item bears on this Story. No Open Decision was silently resolved.

## Interpretations

| # | Interpretation | In digest section 2 |
|---|---|---|
| I-1 | Token taken per HTTP attempt | Yes |
| I-2 | "Near expiry" = 5 minutes | Yes |
| I-3 | Removed key or delegation noticed up to about 55 minutes later | Yes |
| I-4 | Expired token removed at the next token request, or stays until a restart | Yes |
| I-5 | Kinds told apart by framework messages | Yes |
| I-6 | `state` invalid = correlation; refusal and Google error = one kind | Yes |
| I-7 | Only two tag helpers | Yes |
| I-8 | Meet reader excluded | Yes |
| I-9 | New id 5290 | Yes |
| I-10 | Exchange failures without an answer from Google are `Other` | Yes |
| I-11 | A failed token request ends the read | Yes |
| I-12 | A token reported as already expired fails the read | Yes |

AC-011's reading of "pages" (text and behaviour) is stated in the Specification's table and restated by I-7.

## Digest Fidelity

Plain Russian, traces to the Specification, names every interpretation and claims no fix of the rehearsal failure. No
open decision is hidden, since there are none.

## Findings

### m-1 (Minor): FR-004's "exactly one" does not hold for a token that is not remembered

*Location:* FR-004, VR-002. *Checked against:* AC-004.

FR-004 says that when the endpoint succeeds, N concurrent reads cause exactly one token request. Under VR-002, a token
with a missing or short `expires_in` is not remembered, so each waiting read finds no entry and requests its own. The
same applies to the successful answers that VR-001 and VR-002a reject. Google does not answer so in practice, and AC-004
is met in the normal case. The bound should be read, and tested, as applying to a token that can be remembered.

### m-2 (Minor): FR-003 leaves the token lookup's place "to the design", but no design stage will settle it

*Location:* FR-003. *Checked against:* Story Notes (empty API and database deltas), `GoogleRetryHandler`.

The API and database design stages are expected to be empty for this Story, so the placement is really decided at
implementation. One consequence is not spelled out. `GoogleRetryHandler` catches `OperationCanceledException` from the
100 s attempt bound and repeats the attempt. If the token lookup or the wait for another read's token ran under that
bound, a slow token request would be cut off and retried, against FR-003's "keeps today's behaviour" and the "waits"
of FR-004. The rule is implied by FR-003; test writing and implementation should treat the token request and the wait
as outside the Classroom attempt's bound.

## Verdict

`PASS`. Ready for `HUMAN_SPEC_APPROVAL`. m-1 and m-2 are advisory.

Delegation: nothing was delegated. Every citation was read directly.
