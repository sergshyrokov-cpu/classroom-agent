---
artifact_type: api_design
story: US-006
version: 1
status: DRAFT
created_at: 2026-09-17T15:39:33Z
updated_at: 2026-09-17T15:39:33Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-006-spec.md
    version: 2
  - path: docs/decisions/US-006-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 77
  - path: docs/designs/api/US-005-openapi.yaml
    version: 1
supersedes: null
---

# US-006 API Design — Control Plane push on status change

Contract: `docs/designs/api/US-006-openapi.yaml` (`info.version: "1"`).

## 1. Rationale

- **Four surfaces, two hosts.**
  1. Control Plane **pages** — the registration form gains an optional push
     address, the detail page shows it with a warning when missing, and a new push
     address page sets, changes or clears it (spec FR-002 … FR-004).
  2. Control Plane **suspend / resume** POSTs of US-004 — unchanged responses, a new
     side effect: the push is handed to a background sender (FR-005).
  3. The **outbound push** from the Control Plane to the installation (FR-006,
     FR-007) — not an endpoint of the Control Plane, but its client behaviour is
     part of the contract both sides test against.
  4. The installation's **private port** — the push receiver (FR-009, FR-010).
- **No `/api/v1` endpoint.** The receiver belongs to the service channel (API-7,
  DC-12), like the US-005 legitimacy check.
- **Builds on US-001 … US-005** unchanged: Control Plane host-wide rules, `{id}`
  guid route, HTML encoding, the US-004 suspension flow, the US-005 wire conventions
  (`ServiceChannel.JsonOptions`, `ServiceOutcome`), private route group and settings
  reader.

## 2. Routes

| Host / port | Method | Path | Anonymous | Antiforgery | Purpose | Spec |
|---|---|---|---|---|---|---|
| Control Plane | GET | `/installations/new` (changed) | no, `Owner` | — | form gains `pushAddress` | FR-002 |
| Control Plane | POST | `/installations` (changed) | no, `Owner` | required | registers with optional push address | FR-002, FR-003 |
| Control Plane | GET | `/installations/{id}` (changed) | no, `Owner` | — | push address row, warning, link | FR-004 |
| Control Plane | GET | `/installations/{id}/push-address` | no, `Owner` | — | push address form | FR-002 |
| Control Plane | POST | `/installations/{id}/push-address` | no, `Owner` | required | set / change / clear | FR-002, FR-003 |
| Control Plane | POST | `/installations/{id}/suspension`, `/resumption` (changed) | no, `Owner` | required | unchanged response; push on change | FR-005 |
| Installation, private port | POST | `/service/v1/status-pushes` | yes (SC-4 "Status-change push receiver") | exempt (SC-4) | push receiver | FR-009, FR-010 |
| Control Plane → installation | POST | `{pushAddress}/service/v1/status-pushes` | outbound | — | push delivery | FR-006 |

- **Why `/service/v1/status-pushes`.** Same channel convention as US-005
  (`/service/v1/legitimacy-checks`): path-versioned so a breaking change can add
  `v2`; plural noun, each POST submits one push. The path lives in
  `ServiceChannel` so sender and receiver cannot drift.
- **Why `push-address`.** Kebab-case sub-resource like `name` and `client-id`
  (US-002).

## 3. Wire contract (`ClassroomAgent.Contracts`)

JSON, `application/json; charset=utf-8`, camelCase, unknown properties ignored
(DC-12). The contract version stays `1`: the addition is a new type on a new path,
nothing existing changes.

### StatusPushRequest

| Property | Type | Required | Rule (VR-002) |
|---|---|---|---|
| `installationId` | string, UUID | yes | canonical UUID |

Nothing else (SC-12). Constant: `ServiceChannel.StatusPushPath =
"service/v1/status-pushes"`.

Non-`202` bodies reuse `ServiceOutcome` (US-005): `invalid_request` with `400`,
`unknown_installation` with `404`. The Control Plane never reads them (S-10); they
exist for diagnostics and tests.

## 4. POST /service/v1/status-pushes (installation, private port)

Bound to the private route group: its filter compares
`HttpContext.Connection.LocalPort` with `Hosting:PrivatePort` and answers `404` on
any other port; `Host` / `X-Forwarded-Host` play no part (DC-6).

| Step | Check | Result | Effect | Log |
|---|---|---|---|---|
| 0 | request on the private port | else `404`, empty body | nothing | — |
| 1 | body ≤ 4096 bytes (spec I-12) | else `413`, empty body | nothing | `StatusPushRejected` (`Error`, category `too_large`) |
| 2 | JSON content type; body parses into `StatusPushRequest`; `installationId` a UUID | else `400` `{"outcome":"invalid_request"}` — missing body, non-JSON content type, malformed JSON, missing or non-UUID `installationId` | nothing | `StatusPushRejected` (`Error`, category `invalid`), never the body |
| 3 | `installationId` equals `Installation:Id` | else `404` `{"outcome":"unknown_installation"}` | nothing | `StatusPushForeignInstallation` (`Warning`), no id |
| 4 | coordinator `Request()` (§5) | `202`, empty body | `Started` → check runs in background; `Deferred` → one pending check remembered | `Started`: `StatusPushAccepted` (`Information`); `Deferred`: none |

- `202` is returned without waiting for the check.
- Methods other than POST on the path: `404` or `405` as routing resolves it; tests
  assert "not 202, no check".
- Anonymous, no antiforgery (the installation host has no antiforgery middleware
  yet; the endpoint carries the exemption metadata so US-008's global filter keeps
  it exempt). No audit.
- Nothing is written by the receiver; only the triggered check writes
  `LegitimacyState` (US-005). Identical in read-only mode (FR-012).
- `500` only for an unhandled exception, empty body, no check started.

## 5. Push check coordination (installation)

A singleton `PushCheckCoordinator` in `ClassroomAgent.Web.BackgroundServices`
shared by the receiver and `LegitimacyCheckBackgroundService` (spec FR-010, v77).
Its behaviour is the contract tests rely on:

| State when a push arrives | `Request()` returns | Effect |
|---|---|---|
| no check running, and no push-triggered check started within the last minute | `Started` | a check starts now; the minute starts now |
| a check (scheduled or push-triggered) is running | `Deferred` | pending flag set (idempotent) |
| no check running, but a push-triggered check started less than 1 minute ago | `Deferred` | pending flag set (idempotent) |

- **Pending check start:** when the flag is set, it starts at
  `max(completion of the running check, start of previous push-triggered check + 1 min)`.
  Starting it clears the flag, starts a new minute, and logs
  `PendingPushCheckStarted` (`Information`).
- A scheduled check completing does not clear the flag.
- **Schedule reset:** after any push-triggered check completes, the next scheduled
  check is due 6 h (success) or 15 min (failure) after its completion; the previous
  scheduled wait is discarded.
- The decision and the start are atomic; never two checks at once.
- All times from the injected `TimeProvider`; "within the last minute" is strict:
  exactly 60 s after the previous start a push starts a check.
- No state survives a restart (the check at startup covers it).

## 6. Push delivery (Control Plane → installation)

`ControlPlane.Push` holds the sender. Tests of both sides rely on this
classification.

**Request.** `POST {pushAddress}/service/v1/status-pushes`, body
`{"installationId":"<Installation.Identifier>"}`, `Content-Type:
application/json; charset=utf-8`. Plain HTTP. No cookies, no credentials, no
custom headers. Redirects not followed.

| Attempt result | Classification | Next |
|---|---|---|
| `202` within 10 s | `Delivered` | stop; `StatusPushDelivered` (`Information`) |
| `404` within 10 s | `Refused` | stop, no retry; `StatusPushRefused` (`Warning`) |
| any other status within 10 s (`2xx` other than `202`, `3xx`, `4xx`, `5xx`) | `UnexpectedStatus` + code | retry; `StatusPushAttemptFailed` (`Warning`) |
| connection refused, DNS failure, reset | `ConnectionFailed` | retry; `StatusPushAttemptFailed` |
| no response status within 10 s from the attempt start | `Timeout` | retry; `StatusPushAttemptFailed` |

- The 10 s cover connect and response headers (spec I-7); the response body is
  never read.
- **Retry pauses:** 5 s, 30 s, 120 s, each from the end of the failed attempt;
  attempts are numbered 1 … 4. After attempt 4 fails: `StatusPushAbandoned`
  (`Warning`).
- **Replacement:** one in-progress push per `Installation`. Enqueuing for an
  installation that has one cancels it (attempt in flight abandoned, its outcome not
  logged) and starts the new one at attempt 1 (spec I-9). Different installations
  run concurrently.
- **Shutdown:** stopping the host cancels all pushes without waiting; nothing
  persisted.
- **Exceptions:** an unexpected exception inside a push logs
  `StatusPushSenderException` (`Error`, exception type only) and ends that push; the
  sender keeps serving others.
- The installation's identifier (UUID) is sent; the internal numeric id is only
  logged.

**Hand-off (spec FR-005).** `InstallationStatusService.ChangeStatusAsync` reads the
stored push address in the same transaction as the conditional status update; after
commit, on `Changed`:

- address present → `StatusPushDispatcher.Enqueue(installationId, identifier,
  address)` (non-blocking, never throws to the caller; a failure logs
  `StatusPushHandOffFailed`, `Error`);
- address absent → `StatusPushNoAddress` (`Warning`), nothing enqueued.

`Unchanged` and `NotFound` enqueue nothing and log nothing new. The HTTP responses
of `POST …/suspension` and `…/resumption` are exactly US-004's.

## 7. Control Plane pages

### Push address rules (VR-001)

Field name `pushAddress`, optional. Evaluated after trimming; empty or
whitespace-only means "not set". Each failing value reports the first failing rule:

| Order | Rule | Message key |
|---|---|---|
| 1 | length ≤ 255 characters | `Installation.PushAddress.Length` |
| 2 | parses as an absolute URI | `Installation.PushAddress.Format` |
| 3 | scheme `http` (case-insensitive) | `Installation.PushAddress.Scheme` |
| 4 | no user info, query or fragment; path empty or `/` | `Installation.PushAddress.Extra` |
| 5 | host is a DNS name (labels `[A-Za-z0-9-]`, 1–63, no hyphen at a label edge, total ≤ 253), an IPv4 dotted quad, or a bracketed IPv6 literal | `Installation.PushAddress.Host` |
| 6 | port written explicitly, decimal 1–65535 | `Installation.PushAddress.Port` |

**Canonical form:** `http://` + host in lower case (IPv6 in brackets, compressed as
.NET `Uri` renders it) + `:` + port in decimal without leading zeros; no trailing
`/`. Stored and displayed in this form; "unchanged" compares canonical forms
ordinally.

### GET /installations/new, POST /installations (changed)

- The form gains `pushAddress` (empty) with label `Installation.PushAddress.Label`
  and hint `Installation.PushAddress.Hint`.
- `RegisterInstallationRequest` gains `PushAddress` (nullable). Its validation joins
  step 2 of US-002: all failed fields reported at once, all values refilled.
- Step 5 stores the canonical address or null. Audit stays one row,
  `installation_created` (spec I-5). Uniqueness checks of step 4 are unchanged; the
  address is not unique.

### GET /installations/{id} (changed)

Everything of US-002 … US-005 stays. Added after the client ID row:

| State | Rendered |
|---|---|
| address set | label `Installation.PushAddress.Label`; `id="installation-push-address"` with the canonical address (HTML-encoded) |
| not set | label; `id="installation-push-address-none"` with `Installation.PushAddress.NotSet`; and `id="installation-push-address-warning"` with `Installation.PushAddress.MissingWarning` |

A link `id="installation-push-address-change"` (`Installation.PushAddress.Change`) to
`/installations/{id}/push-address` in both states. No push result anywhere.

### GET /installations/{id}/push-address

- `200`: form with `pushAddress` pre-filled with the canonical address (empty when
  not set), the token, the title `Installation.PushAddress.Title`, the hint
  `Installation.PushAddress.Hint` and the note `Installation.PushAddress.ClearNote`
  (empty clears it; without an address status changes reach the school within 6 h).
- `404`: unknown UUID.

### POST /installations/{id}/push-address

| Step | Check | Failure response | Audit |
|---|---|---|---|
| 1 | antiforgery | `400`, error page `Error.PageExpired` | — |
| 2 | installation exists | `404`, error page `Error.NotFound` | — |
| 3 | VR-001 on a non-empty value | `400`, form again, message, value refilled | — |
| 4 | canonical value (null for empty) equals the stored value | `302 /installations/{id}`, nothing written | — |
| 5 | update + audit row in one transaction | — | succeeded, `installation_push_address_changed`, target `installation` |

Success: `302 Location: /installations/{id}`. Any status of the installation. No push
is sent.

### Request / response models

- `ChangeInstallationPushAddressRequest` — `PushAddress` (nullable string). Nothing
  else bound.
- `RegisterInstallationRequest` — adds `PushAddress`.
- `InstallationDetailDto` — adds `PushAddress` (nullable, canonical).
- Service result `ChangePushAddressResult` — `NotFound` | `Unchanged` | `Changed`
  (AD-9).

### Translation keys (uk, en — TC-8)

`Installation.PushAddress.Label`, `.Hint`, `.NotSet`, `.MissingWarning`, `.Change`,
`.Title`, `.ClearNote`, `.Length`, `.Format`, `.Scheme`, `.Extra`, `.Host`,
`.Port`.

## 8. Configuration contract

| Host | Key | Rule | Change |
|---|---|---|---|
| Installation | `Hosting:PrivateAddress` | required; an IPv4 literal, an IPv6 literal (with or without brackets), or exactly `*` | new (spec FR-001, VR-003) |
| Installation | `Hosting:PrivatePort` | as US-005 | — |
| Installation | `Installation:Id`, `ControlPlane:Address`, `ConnectionStrings:Installation` | as US-005 | — |

- Invalid or missing `Hosting:PrivateAddress` → the host does not start; the log names
  the key and the rule ("expected an IP address or *"), never the value.
- The private Kestrel endpoint becomes `http://{address}:{port}` (IPv6 bracketed) or
  `http://*:{port}` for `*`.
- The Control Plane gets no new setting: timeout and retry pauses are fixed values
  (NFR-014), exposed as constants for tests.

## 9. Log events

Event ids continue the US-005 ranges (Control Plane 50xx, installation 51xx).

| Host | Id | Name | Level | Properties |
|---|---|---|---|---|
| Control Plane | 5011 | `StatusPushDelivered` | Information | `InstallationId` (internal) |
| Control Plane | 5012 | `StatusPushAttemptFailed` | Warning | `InstallationId`, `Attempt`, `Category` (`ConnectionFailed` / `Timeout` / `UnexpectedStatus`), `StatusCode` (nullable) |
| Control Plane | 5013 | `StatusPushAbandoned` | Warning | `InstallationId` |
| Control Plane | 5014 | `StatusPushRefused` | Warning | `InstallationId` |
| Control Plane | 5015 | `StatusPushNoAddress` | Warning | `InstallationId` |
| Control Plane | 5016 | `StatusPushHandOffFailed` | Error | `InstallationId`, `ExceptionType` |
| Control Plane | 5017 | `StatusPushSenderException` | Error | `InstallationId`, `ExceptionType` |
| Installation | 5111 | `StatusPushAccepted` | Information | — |
| Installation | 5112 | `StatusPushForeignInstallation` | Warning | — |
| Installation | 5113 | `StatusPushRejected` | Error | `Category` (`invalid` / `too_large`) |
| Installation | 5114 | `PendingPushCheckStarted` | Information | — |

Never a push address, domain, client ID, body or exception message (S-13).

## 10. Auth model

| Operation | Authentication | Policy | Anonymous list | Antiforgery |
|---|---|---|---|---|
| GET `/installations/new`, `/installations/{id}`, `/installations/{id}/push-address` | Owner cookie | `Owner` | — | GET |
| POST `/installations`, `/installations/{id}/push-address`, `…/suspension`, `…/resumption` | Owner cookie | `Owner` | — | required |
| POST `/service/v1/status-pushes` (installation, private port) | none | anonymous | SC-4 "Status-change push receiver" | exempt, SC-4 |

TC-5 cases: push address page GET/POST without session → `302 /sign-in`, without the
`Owner` role → `403`; POST without token → `400`; the Control Plane enumeration tests
gain the two push-address routes as Owner-only. Installation: the receiver is the only
anonymous POST; on the public port `404`, also with forged `Host` /
`X-Forwarded-Host`.

## 11. Error model

| Surface | Form |
|---|---|
| Control Plane pages | error page (`400` antiforgery, `403`, `404`, `500`); form again for `400` validation |
| Push receiver | `202` / `404` / `400` with `ServiceOutcome` or empty; `413` and `500` empty |
| Push delivery | never visible to the Owner; log only |

## 12. Acceptance Criterion → operation map

| AC | Operations | Key assertions |
|---|---|---|
| AC-001 | installation startup | missing / empty / `localhost` / `10.0.0.5:8081` / `**` `Hosting:PrivateAddress` → no start, log names key without value; `127.0.0.1`, `::1`, `[::1]`, `*` → starts; with an IP the private endpoint is bound to it |
| AC-002 | GET/POST `/installations/new` + `/installations`; GET/POST `/installations/{id}/push-address` | optional at registration; set, change, clear at any status; each VR-001 rule → its key, nothing stored; canonical form stored; same address on two installations accepted; unchanged → no write |
| AC-003 | POST `…/push-address` | one `installation_push_address_changed` row per set/change/clear, same transaction, no address in the row; invalid or unchanged → no row; registration with address → only `installation_created` |
| AC-004 | GET `/installations/{id}` | none → `installation-push-address-none` + `installation-push-address-warning`; set → `installation-push-address`, no warning; `uk` and `en` |
| AC-005 | POST `…/suspension` / `…/resumption` → substituted push client | changed → one push to the stored address with only `installationId`; response unchanged and not delayed; unchanged action → none; no address → none + `StatusPushNoAddress`; rename / client ID / push address / AllowedAdmin → none |
| AC-006 | push client against a local test server; sender on a fake clock | `202` → `Delivered`, 1 attempt; `404` → `Refused`, 1 attempt; `500` / `302` / timeout / refused connection → 4 attempts at +5 s, +30 s, +120 s, `StatusPushAbandoned`; no address or body in logs; no audit |
| AC-007 | sender | second enqueue for the same installation during retry wait → old attempts stop, new attempt 1 at once; other installation unaffected |
| AC-008 | host stop | stops promptly with pending retries; nothing persisted |
| AC-009 | POST `/service/v1/status-pushes` (private port), substituted `IControlPlaneClient` | own id → `202` immediately; one check runs; `LegitimacyState` from the check answer only; next scheduled check 6 h / 15 min after it; `StatusPushAccepted` |
| AC-010 | same | other UUID → `404` `unknown_installation`; no check; `StatusPushForeignInstallation` |
| AC-011 | same, fake clock, blocked check | push during a running check → `202`, no second concurrent check, pending check runs right after; pushes within a minute → `202`, one pending check at +60 s; many pushes → one pending; no log for deferred pushes, `PendingPushCheckStarted` when it starts |
| AC-012 | same | missing / non-JSON / malformed / non-UUID body → `400` `invalid_request`; > 4096 bytes → `413`; no check; `StatusPushRejected` without body; schedule unchanged |
| AC-013 | same + enumeration | no session, no token → processed; public port → `404` (also forged `Host`); GET/PUT → not `202`; only anonymous POST of the installation |
| AC-014 | same, stored state read-only (suspended / expired / never confirmed) | push accepted; check writes `LegitimacyState`; resumed answer → mode left |
| AC-015 | all new Control Plane texts | every key in `uk` and `en`; default Ukrainian; address shown as stored |

## 13. Compatibility

- **Control Plane:** additive. Changed operations keep all earlier assertions
  (registration form gains an optional field; detail page gains a row; suspend and
  resume keep their responses). One new Owner-only page pair. No new anonymous or
  exempt endpoint.
- **Installation:** one new anonymous POST on the private port, already foreseen by
  SC-4. One new required setting — an existing deployment without
  `Hosting:PrivateAddress` no longer starts (intended, `trebovaniya.md` v75; DC-3
  already lists the private port's address as required).
- **Channel:** new path under `/service/v1`; contract version stays `1`; older
  Control Planes simply never push. An installation older than this Story answers
  the push path `404` → `Refused`, no retries (acceptable: the Control Plane is
  upgraded first, DC-12, and the periodic check still delivers).

## 14. Open questions

None blocking. Notes for later stages:

- **`db-designer`:** nullable `push_address` column on `installation` (≤ 255, check
  constraint on the canonical shape optional); audit action code
  `installation_push_address_changed`; the conditional status update must also return
  the push address in the same transaction (read before update under the same
  transaction is enough — the address change is itself a separate audited update).
- **`404` vs `405`** for wrong methods on the receiver is left to routing; tests
  assert the outcome class.
