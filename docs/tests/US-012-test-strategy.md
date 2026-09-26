---
artifact_type: test_strategy
story: US-012
version: 1
status: DRAFT
created_at: 2026-09-26T17:38:32Z
updated_at: 2026-09-26T17:38:32Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-012-manage-dean-accounts.md
    version: null
  - path: docs/specifications/US-012-spec.md
    version: 1
  - path: docs/designs/api/US-012-api-design.md
    version: 1
  - path: docs/designs/api/US-012-openapi.yaml
    version: 1
  - path: docs/designs/database/US-012-db-design.md
    version: 1
  - path: docs/designs/database/US-012-entity-model.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-012 Test Strategy — Create and manage Dean accounts

## 1. Scope

Two surfaces and the rules behind them: the Admin's four management actions
(BR-014) and the Dean's own sign-in, forced temporary-password change and
voluntary change (SC-2). Plus the audit trail of all of it (SC-11), the
translations (NFR-073) and the one schema change (db-design §3).

This is the Story that finally makes a **real Dean session** possible, so it
also carries the test that three security reviews have been waiting for: the
HTTP-level forbidden-role assertion on the three Admin-only settings pages
(AC-014, carried US-010 F-1 → US-011 F-1).

Out of scope for tests, because out of scope for the Story: deleting an account,
changing an account's email, choosing a language, the retention purge, anything
Google.

## 2. Test levels

| Level | Where | What it proves here |
|---|---|---|
| Unit / Application | `ClassroomAgent.Tests.Application.UseCases` | the password policy, the six-step sequence, the lockout arithmetic, read-only refusals in the layer that enforces them (AD-6, TC-5) |
| Integration (host + real PostgreSQL) | `ClassroomAgent.Tests.Web.Pages`, `.Web.Security`, `.Web.Persistence` | status codes and redirects of the contract, authorization, the check constraint, the audit rows, the migration count |
| Contract | inside the host tests | every operation of `US-012-openapi.yaml`: its statuses, its `Location`, its re-rendered form (TC-3) |
| Security | `.Web.Security` + the Application tests | indistinguishable refusals, no password anywhere in a response, the SC-4 anonymous list, antiforgery, the role boundary |

No test calls Google — nothing in this Story calls Google at all (TC-4, spec
S-14). Integration tests use Testcontainers PostgreSQL, never InMemory (TC-2).
Time comes from `ManualTimeProvider`, so the 15-minute lockout is proved without
waiting (FR-013).

## 3. Positive scenarios

| # | Scenario | Level |
|---|---|---|
| P-1 | An Admin opens the screen and sees every Dean, active and disabled, with the four OD-003 columns | integration |
| P-2 | An Admin creates an account: row created with role Dean, sign-in method password, hash stored, temporary mark set, PRG back to the screen | integration + unit |
| P-3 | An Admin disables an account; the row is disabled and the security stamp changes | unit + integration |
| P-4 | An Admin re-enables it; only the state changes | unit |
| P-5 | An Admin resets the password: new hash, temporary mark, counter zeroed, lockout cleared, stamp rotated | unit |
| P-6 | A Dean signs in with a correct, non-temporary password and lands on `/` | integration |
| P-7 | A Dean with a temporary password is sent to the forced change form, changes it, and is then signed in | integration |
| P-8 | A Dean changes their own password with the current one | unit + integration |
| P-9 | The Dean's own password change succeeds **in read-only mode** (BR-026) | unit |
| P-10 | A Dean keeps `200` on the legitimacy-status view both roles may see | integration |

## 4. Negative scenarios

| # | Scenario | Level |
|---|---|---|
| N-1 | Creation refused: address in another domain | unit + integration |
| N-2 | Creation refused: free-form string, empty local part | unit |
| N-3 | Creation refused: email already used by a Dean, by a **disabled** Dean, and by an **Admin** — the three answer alike | unit |
| N-4 | Every management action refused in read-only mode, with no row written and one audit row (AC-009) | unit |
| N-5 | A management action on an id that is not a Dean, and on an id that matches nothing → `404`, indistinguishable | integration |
| N-6 | Disabling an already-disabled account (stale screen) → `409`, nothing written | integration |
| N-7 | A reset does **not** re-enable a disabled account | unit |
| N-8 | Re-enabling does **not** clear a lockout and does **not** set a temporary password | unit |
| N-9 | Sign-in refused at each of steps 1–4, each with its own audit category and counter behaviour | unit |
| N-10 | A Dean is `403` on all three settings pages, on every method — AC-014, over HTTP, with a real session | integration |
| N-11 | An Admin is `403` on `/account/password` — an Admin has no password (S-07) | integration |
| N-12 | Any form without an antiforgery token → `400`, the anonymous `POST /sign-in` included | integration |
| N-13 | The forced change page is unreachable without the restricted session, and a Dean in that state cannot reach any other page | integration |

## 5. Boundary scenarios

| # | Scenario |
|---|---|
| B-1 | Password length 14 refused, 15 accepted, 128 accepted, 129 refused — counted in **characters**, so a string of 15 non-ASCII characters is accepted and 14 is not |
| B-2 | A password of 15 spaces is accepted (no composition rule, spaces not trimmed) |
| B-3 | The containment rule applies to a local part of exactly 4 characters and **not** to one of 3, which is compared for equality only (SC-2 v65) |
| B-4 | Case-insensitivity: the password may not equal the login in any case combination |
| B-5 | The 4th failed attempt does not lock; the 5th does; the lock lifts at exactly 15 minutes, not at 14:59 |
| B-6 | A successful sign-in after 4 failures resets the counter to zero |
| B-7 | `Dean@school.example` and `dean@school.example` are the same login for uniqueness and for sign-in |

## 6. Validation scenarios

Each rule of spec §6 gets its own test: VR-001 (email required, shape, domain,
uniqueness), VR-002 (the policy, on all four forms that set a password), VR-003
(new ≠ temporary at the forced change; **no** rule against new = current at the
voluntary one — asserted as accepted, so a later implementer cannot quietly add
it), VR-004 (antiforgery on every form), VR-005 (target must be a Dean; state
transitions), VR-006 (nothing rejected is echoed).

## 7. Security scenarios

| # | Scenario | Source |
|---|---|---|
| S-1 | Steps 1, 2 and 3 of the sequence are identical over HTTP: same status, same `Location`, same message key | SC-2, spec S-05, I-6 |
| S-2 | "Account disabled" appears **only** with the correct password and no lockout; a wrong password on a disabled account gets the common message | SC-2 v65 |
| S-3 | Step 2 does not verify the password — a locked-out account with the correct password is refused and the counter does not move | SC-2 v66 |
| S-4 | Step 4 neither increments nor resets the counter | SC-2 v65 |
| S-5 | No response, page or log carries a password, a hash, a security stamp, the counter or the lockout end | SC-10, spec S-10 |
| S-6 | The typed login of an unknown account appears in no audit row and no log | SC-11 |
| S-7 | An Admin row can never carry a temporary password — rejected by PostgreSQL itself | db-design §3.2 |
| S-8 | No `AppUser` is ever deleted: no repository method, no endpoint, no UI control | BR-014, AC-008 |
| S-9 | The SC-4 anonymous list is unchanged in **pages**; `POST /sign-in` is anonymous on the already-anonymous `sign-in` pattern | SC-4 |
| S-10 | Disabling, resetting and changing a password each rotate the security stamp | spec FR-019 |
| S-11 | Read-only mode is refused in the Application layer, with the guard consulted before any repository call | AD-6, TC-5 |

## 8. Persistence scenarios

| # | Scenario |
|---|---|
| D-1 | `password_is_temporary` exists, is `NOT NULL`, defaults to `false` |
| D-2 | `ck_app_user_password_temporary` rejects an Admin row with the flag set — at the database, not only in the domain |
| D-3 | The unique index still refuses a second account on the same normalized email |
| D-4 | Installation migrations: **five**, `_AddDeanAccounts` last; tables still five (an expected change to `AppUserMigrationTests`, db-design §8) |
| D-5 | No new table, no new index on `app_user`, no new column on `audit_event` |
| D-6 | Every refusal commits **exactly one** audit row and nothing else (carried US-009 F-2) |
| D-7 | The nine row shapes of db-design §4.4, each asserted on its own path |

## 9. Translation scenarios

Every new key resolves in both `uk` and `en`, and the two files hold the same
key set (TC-8). The keys the screens need are listed in the test data class, so
a missing one fails at the test, not in the browser. The common refusal message
and the "account disabled" message are asserted to be **different keys** with
**different text** — and steps 1–3 to use the same one.

## 10. Required fixtures

- `PostgreSqlFixture` — the shared Testcontainers database (TC-2).
- `InstallationTestHost` — the host, with `InsertAppUserAsync` extended by
  the new `password_is_temporary` column and by a helper that seeds a Dean with
  a known password hash.
- `ManualTimeProvider` — the lockout clock.
- `FormClient` — browser-like client that keeps cookies and the antiforgery
  token; the only way the new forms are exercised.
- `DeanAccountTestData` — paths, policy names, form field names, translation
  keys and the synthetic passwords, in one place so the tests and the future
  implementation cannot drift (the US-009 / US-011 pattern).

Synthetic data only: the domain is `school.example`, the accounts are
`dean@school.example` and friends, the passwords are obviously fake
(`correct horse battery staple 12`). No real school, no real person (TC-4).

## 11. Excluded scenarios, with justification

| Excluded | Why |
|---|---|
| The real elapsed 15 minutes | the lockout is proved by advancing `ManualTimeProvider`; a waiting test would be slow and flaky (TC-1) |
| The hasher's own algorithm strength | `IPasswordHasher<AppUser>` is a framework primitive; the tests assert that a hash is stored and verifies, not how it is computed |
| Brute-force timing analysis of steps 1–3 | the tests assert identical status, `Location` and message key; wall-clock timing equality is not assertable deterministically in this suite (known limitation) |
| Concurrent creation of the same email by two Admins | the unique index is asserted directly (D-3); the two-request race is inherently timing-dependent |
| A Dean's working screens (courses, journals, exports) | EPIC-1, EPIC-3, EPIC-4 — they do not exist yet |

## 12. Known limitations

- **Timing equality of the refusal paths is not asserted** (§11). Step 2 skips
  the password verification by design, which is observable in principle as a
  faster answer; SC-2 accepts that trade because the alternative — verifying a
  password during a lockout — is worse.
- **The forced-change session** is asserted through its observable behaviour
  (which pages it may reach), not through the claim that carries it: the claim
  is an implementation detail the tests deliberately do not pin.
- The suite cannot prove that **no future** code deletes an account; D-8 asserts
  the absence of a delete on the repository surface and the endpoints as they
  are at the end of this Story.

## 13. Open Decisions affecting testing

None. OD-001 … OD-004 were resolved before activation and each is reflected in a
test: OD-001 by the sign-in tests existing here at all, OD-002 by the hash being
produced through `IPasswordHasher<AppUser>`, OD-003 by the four asserted list
columns, OD-004 by the absence of any edit-email or delete path.
