---
id: US-064
epic: EPIC-6
title: A disabled Dean's open session is refused on the next request
slug: disabled-dean-session-refused-on-next-request
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-10-27 from trebovaniya.md v103 (`5cea77b`,
# §2 «Отключение действует сразу»; derived BR-014, SC-2 in `bcc5447`). Item 4 of
# the v103 agenda. The behaviour is built (US-012 FR-007, US-049 per-request
# check); this Story adds the end-to-end proof v103 now requires. Before the pilot.
---

# User Story

As an **Admin**

I want a Dean I disable to lose access at once, even with a browser tab open

So that disabling an account is a real stop — the Dean cannot open the next page
or download one more export — and not a promise that holds only after the session
expires.

---

# Business Value

v103 states that disabling takes effect on the next request (BR-014, SC-2). The
code does it — `AppUser.Disable` rotates the security stamp and the session cookie
is checked against the stamp and the disabled state on every request — but no test
replays the old cookie of a disabled Dean over HTTP. The same proof exists for
sign-out (`InstallationSignOutTests.ThePreviousCookieReplayed_NoLongerAuthenticates`);
for disabling it is unit-level only. A regression here — for example, a
`SecurityStampValidator` interval brought back by a later change (SC-2 calls that a
finding) — would pass the suite silently.

---

# Scope

**In scope:**

- an end-to-end test through the real HTTP pipeline (`WebApplicationFactory`,
  real PostgreSQL via Testcontainers, TC-2): a Dean signs in, the Admin disables
  the account through the Dean-accounts page, the Dean's cookie captured before
  the disabling is replayed;
- the replayed cookie is refused on a page GET (the journal) — sent to the sign-in
  page — and on the journal export (`POST /api/v1/exports/journal-xlsx`, the
  Data Plane's API endpoints are the exports) — the unauthenticated refusal of the
  API conventions, no file;
- re-enabling the account does not revive the old cookie (the stamp was rotated);
  the Dean signs in again with the password and works;
- a second Dean's session is not affected;
- if a test fails, the fix in production code is in scope (that would be a defect
  against BR-014), and the implementation report says so.

**Out of scope:**

- a request already in progress at the moment of disabling — v103 lets it complete;
  not tested (a timing test with no stable assertion);
- revocation of an Admin in `AllowedAdmin` — a known limitation in v103 (§2,
  BR-012, SC-3): the session ends on its own expiry; see OD-001;
- any UI change, any message on the sign-in page beyond what exists today.

---

# Acceptance Criteria

## AC-001 The old cookie of a disabled Dean opens no page

**Given** a Dean signed in, with their session cookie captured

**When** the Admin disables the account and the cookie is replayed on the journal
page

**Then** the response sends the Dean to the sign-in page, and no journal content
is returned.

## AC-002 The old cookie downloads no export

**Given** the situation of AC-001

**When** the cookie is replayed on a journal export (with a valid antiforgery
token taken before the disabling)

**Then** the response is the unauthenticated refusal of the API conventions (status
and error body), no file is returned, and no export audit event is written.

## AC-003 Re-enabling does not revive the old cookie

**Given** the account disabled and then enabled again by the Admin

**When** the old cookie is replayed

**Then** it is still refused; after signing in again with the password, the Dean
opens the journal.

## AC-004 Another Dean is not affected

**Given** two Deans signed in

**When** the Admin disables one

**Then** the other's cookie still opens the journal.

---

# Open Decisions

Resolved by the Owner on 2026-10-27 before activation.

## OD-001 Pin the Admin-revocation limitation with a test

v103 records that revoking an Admin in `AllowedAdmin` does not end their open
session (known limitation). A test could assert that the Admin's session still
works after revocation.

- **a) (recommended)** — no test: a limitation is something we accept, not a
  behaviour we promise; a test would turn it into a requirement and fail the day
  someone improves it. The Story's notes record that it was looked at.
- b) — a test asserting the current behaviour, renamed and inverted when the
  limitation is lifted.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»).

---

# Notes

- **Code today:** `AppUser.Disable` (`AppUser.cs:132`) rotates the stamp;
  `AccountSessionService` (~28) accepts a session only when the account is not
  disabled and the stamp matches; the per-request check is in
  `InstallationSession.cs` (~100). The sign-out replay test is the model:
  `tests/ClassroomAgent.Tests/Web/Security/InstallationSignOutTests.cs:36`;
  disabling through HTTP — `DeanAccountsPageTests` (`SetDeanStateAsync`).
- **Expected to be test-only.** A red test here is a production defect, not a red
  phase: TEST_WRITING expects the tests to pass already (unlike the usual red
  phase — see the "vacuous tests" caution: each test must also be shown able to
  fail, e.g. by asserting the cookie works *before* disabling).
- Expected stages: SPECIFICATION (short), API_DESIGN and DB_DESIGN NOT_APPLICABLE.
- No new NuGet package, no new requirements version.
