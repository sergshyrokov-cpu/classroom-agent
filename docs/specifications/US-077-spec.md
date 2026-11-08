---
artifact_type: specification
story: US-077
version: 1
status: DRAFT
created_at: 2026-11-08T11:49:55Z
updated_at: 2026-11-08T11:49:55Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-077-diagnostics-before-pilot.md
    version: null
  - path: trebovaniya.md
    version: 113
  - path: docs/decisions/US-077-open-decisions.md
    version: 1
supersedes: null
---

# US-077 Specification: fewer Google token requests, and logs that explain failures, before the pilot

Grounded in `trebovaniya.md` v113: section 6 (read-only scopes), section 8 (no inline code, `no-store` with static
interface files excepted, v85, v95), section 9. Conventions: SC-7, SC-10, SC-11, SC-13, DC-10, AD-4, AD-9.

The Story was aligned with v113 and changes no user-visible text (Owner, 2026-11-08), so `trebovaniya.md` does not
change. Section 7 has no open item this Story depends on. The Story has no Open Decisions, and this Specification
opens none.

Code facts below were read from the repository at `810d12f`; the framework's failure messages (FR-006) were checked
in the installed ASP.NET Core 10.0.12 assemblies.

## Overview

Five independent, small changes to diagnostics and efficiency. Four of them change no behaviour a user can observe;
the fifth makes browsers fetch changed CSS and JavaScript.

## Business Goal

Remove a suspected trigger of the rehearsal's token refusal, make a failed Google sign-in diagnosable from the log,
and stop a kit update leaving a half-old interface. The Specification does not claim that the rehearsal failure is
fixed: its cause is unproven (Story, Notes).

## Business Flow

There is no new user flow. A synchronization runs as before and asks Google for a token about once an hour per
technical account and scope set. A failed Google sign-in looks the same to the user and gains a reason in the log.
After a kit update, the next page a browser loads refers to the changed files under a new address.

## Functional Requirements

### FR-001 What is remembered

`GoogleClassroomReader` remembers access tokens in the process's memory, in an instance owned by the singleton reader
(no static mutable state). One entry holds exactly: the key of FR-002, the token string, and its expiry instant.

The entry holds **no key material**. A `ServiceAccountCredential` holds the private key, so remembering one instance
across reads is forbidden (S-02). A token is obtained by building the credential as today, for that one token request
only, and keeping only the resulting token string and expiry.

The token cache is entirely inside `Infrastructure`; no port or Google SDK type changes (AD-4). `IClassroomReader` is
unchanged.

### FR-002 The key of a remembered token

A token is remembered and reused under the pair (technical account as passed to the read, scope set). Both compare
ordinally and exactly; the scope set compares regardless of order. Today's two scope sets (`CourseAndRosterScopes`,
`CourseWorkScopes`) stay as they are and each gets its own token. A token is never sent for a different pair.

### FR-003 When a token is reused and when a new one is requested

Each outgoing Classroom HTTP attempt, including each page of a paged read and each retry, takes its token at that
moment, not once per read:

1. If an entry for the pair exists and `now + 5 minutes < expiry`, its token is sent, and no key is loaded.
2. Otherwise a new token is requested, and the entry replaced, before the attempt is sent.

`now` comes from the reader's `TimeProvider`. `expiry` is the instant just before the token request was sent, by that
`TimeProvider`, plus the `expires_in` Google returned. The 5-minute margin is a constant, not a setting, for the same
reason as `PageSize`.

A token request keeps today's behaviour: the same `TokenServerUrl`, delegated subject, retry handler
(`GoogleRetryHandler`), per-attempt bound and no library back-off. Its HTTP client lives for that request and does not
depend on a `ClassroomService` that a finished read has disposed.

### FR-004 Concurrent reads

For each pair, at most one token request is in flight at any moment. A read that finds another read requesting the
token for its pair waits (honouring its own `CancellationToken`) and then re-checks the entry.

**The exact bound for AC-004:** when the token endpoint succeeds, N concurrent reads for one pair with no valid token
cause **exactly one** token request; for K different pairs, exactly K. When it fails, the waiting reads try one after
another, so there are never more token requests than reads, which is today's count.

### FR-005 Failures, a rejected token, and clean-up

- A failed token request is never remembered: no entry is written, an existing entry stays only if it is still valid
  under FR-003, and the next attempt requests a token again (AC-005).
- Every failure ends in the same `GoogleReadFailureKind` and `SyncDiagnosis` as today, through the same `GuardAsync`
  classification. A missing or unusable key ends as `Configuration` / `KeyUnavailable`, at the moment a token is needed
  (I-3).
- If Classroom answers `401` to an attempt, the entry is dropped if it still holds the token that attempt carried,
  and the attempt is repeated with a new token, as the Google library's credential does today. The number of attempts
  is no larger than today.
- An expired entry is removed whenever a token request is made for any pair, so a token of a replaced technical
  account does not stay in memory indefinitely (I-4).

### FR-006 The reason for a failed Google sign-in

`GoogleSignInEvents.OnRemoteFailureAsync` determines one kind from this closed list, checked in this order against
`context.Failure`:

| Order | `FailureKind` | Condition |
|---|---|---|
| 1 | `CorrelationFailed` | message equals `Correlation failed.` or `The oauth state was missing or invalid.` |
| 2 | `TokenExchangeFailed` | message starts with `OAuth token endpoint failure` or equals `Failed to retrieve access token.` |
| 3 | `GoogleError` | `Data` contains the key `error`, or message equals `Access was denied by the resource owner or by the remote server.` |
| 4 | `Other` | anything else, including no failure object |

Order matters: a token-endpoint failure with a standard error body also carries `Data["error"]`, so rule 2 precedes
rule 3. The message and `Data` are only compared in memory and never written, logged or carried further. If a future
framework version changes a message, the result degrades to `Other`, and a test that drives the real authentication
handler catches it (I-5).

In this path the existing line of event `AdminSignInCallbackFailed` (2203, `Warning`) gains `{FailureKind}` and is
still the **only** application log line written for the failure: `Warning`, the kind and `{RequestId}`. The same event
in `OnTicketReceivedAsync` is unchanged and carries no kind. The audit row, the `TempData` refusal key, the redirect
and the user's text are unchanged.

The kind's type and its mapping live in `ClassroomAgent.Web.Security` beside `SignInLog`.

### FR-007 Distinct event ids

`ReadOnlyWriteRefused` in `RefusalLog.cs` takes the id **5290**, free in the solution. `RetentionPurgeStarted` keeps
5201. Both names are unchanged.

`2420` also appears twice (`GoogleClassroomReader`, `GoogleMeetReportsReader`); that is one event, `SyncGoogleRetry`,
written by two readers, not a collision, and is left as it is.

### FR-008 No warning 10103

`WorkspaceConnectionRepository.GetForReadAsync` and `GetForUpdateAsync` order by the primary key before
`FirstOrDefaultAsync`. The result is the same single row: `uq_workspace_connection_singleton` allows no second one.
Tracking behaviour is unchanged (untracked for read, tracked for update).

### FR-009 Versioned static files

Each of the 21 references below carries `asp-append-version="true"`, whose rendered address changes with the file's
content:

| Host | View | File |
|---|---|---|
| Web | `Shared/_Layout` | `/lib/bootstrap/css/bootstrap.min.css`, `/css/site.css` |
| Web | `ConnectionInstruction/Index` | `copy-instruction.js` |
| Web | `Courses/Index`, `Journal/Index`, `MeetCodes/CourseChoice`, `MeetReports/Index`, `Report/Index` | `picker-narrow.js` (one each) |
| Web | `MeetReports/Index`, `MeetReports/Meeting` | `meet-export.js` (one each) |
| Web | `Report/Index` | `report-export.js` |
| Web | `ReportTemplates/Form` | `report-template-scale.js` |
| Web | `DeanAccounts/Index`, `DeanPassword/Forced`, `DeanPassword/Own`, `SignIn/Index` | `password-toggle.js` (one each) |
| Control Plane | `Shared/_Layout` | `/lib/bootstrap/css/bootstrap.min.css`, `/css/site.css` |
| Control Plane | `Installations/Detail` | `copy-identifier.js` |
| Control Plane | `Setup/Index`, `SignIn/Index` | `password-toggle.js` (one each) |

Neither host enables the MVC tag helpers today, so `asp-append-version` alone would be emitted as a literal attribute.
Each host's `Views/_ViewImports.cshtml` adds **only** the two tag helpers needed, by full type name:
`Microsoft.AspNetCore.Mvc.TagHelpers.ScriptTagHelper` and `Microsoft.AspNetCore.Mvc.TagHelpers.LinkTagHelper`. The
wildcard import is forbidden here: it would also activate the form tag helper, which adds antiforgery fields and could
change the existing forms (AC-011, I-6).

The `Cache-Control` of static files stays as it is (`NoStoreMiddleware.MarkStaticFile`), and no inline code is added.

## Acceptance Criteria

The Story's criteria, unchanged, with the bounds this Specification fixes.

| AC | Statement | Fixed here |
|---|---|---|
| AC-001 | Several consecutive reads for one account and scope set within validity: token endpoint called once, every read carries that token | FR-001…003 |
| AC-002 | A token issued for one account or scope set is never sent for another | FR-002 |
| AC-003 | At or near expiry by the test's clock, a new token is requested before the read is sent; no read goes out with an expired token | "near" = within 5 minutes, FR-003 |
| AC-004 | Concurrent reads with no valid token: all succeed, none receives another account's token, token requests stay small | exactly one per pair on success, FR-004 |
| AC-005 | Token endpoint refuses or is unavailable: same failure kind and `SyncDiagnosis` as today; after recovery the next read requests a new token and succeeds | FR-005 |
| AC-006 | The key is not held between token requests; neither token nor key appears in a log line, the database or a response | FR-001, S-01…S-03 |
| AC-007 | For each kind in the closed list, one `Warning` with the kind and request id; no failure message, email or query string; user's view and audit row as before | FR-006 |
| AC-008 | `ReadOnlyWriteRefused` and `RetentionPurgeStarted` have different ids, names unchanged, neither new id collides | FR-007 |
| AC-009 | Reading the connection for display and for update raises no `FirstWithoutOrderByAndFilterWarning`, same single row | FR-008 |
| AC-010 | Every reference to the host's own CSS or JavaScript carries the content version; changing a file changes it; no inline code; file loads under CSP | FR-009 |
| AC-011 | Translation files, pages, audit events and Google scopes unchanged; no NuGet package added | "Pages" reads as their text and behaviour; FR-009 is the only change to views, I-7 |

## Validation Rules

- **VR-001** A token response with an empty token string is not remembered; the read ends as `Unexpected`.
- **VR-002** A token whose `expires_in` is missing, zero, negative, or not above the 5-minute margin is used for the
  attempt that requested it and is not remembered.
- **VR-003** `FailureKind` takes only the four values of FR-006.
- **VR-004** Reader inputs keep today's guards (`ArgumentException.ThrowIfNullOrWhiteSpace`).

No request body, query parameter or endpoint is added.

## Security Requirements

- **S-01** The token lives only in process memory: never in the database, a file, a log, an exception message or a
  response (SC-10, Story). The type holding it must not expose it through `ToString` or a record's generated members.
- **S-02** The key is resolved from `ISecretStore` when a token is needed and is not kept between token requests (SC-7,
  US-011 FR-016). No field, cache or closure outlives the token request while holding the key or a credential built
  from it.
- **S-03** A token is reused only for the identical account and scope set. Each read keeps its minimal read-only scopes,
  and no scope is added (Hard Stops, `trebovaniya.md` section 6).
- **S-04** The `FailureKind` line holds only a closed-list value and the request id: no exception message, Google
  response, callback address, query string or email (SC-10). Framework `Information` lines carrying the failure message
  stay filtered by the existing `Microsoft` → `Warning` override (`InstallationLogging`), which this Story does not
  change.
- **S-05** No new outbound destination: token requests go to the same endpoint as today (SC-13).
- **S-06** `GoogleAccessProbe` does not use the cache: «check access» really asks Google each time.
- **S-07** No authorization, session or audit behaviour changes (SC-11).

## Error Handling

As today, through `GuardAsync` and US-017's closed list (AD-9); nothing new is shown to a user. A waiter cancelled
while waiting for a token ends as any cancelled read does.

## Non-Functional Requirements

- Token requests fall from one per read to about one per hour for each (account, scope set) pair in use.
- Reads gain no extra round trip. Waiting occurs only while a token is actually being requested.
- All tests use a fake token endpoint and synthetic fixtures (TC-4); expiry is driven by a fake `TimeProvider`.
- No new package, setting, schema, migration, endpoint, translation key, page or audit event.

## Out of Scope

- **P-7 b:** how a token refusal after earlier successes is diagnosed and what the Admin reads (needs a new
  `trebovaniya.md` version; Owner, 2026-11-08).
- Any change to a user-visible text, screen, right, endpoint, schema or audit event.
- `GoogleAccessProbe` (S-06).
- `GoogleMeetReportsReader`: it builds its credential per read in the same way, but the Story names only
  `GoogleClassroomReader`, and the rehearsal evidence concerns the Classroom reads (I-8).
- The `no-store` rule and the caching of static files.
- Other places that might raise warning 10103; the Story names this repository.

## Open Decisions

None. See `docs/decisions/US-077-open-decisions.md`.

## Interpretations for the Owner to confirm

- **I-1** Token taken per HTTP attempt, not per read, because a long paged read can outlast a token and AC-003 says a
  read never goes out with an expired token (FR-003).
- **I-2** "Near expiry" is 5 minutes (FR-003). Long enough to cover a retry sequence's single attempt; Google tokens
  last an hour.
- **I-3** **Consequence of reuse:** while a remembered token is valid the key is not loaded and Google is not asked. A
  key removed, or a delegation withdrawn, during a run is noticed only when the token expires or Classroom refuses it,
  at most about 55 minutes later. Today a removed key is noticed at the next read. AC-005's "same failure kind" holds
  from that moment on. This is inherent in the Story's goal.
- **I-4** "Only until it expires" (Story): an expired token is never used and is removed at the next token request of
  any pair (FR-005), not by a timer. If no token is requested at all, a worthless expired string stays in memory until
  the next one or a restart.
- **I-5** Google's sign-in failures are told apart by comparing the framework's fixed messages in memory, because they
  share one exception type (Story, Notes). There is no other discriminator for correlation failures (FR-006).
- **I-6** `state` missing or invalid counts as `CorrelationFailed`: both mean this return cannot be tied to a sign-in
  started here. Refusal by the user and an error from Google are one kind, as in the Story's list.
- **I-7** Only the two tag helpers are enabled (FR-009). The rendered pages change only in the `?v=` of those addresses.
- **I-8** The Meet reports reader is left out of this Story (Out of Scope). Its reads are few compared with
  per-course Classroom reads. It can be added later by the same pattern.
- **I-9** The new id is 5290, outside the retention series 5201…5205 (FR-007).

## Traceability

| AC | Functional requirement / rule | Security |
|---|---|---|
| AC-001 | FR-001, FR-002, FR-003 | S-03 |
| AC-002 | FR-002 | S-03 |
| AC-003 | FR-003, VR-002 | — |
| AC-004 | FR-004 | S-03 |
| AC-005 | FR-005, VR-001 | S-01 |
| AC-006 | FR-001, FR-005 | S-01, S-02 |
| AC-007 | FR-006, VR-003 | S-04, S-07 |
| AC-008 | FR-007 | — |
| AC-009 | FR-008 | — |
| AC-010 | FR-009 | S-07 |
| AC-011 | FR-009, Non-Functional Requirements | S-03, S-05, S-06, S-07 |

**Manual check, on the server:** after the first kit update, a page shows the new styles without Ctrl+F5 (Story, Notes).

Delegation: nothing was delegated. Lookups were a handful of direct reads.
