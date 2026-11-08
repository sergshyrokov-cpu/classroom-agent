---
artifact_type: specification_review
story: US-077
version: 1
status: APPROVED
created_at: 2026-11-08T11:55:13Z
updated_at: 2026-11-08T11:55:13Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-077-diagnostics-before-pilot.md
    version: null
  - path: trebovaniya.md
    version: 113
  - path: docs/specifications/US-077-spec.md
    version: 1
  - path: docs/specifications/US-077-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-077-open-decisions.md
    version: 1
supersedes: null
---

# US-077 Specification Review

## Summary

**Verdict: CHANGES_REQUIRED**, 0 Critical, 2 Major, 4 Minor. Scope, static-file inventory, event id and EF change are
accurate. The two Major findings are small to fix: the token and retry layers are not defined together (M-1), and one
reading of "token exchange failed" is not shown to the Owner (M-2).

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-077-diagnostics-before-pilot.md` | — |
| Requirements | `trebovaniya.md` | 113 (sections 6, 7, 8) |
| Specification | `docs/specifications/US-077-spec.md` | 1 |
| Digest | `docs/specifications/US-077-spec-digest.ru.md` | 1 |
| Open Decisions | `docs/decisions/US-077-open-decisions.md` | 1 |

Also read: SC-7, SC-10, SC-11, SC-13, DC-10, AD-4, AD-9; US-011 FR-016; `GoogleClassroomReader`, `GoogleRetryHandler`,
`GoogleFailureClassifier`, `GoogleSignInEvents`, `SignInLog`, `RefusalLog`, `WorkspaceConnectionRepository`,
`InstallationLogging`, `NoStoreMiddleware`, both `_ViewImports.cshtml`, `CompleteGoogleSignInUseCase`.

## Fidelity

- Read-only scopes unchanged (section 6); no new outbound destination (SC-13).
- No rule in `trebovaniya.md`, SC-*, NFR-* or BR-* forbids holding a delegated access token in memory; the Story
  allows it and binds it (S-01…S-03). SC-7's key rules are kept: the key is not held between token requests.
- `no-store` exception for static files (section 8, v85) and the no-inline-code rule are respected.
- FR-006: `SignInRefusal.SignInFailed` is the only outcome of `RecordFailedCallbackAsync`, so event 2203 is the line
  that gains the kind. Audit and redirect are untouched.
- FR-007: 5290 is unused in `src` and `tests`; 5201 occurs exactly twice, as stated.
- FR-008: both repository methods are as described.
- VR-001, VR-002 have no direct source but serve AC-003, AC-005 and S-01. See m-1.

## Story Coverage

AC-001…AC-011 are all represented. AC-004's bound, left to the Specification by the Story, is fixed in FR-004, but the
failure half of it is not true as written (M-1). Nothing goes beyond the Story's scope. The Meet reader's exclusion is
consistent with Story Scope item 1 and is disclosed (I-8).

## Consistency

- FR-009's inventory was recounted independently: 16 references in Web and 5 in the Control Plane, 21 in all, matching
  the table. No multi-line tag, `Layout = null` view or reference built in C# exists, so the error page is covered
  through `_Layout`.
- No `@addTagHelper` exists in `src`, as FR-009 says.
- No permission, read-only, localization or audit behaviour changes.

## Open Items

No `trebovaniya.md` section 7 item (14, 26…30) bears on this Story. No Open Decision was silently resolved.

## Interpretations

| # | Interpretation | In digest section 2 |
|---|---|---|
| I-1 | Token taken per HTTP attempt | Yes |
| I-2 | "Near expiry" = 5 minutes | Yes |
| I-3 | Removed key or delegation noticed up to about 55 minutes later | Yes |
| I-4 | Expired token removed at the next token request | Yes, incomplete (m-3) |
| I-5 | Kinds told apart by framework messages | Yes |
| I-6 | `state` invalid = correlation; refusal and Google error = one kind | Yes |
| I-7 | Only two tag helpers | Yes |
| I-8 | Meet reader excluded | Yes |
| I-9 | New id 5290 | Yes |
| — | A sign-in token exchange that fails by network error or unreadable answer is `Other`, not `TokenExchangeFailed` | **No (M-2)** |
| — | An `expires_in` of zero or below still sends the token once | No (m-1) |

## Digest Fidelity

Plain Russian, traces to the Specification, and claims no fix of the rehearsal failure. Gaps: M-2 and m-3. The Owner
is also not told about M-1's behaviour, which must be settled first.

## Findings

### M-1 (Major): FR-003 and FR-004 do not define how a token request sits inside the retry loop

*Location:* FR-003, FR-004, FR-005. *Checked against:* `GoogleRetryHandler`, Story Notes ("Token requests keep the
retry behaviour they have today"), AC-005.

FR-003 requires each attempt, "including each retry", to take its token at that moment. Retries happen inside
`GoogleRetryHandler`, under the Google library, so meeting FR-003 places the token lookup inside that loop. The loop
catches `HttpRequestException` and repeats, and a token request that has used up its own four attempts ends in exactly
that bare exception (it is also how `429` and `5xx` end, `GoogleFailureClassifier.IsUnavailable`). So one read could
make up to 4 × 4 = 16 token attempts, with the pauses multiplied, during the very token refusal this Story is meant to
make less likely. FR-004's statement that there are "never more token requests than reads, which is today's count" is
then false, and FR-003's "keeps today's behaviour" is not met.

*Required:* state that a failed token request ends the read and is not repeated by the Classroom attempt's retry loop,
and bound the token attempts of one read in FR-004, so tests can assert it.

### M-2 (Major): an undisclosed reading of "token exchange failed"

*Location:* FR-006, digest section 2. *Checked against:* AC-007, checklist item 5.

Rule 2 recognises only an answer from Google's token endpoint with a readable body. A network failure or a timeout
during the exchange, or an error answer whose body is not JSON, arrives with another exception and falls to `Other`.
The Story's purpose is that the log says why a sign-in failed, and "the exchange failed" is a natural reading for these
cases too. Keeping them as `Other` may be right, because the exception type alone does not separate them from a failed
user-information request, but it is a choice, and the digest does not name it.

*Required:* either classify these cases, or state in FR-006 that they are `Other` and name that in the interpretations
and digest section 2.

### m-1 (Minor): VR-002 sends a token already expired by its own answer

An `expires_in` of zero or below "is used for the attempt". AC-003 says a read never goes out with an expired token.
Harmless in practice, since Google does not answer so; treating zero or below as a failed token request would remove
the conflict. A missing or short `expires_in` is fine as written.

### m-2 (Minor): S-04 names only the framework's `Information` lines

`Microsoft.AspNetCore.Authentication` also writes `Warning` lines for a correlation failure (`CorrelationCookieNotFound`
and its neighbours, strings confirmed in the 10.0.12 assembly), and these pass the `Microsoft` → `Warning` override.
They exist today and hold no failure message. FR-006 correctly says "application log line", but AC-007's "one Warning"
test must count by the application's category, and S-04 should say so.

### m-3 (Minor): digest on I-4 omits the case the Specification names

The Specification says that if no token is requested, the expired string stays until the next request or a restart.
The digest stops at "at the next token request". This is the one place where "only until it expires" (Story, Security
constraints) is relaxed, so the Owner should read it.

### m-4 (Minor): I-2's reason is unclear

"Long enough to cover a retry sequence's single attempt" mixes two things. One attempt is bounded by 100 s; a whole
sequence can last about 12.7 minutes (4 × 100 s plus 3 × 120 s), which is more than the margin and is why I-1 exists.

## Verdict

`CHANGES_REQUIRED`, loop back to `SPECIFICATION`. M-1 and M-2 must be fixed before the human gate; m-1…m-4 are advisory
and cheap to fix in the same revision.

Delegation: nothing was delegated. Every citation was read directly.
