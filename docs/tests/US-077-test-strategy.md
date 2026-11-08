---
artifact_type: test_strategy
story: US-077
version: 1
status: DRAFT
created_at: 2026-11-08T13:37:28Z
updated_at: 2026-11-08T13:37:28Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-077-diagnostics-before-pilot.md
    version: null
  - path: docs/specifications/US-077-spec.md
    version: 2
  - path: docs/decisions/US-077-open-decisions.md
    version: 1
  - path: docs/designs/api/US-077-api-design.md
    version: 1
  - path: docs/designs/database/US-077-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 113
supersedes: null
---

# US-077 Test Strategy

## 1. Scope

Five independent changes: reuse of the Google access token in `GoogleClassroomReader` (AC-001…006), the kind of a
failed Google sign-in in the log (AC-007), the event id collision (AC-008), EF Core warning 10103 (AC-009), versioned
static files (AC-010), and "nothing else changed" (AC-011). API and database designs are NOT_APPLICABLE: no endpoint,
schema or migration is involved.

## 2. Test levels

| Level | Where | Used for |
|---|---|---|
| Adapter, offline | `Infrastructure/Google/GoogleClassroomReaderTokenReuseTests` | AC-001…006: the real reader over a fake token endpoint, a manual clock and a spy secret store (TC-4) |
| Host integration | `Web/BackgroundServices/TokenReuseSyncTests` | AC-001, AC-006: a whole run, real adapter, PostgreSQL |
| Host integration, real auth handler | `Web/Logging/GoogleSignInFailureKindTests` (first group) | AC-007: real framework messages through the real Google handler |
| Event called directly | `Web/Logging/GoogleSignInFailureKindTests` (second group) | FR-006 table, row by row, with boundaries |
| Source rule | `Architecture/EventIdCollisionTests`, `Architecture/VersionedStaticFileRuleTests` | AC-008, AC-010, AC-011 |
| Integration, PostgreSQL | `Infrastructure/Persistence/WorkspaceConnectionRepositoryOrderingTests` | AC-009: a context with the warning turned into an exception (TC-2) |
| Page, over HTTP | `Web/Pages/VersionedStaticFilePageTests` | AC-010, AC-011: rendered pages of both hosts |

No compile-only skeleton is needed: the reader's constructor and port are unchanged, and the tests assert only
observable behaviour (what crosses the transport, what is logged, what is rendered). The Specification does not name
the cache's internal types, so no test names them.

## 3. Scenarios

**Positive.** Consecutive reads, pages and a whole run share one token per pair; each scope set gets its own; a
versioned address is served; each FR-006 kind is logged.

**Negative.** A token is never sent for another account or scope set; an expired token is never sent; a refusal or an
outage is not remembered; a `401` for an older token does not drop a newer one; a failure's message, the email, the
code, the `state` and the query string are never logged; the key and the token are held or shown nowhere they must
not be.

**Boundary.** The 5-minute margin at 3299 / 3300 / 3301 s; expiry counted from before the request; `expires_in`
missing, 1, 300, 301, 0, -1; empty token; the FR-006 comparisons exact (case, trailing text, leading space, missing
period, empty message, no failure object).

**Concurrency.** N reads of one pair: exactly one request. K pairs: exactly K. On refusal: N requests, one at a time.
A token that cannot be remembered: each read its own, one at a time (SPEC_REVIEW m-1). A read cancelled while waiting
makes none. The token request and the wait for it stay outside the Classroom attempt's 100 s bound (SPEC_REVIEW m-2).

**Validation.** VR-001, VR-002, VR-002a, VR-003 as above. VR-004 is today's guards, already covered by US-014 tests.

**Security.** S-01: the token in no log line, failure, table or `ToString`. S-02: the key resolved once per token
request and none of it reachable from the reader between requests (a field walk that must also find the token, so it
is not vacuous). S-03: exact scope sets. S-04: only the kind and the request id. S-07: the redirect, the session
cookie and the audit row as before. No endpoint or rights change, so TC-5 adds nothing.

**Persistence.** AC-009 only; no schema change.

## 4. Fixtures

- A `World` private to the reader tests: a fake token endpoint issuing a distinct token per request and recording its
  subject and scopes, scripted Classroom answers, gates to hold answers open, a count of token requests in flight.
- `GoogleSignInStub` gains `TokenAnswer` and `UserInformationAnswer`, so the failures reach the real handler.
- `Help.ScriptSources` and `ConnectionInstructionHostExtensions.ScriptSources` now return the address without its
  `?v=`. FR-009 changes those addresses, and the tests that use them are about which script is included; their
  assertions are otherwise unchanged.
- The concurrency tests hold the token answer open for up to 200 ms of real time so a defective, unserialized reader
  can show itself. A correct reader passes for any length of this window, so the result does not depend on timing.

## 5. Excluded, with reason

- **Scope set compared regardless of order (FR-002).** Today's two scope sets are private constants of fixed order, so
  no public path can present one in another order. To be read in IMPLEMENTATION_VERIFICATION.
- **`GoogleAccessProbe` not using the cache (S-06).** The class is outside the change; `GoogleAccessProbeTests` stays
  and the diff must not touch it.
- **Two parts of AC-011 are read from the diff, not by a test:** translation files and NuGet references. A test pinning
  today's contents would fail on the next Story that legitimately adds a key or a package. IMPLEMENTATION_VERIFICATION
  checks that `git diff` touches no `.resx` and no `PackageReference`.
- **A browser's real behaviour after a kit update** is the manual check on the server named by the Story.

## 6. Known limitations

- "Changing a file changes the version" is proved by the rendered `v=` being the SHA-256 of the file's bytes, not by
  editing a file under a running host.
- FR-006 depends on framework messages; the real-handler group is what catches a change in a future framework version.
- A test that scripted a second token for a second read of one reader would have to change with FR-003; none was found
  by reading the existing reader tests, and the full suite at IMPLEMENTATION will confirm it.

## 7. Open Decisions affecting testing

None.
