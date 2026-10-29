---
artifact_type: specification_review
story: US-064
version: 1
status: APPROVED
created_at: 2026-10-29T07:04:05Z
updated_at: 2026-10-29T07:04:05Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-064-disabled-dean-session-refused-on-next-request.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/specifications/US-064-spec.md
    version: 1
  - path: docs/specifications/US-064-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-064-open-decisions.md
    version: 1
supersedes: null
---

# US-064 Specification Review

## Summary

**Verdict: PASS.** The Specification is faithful to `trebovaniya.md` §2 (v103, «Отключение действует
сразу»), BR-014 and SC-2; it covers all four Acceptance Criteria, stays test-only with the defect fix
fenced (FR-008), and its code facts match the source. Two Minor findings, both about the strength of
the tests the Specification asks for. Nothing was delegated; the reviewer read every cited text and
code location itself.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-064-disabled-dean-session-refused-on-next-request.md` | — |
| Requirements | `trebovaniya.md` | 104 |
| Specification | `docs/specifications/US-064-spec.md` | 1 |
| Digest | `docs/specifications/US-064-spec-digest.ru.md` | 1 (input: spec v1 — current) |
| Open Decisions | `docs/decisions/US-064-open-decisions.md` | 1 |

## Fidelity

- §1 quote of `trebovaniya.md` §2 (lines 737–740) is verbatim. The Admin-revocation limitation
  (lines 684–687) is cited correctly in §10.
- SC-2 (security-conventions.md ~123–129): per-request validation against stamp and disabled
  state, periodic interval is a finding — matches spec §2 and §7.
- SC-2 lines 52–57 (disabled message only on the sign-in form) — matches spec §7.
- Code facts §1.1 checked against source: `AppUser.Disable` rotates the stamp, `ReEnable` does not
  (`AppUser.cs:132-146`); `AccountSessionService.IsCurrentAsync` checks disabled + stamp;
  `InstallationSession.ValidatePrincipalAsync` rejects and signs out; `OnRedirectToLogin` gives
  `401` API-6 under `/api/v1`, else redirect to `/sign-in` (`SignInRoutes.SignInPage`); no
  `SecurityStampValidator`/`ValidationInterval` in `src`; routes `/workspace/journal`,
  `/api/v1/exports/journal-xlsx`, `/settings/deans/{deanId:long}/state`; policies `ViewJournal`,
  `UseReportTemplates` (Admin and Dean), `ManageDeanAccounts`. All correct.
- No rule without a source; FR-001…FR-007 are test requirements derived from the Story's scope,
  FR-008 is the Story's own scope item.

## Story Coverage

AC-001 → FR-004, AC-002 → FR-005, AC-003 → FR-006, AC-004 → FR-007; FR-001–FR-003 apply to all.
Out-of-scope items match the Story (in-progress request, Admin revocation, UI). US-067 (login change)
correctly excluded. Nothing beyond the Story.

## Consistency

No new endpoint, policy, audit action or text: permission matrix, localization (NFR-073) and SC-11
are untouched. Consistent with US-012 (Disable/ReEnable, FR-007/FR-008 there) and the sign-out replay
model of US-049 / `InstallationSignOutTests`. Read-only mode (BR-026) is not mentioned — see m-2.

## Open Items

`trebovaniya.md` §7 has no item on sessions or Dean accounts. OD-001 resolved a) by the Owner,
carried verbatim. No silently resolved open item.

## Interpretations

1. FR-005 / FR-003 — "no export audit event" read as "count unchanged across the replay" (one real
   export happens before disabling). Named in spec §11.1 and digest §2.2.
2. FR-004 — "no journal content" read as a `302` to `/sign-in` with no journal body. Spec §11.2,
   digest §2.3.
3. FR-006 — re-sign-in uses a Dean past the first-login password change. Spec §11.3, digest §2.4.
4. FR-003 — the "show the test can fail" proof is a success of the same request before disabling
   (from the Story's Notes). Digest §2.1.
5. FR-001 — disabling goes through the HTTP state endpoint, not a database change (the Story's
   "through the Dean-accounts page"). Not listed as an interpretation, but it is the Story's literal
   wording, not a choice.

All choices are visible in the digest.

## Digest Fidelity

Every digest statement traces to the Specification; OD-001 is presented as resolved with its
resolution; out-of-scope list matches §10. Plain Russian, no softening. FR-008 (defect fix in scope)
is stated honestly.

## Findings

- **m-1 (Minor)** — spec FR-003 / FR-007 (AC-004). FR-003's "same request succeeds before disabling"
  proves the fixture works, but for AC-004 the asserted outcome after disabling is also *success*
  (the other Dean still opens the journal), so that test passes even if the disabling never
  happened (wrong `deanId`, state endpoint refused). The Specification should require the AC-004
  test to also assert that the disabled Dean's cookie is refused in the same run. Checked against
  the Story Notes ("each test must also be shown able to fail").
- **m-2 (Minor)** — spec §1.1 / FR-001. The Specification does not state that the installation is in
  normal (not read-only) mode for these tests. Under BR-026 read-only mode blocks everything but
  viewing and export; whether the fixture's state endpoint call is accepted depends on it. A single
  sentence ("the installation is licensed and not read-only") removes the ambiguity for TEST_WRITING.

## Verdict

PASS — no Critical or Major findings; m-1 and m-2 go to the Owner at HUMAN_SPEC_APPROVAL.
