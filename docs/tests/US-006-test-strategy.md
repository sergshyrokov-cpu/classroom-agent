---
artifact_type: test_strategy
story: US-006
version: 1
status: DRAFT
created_at: 2026-09-19T09:40:00Z
updated_at: 2026-09-19T09:40:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-006-control-plane-push.md
    version: null
  - path: docs/specifications/US-006-spec.md
    version: 2
  - path: docs/decisions/US-006-open-decisions.md
    version: 2
  - path: docs/designs/api/US-006-api-design.md
    version: 1
  - path: docs/designs/api/US-006-openapi.yaml
    version: 1
  - path: docs/designs/database/US-006-db-design.md
    version: 1
  - path: docs/designs/database/US-006-entity-model.md
    version: 1
  - path: trebovaniya.md
    version: 77
supersedes: null
---

# US-006 Test Strategy — Control Plane push on status change

Traceability: `docs/tests/US-006-ac-test-matrix.md`. Execution evidence:
`docs/evidence/US-006-test-generation-report.md`.

## 1. Scope

Four surfaces, two hosts (api-design §1):

1. **Control Plane pages** — the registration form's optional push address, the
   detail page's row and missing-address warning, and the push address page
   (GET/POST) that sets, changes and clears it (AC-002 … AC-004, AC-015).
2. **Control Plane push sender** — what a suspend or resume that changed the
   status hands to the background sender, and how the sender attempts, retries,
   replaces and abandons a push (AC-005 … AC-008).
3. **Installation push receiver** on the private port — validation, identifier
   check, `202`, the triggered check, deferral and the single pending check
   (AC-009 … AC-014).
4. **Installation configuration** — the new required `Hosting:PrivateAddress`
   (AC-001).

Out of scope for tests, because the Story puts it out of scope: read-only
enforcement (US-007), showing push results anywhere, a persistent retry queue,
verifying that a push address belongs to a school, and the DC-2 deployment check
that the private port does not answer on the public address.

## 2. Test levels (TC-1, TC-2, TC-5)

| Level | Used for | Host |
|---|---|---|
| Integration (`WebApplicationFactory<Program>` + Testcontainers PostgreSQL) | every page, every endpoint, the sender, the coordinator, configuration refusal, logs | both |
| Contract | the wire body of the outbound push and of the receiver, status codes and `ServiceOutcome` bodies | both |
| Security | Owner-only policies, antiforgery, anonymous-endpoint enumeration, private-port isolation, output encoding, log and audit hygiene | both |
| Persistence | the `push_address` column, its check constraint, the absence of a unique index, the audit row and its transaction | Control Plane |

No unit-level test class is added: every rule of this Story is observable
through a host, and a use-case seam that could be unit-tested (US-005's
`CheckLegitimacyUseCase`) is unchanged. The EF Core InMemory provider and SQLite
stay forbidden (TC-2).

## 3. Substitution seams and fixtures (TC-4)

The implementation must keep these seams, or the tests below cannot observe the
behaviour without a real network:

| Seam | Constant / type | Used by |
|---|---|---|
| The Control Plane's push HTTP client is a **named** client `status-push`; its primary handler is replaced | `PushTestData.HttpClientName` → `TestInfrastructure.PushClientStub` | sender tests |
| The Control Plane runs on an injected `TimeProvider`, and the retry pauses **and** the 10 s attempt timeout are measured on it | `ControlPlaneTestHost.StartAsync(manualTime:)` → `ManualTimeProvider` | sender tests |
| The Control Plane test host may add or replace services | `ControlPlaneTestHost.StartAsync(configureServices:)` | sender tests |
| The installation's `IControlPlaneClient` is substituted | `FakeControlPlaneClient` (US-005) | receiver and coordinator tests |
| The installation's private port is addressed through `TestServer.SendAsync` with `Connection.LocalPort` | `InstallationTestHost.SendPrivateAsync` / `SendPublicAsync`, now with a body | receiver tests |
| Log events are read from the rolling Serilog file as compact JSON | `ReadLogEventsAsync` on both hosts, `LogEvent` | logging tests |

Fixed names the tests assert and the implementation must use — renaming one is a
contract change, not a refactoring:

- paths `/service/v1/status-pushes` and `/installations/{id}/push-address`;
- form field and JSON property `pushAddress` / `installationId`;
- audit action code `installation_push_address_changed`;
- translation keys `Installation.PushAddress.*` (api-design §7);
- log event names `StatusPushDelivered`, `StatusPushAttemptFailed`,
  `StatusPushAbandoned`, `StatusPushRefused`, `StatusPushNoAddress`,
  `StatusPushAccepted`, `StatusPushForeignInstallation`, `StatusPushRejected`,
  `PendingPushCheckStarted`, with the properties of api-design §9;
- setting key `Hosting:PrivateAddress`.

All fixture data is synthetic (`PushTestData`, `InstallationTestData`); no test
calls Google, a real installation or a real Control Plane.

## 4. Scenarios

### 4.1 Positive

- Registration with and without an address; the form offers the field.
- Set, change and clear on the push address page, at `active` and `suspended`.
- Canonical storage: trimming, a single trailing `/`, lower-cased host, bracketed
  IPv6.
- Detail page with an address (no warning) and without one (warning + "not set"),
  both linking to the push address page.
- Suspend and resume with an address → exactly one push with `installationId`
  only, to the stored address, `POST`, JSON, no cookies or credentials.
- `202` → delivered after one attempt.
- Receiver: own id → `202` at once and one check; unknown JSON properties ignored;
  read-only mode behaves identically.
- Coordinator: a push more than a minute after the last push-triggered check
  starts a check at once; after it the schedule is 6 h (success) or 15 min
  (failure) from its completion.
- Startup with each valid `Hosting:PrivateAddress` form (IPv4, IPv6 bracketed and
  bare, `*`).

### 4.2 Negative

- Every VR-001 rule, on the push address page and at registration, with the
  message key of the first failing rule; nothing stored, nothing audited.
- Unknown installation on the push address page (GET and POST) → `404`.
- Suspend/resume that changed nothing → no push; rename, client ID, push address
  change, `AllowedAdmin` add and revoke → no push.
- No address on the school → no push, one `StatusPushNoAddress` warning.
- `404` from the installation → refused, no retry.
- `500`, `400`, `200`, `302`, connection failure, timeout → attempt failed,
  retried, then abandoned after the fourth attempt.
- Receiver: another installation's UUID → `404` `unknown_installation`, no check;
  missing, non-JSON, malformed, non-UUID body → `400` `invalid_request`;
  > 4096 bytes → `413`; a method other than POST → not `202`.
- Receiver on the public port → `404`, also with forged `Host` and
  `X-Forwarded-Host`.
- Startup with a missing, empty, host-name, CIDR, `**`, `+`, out-of-range or
  address-with-port `Hosting:PrivateAddress` → the host does not start.

### 4.3 Boundary

- Address length 255 / 256 characters; port `1`, `65535`, `0`, `70000`, absent.
- Body of exactly the allowed size versus oversized (4 KB, spec I-12).
- The one-minute limit: a push at `+60 s` starts a check, a push inside the
  minute defers; the pending check starts its own minute.
- Retry pauses measured exactly: attempts at `t`, `t+5 s`, `t+35 s`, `t+155 s`.
- Six-hour and fifteen-minute reschedule measured from the pushed check's
  completion.

### 4.4 Validation

VR-001 rule order (length → format → scheme → extra → host → port) is asserted by
one theory row per rule; the refused form refills the typed value and shows the
translated message. VR-002 is asserted through the receiver's status codes and
`ServiceOutcome` bodies. VR-003 is asserted through startup refusal.

### 4.5 Security (TC-5, spec §7)

- Push address page GET and POST: anonymous → `302 /sign-in`; without the `Owner`
  role → `403`; POST without an antiforgery token → `400` `Error.PageExpired`;
  before setup → `302 /setup`. Each refusal changes nothing.
- The two new Control Plane routes join the installation-route enumeration, which
  asserts that none of them is anonymous and none accepts `PUT`/`PATCH`/`DELETE`.
- The installation's routed endpoints become exactly liveness, readiness and the
  receiver; the receiver is the only state-changing endpoint, POST only,
  anonymous and carrying antiforgery-exemption metadata (S-01).
- Output encoding: a refused value containing markup is never echoed unencoded.
- Log hygiene: no log line of either host carries the address, the domain, the
  client ID, a body or a received foreign id.
- Audit hygiene: no row carries an address; delivery is never audited; audit rows
  stay non-updatable.

### 4.6 Persistence (db-design §7)

Column shape and nullability, no unique index, two installations sharing one
address, the check constraint accepting the canonical forms and rejecting
`https://`, missing port, trailing slash, path, upper-case host, leading-zero
port and the empty string, the immutability trigger still rejecting a domain
change while accepting a `push_address` change, exactly one audit row per real
change written with it, and `updated_at` untouched by an unchanged submission.

## 5. Required fixtures

New: `PushTestData` (paths, field names, canonical addresses, translation keys,
the `Hosting:PrivateAddress` key), `PushClientStub` (scripted HTTP answers and
recorded attempts), `PushHostExtensions` (register with an address, submit the
form, read and set the column).

Changed: `ControlPlaneFactory` and `ControlPlaneTestHost` (optional
`configureServices` and manual clock; `ReadLogEventsAsync`),
`InstallationTestHost` (request bodies on the private and public ports;
`Hosting:PrivateAddress` in the default settings), `InstallationConfigurationKeys`
(`PrivateAddressValue`), `Html.ElementText`.

## 6. Excluded scenarios and known limitations

| Not tested | Why |
|---|---|
| That Kestrel really binds the private endpoint to the configured address and to no other | `WebApplicationFactory` uses `TestServer`; there is no listening socket to inspect. The setting's validation and refusal are tested; the binding itself is the DC-2 deployment check (spec §10). The tests do assert that every invalid value refuses startup, which is the part code owns. |
| A real TCP connection from the Control Plane to a real installation | TC-4: both sides are substituted at their ports; the wire shape is asserted on the recorded request instead. |
| The real `StatusPushClient` against a local test HTTP server (spec §9 "Testability") | Would add a listening socket to the suite for one classification table that `PushClientStub` already drives through the sender. Recorded as a non-blocking finding: if the implementor puts the classification inside the client rather than the sender, a client-level test must be added in IMPLEMENTATION. |
| Concurrency of two Owner sessions changing the address at once (db-design §4.2) | The outcome is "last value wins, two audit rows" with no invariant to break; no assertion would fail on a wrong implementation. |
| A push during Control Plane shutdown beyond "pending retries are lost" | Nothing is persisted, so there is nothing else to observe. |

## 7. Open Decisions affecting testing

None open. OD-001 is RESOLVED (option 2, one pending check); the pending-check
behaviour it fixed is covered by `PushCheckCoordinationTests` and the deferred
logging test.
