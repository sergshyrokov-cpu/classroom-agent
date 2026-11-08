---
id: US-077
epic: EPIC-1
title: Fewer Google token requests, and logs that explain failures, before the pilot
slug: diagnostics-before-pilot
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-11-08T11:01:39Z from the deployment rehearsal notes P-2, P-3, P-4, P-7
# (D:\HyperV\rehearsal-findings.md) and the pre-pilot triage of 2026-11-04.
# Decided by the Owner 2026-11-08: no user-visible text changes, so no new trebovaniya.md version.
---

# User Story

As the **Owner running the pilot**

I want synchronization to ask Google for far fewer tokens, the log to say why a sign-in failed, and browsers to pick
up new styles after an update

So that a long first synchronization is less likely to break, a failure can be diagnosed from the log alone, and an
update does not leave anyone with a half-old interface.

---

# Business Value

The rehearsal on a real domain requested a new Google token for every read, about one per second for 30 minutes, and
one run then failed with a token refusal after 672 courses had worked. A rerun did not repeat it, so the cause is a
suspicion, not a proven fact; but the first synchronization takes over a day, and a stopped run is expensive.

During the pilot the Owner has only the log to work from. Today a failed Google sign-in leaves no reason in it, two
different events share one id, and one needless warning appears on every synchronization run. Separately, nothing
tells a browser that CSS or JavaScript changed after a kit update.

---

# Scope

**In scope:**

1. **Reuse of the Google access token (P-7 a).** `GoogleClassroomReader` obtains a new token only when it has no valid
   one for the same technical account and scopes, instead of on every read.
2. **The reason for a failed Google sign-in in the log (P-4).** `GoogleSignInEvents.OnRemoteFailureAsync` writes one
   Warning naming the kind of failure from a closed list.
3. **Unique event id (P-2).** `ReadOnlyWriteRefused` and `RetentionPurgeStarted` no longer share `5201`.
4. **No needless EF Core warning (P-3).** `WorkspaceConnectionRepository` reads the singleton row in a way that does
   not raise warning 10103.
5. **New static files reach the browser (pre-pilot triage item 4).** Every reference to the application's own CSS or
   JavaScript, in both hosts, carries a version that changes with the file's content (`asp-append-version` or
   equivalent).

**Out of scope:**

- **P-7 b:** how a token refusal after earlier successes is diagnosed and what the Admin reads. It would change a
  user-visible text and needs a new `trebovaniya.md` version; the Owner decided not to do it now.
- Any change to a user-visible text, screen, right, endpoint, schema or audit event.
- `GoogleAccessProbe`: «check access» is a one-off check and must really ask Google for a token each time.
- The `no-store` rule for pages, responses and exports (`trebovaniya.md` section 8, v85), which stays as it is;
  static interface files already keep normal caching there.

---

# Security constraints

Binding on the Specification; the token is a new thing held by the process.

- The token lives only in the process's memory and only until it expires: never in the database, a file, a log or a
  response. A refused or failed token request is never remembered as a token.
- The service-account key stays as it is today: read from the secret store when it is needed to get a token, and not
  kept between calls (SC-7, US-011 FR-016). What is reused is the short-lived token, not the key.
- A token is reused only for the identical technical account and scope set. Each read keeps its own minimal read-only
  scopes, and no scope is added (Hard Stops: every scope is read-only).
- The new log line holds neither the exception message nor anything from Google's response or the callback address:
  only a value from the closed list and the existing request id (SC-10).

---

# Acceptance Criteria

## AC-001 One token for many reads

**Given** a fake Google token endpoint and several consecutive reads for the same technical account and scopes

**When** the reads run within the token's validity

**Then** the token endpoint is called once, and every read carries that token.

## AC-002 A different account or scope set gets its own token

**Given** reads with a different technical account, or with a different scope set

**Then** a token issued for one is never sent for the other.

## AC-003 Expiry

**Given** a token at or near its expiry, by the test's clock

**When** the next read runs

**Then** a new token is requested before the read is sent; a read never goes out with an expired token.

## AC-004 Concurrent reads

**Given** several reads running at the same time with no valid token

**Then** they all succeed, none receives another account's token, and the number of token requests stays small. The
Specification sets the exact bound.

## AC-005 Failures are not remembered, and their diagnosis is unchanged

**Given** the token endpoint refuses or is unavailable

**When** a read runs, and another runs after the endpoint recovers

**Then** the first ends with the same failure kind and `SyncDiagnosis` as today, and the second requests a new token
and succeeds.

## AC-006 The key and the token stay out of sight

**Given** the code and a run with a token

**Then** the key is not held between token requests, and neither the token nor the key appears in a log line, the
database or a response.

## AC-007 A failed Google sign-in says why

**Given** a Google callback failing for each kind in the closed list (the Specification fixes it; at least: correlation
failed, Google returned an error or refusal, token exchange failed, other)

**When** the callback is handled

**Then** one Warning with that kind and the request id is written; the message text of the failure, the email and the
query string are not. What the user sees and the audit row are as before.

## AC-008 Event ids are distinct

**Given** the events `ReadOnlyWriteRefused` and `RetentionPurgeStarted`

**Then** they have different numeric ids, both names are unchanged, and neither new id collides with another event in
the solution.

## AC-009 No warning 10103

**Given** the workspace connection is read, for display and for update

**Then** EF Core raises no `FirstWithoutOrderByAndFilterWarning`, and the same single row is returned as before.

## AC-010 Versioned static files

**Given** any page of either host, signed in or not, including the error page

**Then** every reference to the host's own CSS or JavaScript carries the content version, and changing a file changes
the version. No inline code appears (`trebovaniya.md` section 8), and the file still loads under the page's CSP.

## AC-011 Nothing else changed

**Given** the translation files, the pages, the audit events and the Google scopes

**Then** none has changed, and no NuGet package is added.

---

# Open Decisions

None. The Owner's decision of 2026-11-08 excludes P-7 b and every text change.

---

# Notes

- **Token request per read today:** `GoogleClassroomReader.CreateService` builds a new `ServiceAccountCredential`
  through `Credential(...)`, lines 464 and 566. The reader is a singleton (`InstallationServices.cs:132`), and there is
  no "run" object at the port, so the reuse belongs to the reader and is keyed by account and scopes.
- **Disposal:** each read disposes its `ClassroomService`, and its HTTP client factory wraps the retry handler. A reused
  token must not depend on a disposed client. Token requests keep the retry behaviour they have today.
- **P-7 is unproven.** This Story removes a wasteful pattern and the suspected trigger; it does not claim to fix the
  rehearsal failure. A rerun of the same sync did not repeat it.
- **Sign-in failure kinds:** a correlation failure and a token failure can both arrive as
  `AuthenticationFailureException`, so the exception type alone does not separate them. The Specification states how
  they are told apart, without writing the message itself to the log.
- **Layouts:** a search for `~/` references found only 8 in six views, none in `_Layout*.cshtml`. The Specification
  lists every real reference, in both hosts, whatever form its path takes. `InlineCodeRuleTests` already accepts
  `asp-append-version`.
- **P-2:** tests look these events up by name, so changing the number is safe; `RefusalLog.cs:13` is the one place to
  change and `5201` stays with the retention series 5201…5205.
- **Manual check, on the server:** after the first kit update, a page shows the new styles without Ctrl+F5.
- No new endpoint or schema is expected: API and database design should be empty deltas.
