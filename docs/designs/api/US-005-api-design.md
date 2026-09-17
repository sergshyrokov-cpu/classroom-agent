---
artifact_type: api_design
story: US-005
version: 1
status: DRAFT
created_at: 2026-09-17T13:33:00Z
updated_at: 2026-09-17T13:33:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-005-spec.md
    version: 1
  - path: docs/decisions/US-005-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 73
  - path: docs/designs/api/US-004-openapi.yaml
    version: 1
supersedes: null
---

# US-005 API Design — Installation legitimacy check and grace period

Contract: `docs/designs/api/US-005-openapi.yaml` (`info.version: "1"`).

## 1. Rationale

- **Three surfaces, two hosts.**
  1. The **service channel** in the Control Plane — the legitimacy check called by
     installations. JSON over HTTPS, protected by network isolation (SC-9). It is
     **not** the public REST API: API-1 … API-6 do not apply to it (API-7); its
     rules are DC-12 and this document.
  2. The installation's **private port** — liveness and readiness (DC-6, DC-11).
  3. The Control Plane **detail page** (US-002) — gains the last check section.
- **No `/api/v1` endpoint**, and no installation page on the public port (spec
  I-13).
- **Not `NOT_APPLICABLE`.** The Story adds an endpoint, a wire contract and
  private endpoints.
- **Builds on US-001 … US-004.** The Control Plane host-wide rules of
  `US-001-openapi.yaml`, the `{id}` route and output encoding of
  `US-002-openapi.yaml`, and the detail page as extended by US-003 and US-004 apply
  unchanged.

## 2. Routes

| Host / port | Method | Path | Anonymous | Antiforgery | Purpose | Spec |
|---|---|---|---|---|---|---|
| Control Plane | POST | `/service/v1/legitimacy-checks` | yes (SC-4 "Legitimacy check") | exempt (SC-4) | the check | FR-002, FR-004 … FR-006 |
| Control Plane | GET | `/installations/{id}` (changed) | no, `Owner` | — | detail page gains "last check" | FR-011 |
| Installation, private port | GET | `/health/live` | yes (SC-4 "Liveness and readiness") | — | liveness | FR-013 |
| Installation, private port | GET | `/health/ready` | yes (same entry) | — | readiness | FR-013 |
| Installation, public port | any | any | — | — | `404`, empty body | FR-014, I-13 |

- **Why `/service/v1/…`.** The channel is versioned in the path so a breaking
  change can add `/service/v2/…` and serve both during the upgrade window (DC-12,
  spec I-7). `service` keeps the channel visibly apart from Owner pages and from a
  future `/api/v1`. `legitimacy-checks` is a plural noun (API-3 style): each POST
  submits one check.
- The contract version sent in the body (§4) is the `Contracts` version; the path
  version is the endpoint version. In this Story both are `1`.

## 3. Control Plane host-wide rules for the channel

- **Anonymous and antiforgery-exempt** on this one endpoint only — the existing
  "Legitimacy check" entries of both SC-4 closed lists. The US-001 enumeration
  tests list it as allowed; no other endpoint changes (S-01).
- **Setup gate applies unchanged.** Before the Owner account exists the endpoint,
  like every other, answers `302 /setup`. No `Installation` can exist then, and the
  installation classifies a `3xx` as an `error answer` (§6) — an unsuccessful check,
  as the spec requires. No new exception to the setup gate.
- **Other methods** on the path do not reach the handler: `404` or `405`, as the
  host's routing and catch-all resolve it. Tests assert "not 200, not 400, no
  record".
- **Not cookie-authenticated.** A session cookie, if sent, is ignored; no challenge,
  no redirect.
- **No audit row**, ever (S-14).

## 4. Wire contract (`ClassroomAgent.Contracts`)

JSON, `application/json; charset=utf-8`, property names camelCase. Both sides
ignore unknown properties (DC-12). Contract version constant: `1`.

### LegitimacyCheckRequest

| Property | Type | Required | Rule (VR-002) |
|---|---|---|---|
| `installationId` | string, UUID | yes | canonical UUID |
| `applicationVersion` | string | yes | `MAJOR.MINOR.PATCH`, each 0–999999, no leading zeros except `0`, ≤ 20 chars |
| `contractVersion` | integer | yes | 1–999999 |

### LegitimacyCheckResponse (known installation, `200`)

| Property | Type | Values |
|---|---|---|
| `status` | string | `active` \| `suspended` |
| `compatibility` | string | `supported` \| `upgrade_recommended` \| `upgrade_required` |
| `domain` | string | the `Installation` domain (3–253) |
| `clientId` | string | the `Installation` client ID (10–32 digits) |

Nothing else — no id echo, no time, no versions (SC-12).

### ServiceOutcome (non-`200` bodies)

| Property | Type | Values |
|---|---|---|
| `outcome` | string | `unknown_installation` (with `404`) \| `invalid_request` (with `400`) |

No field errors, no echo of the request, no message text.

## 5. POST /service/v1/legitimacy-checks

| Step | Check | Result | Recorded | Log |
|---|---|---|---|---|
| 1 | body is JSON with a JSON content type and parses into `LegitimacyCheckRequest`; every rule of §4 holds | else `400` `{"outcome":"invalid_request"}` — including a missing body, a non-JSON content type, a wrong type, a missing field | nothing | no body, no values |
| 2 | an `Installation` with `installationId` exists | else `404` `{"outcome":"unknown_installation"}` | nothing | `Warning`, received id |
| 3 | compatibility per spec FR-005 (supported contract versions `{1}`; configured minimum / recommended) | — | — | — |
| 4 | upsert `InstanceLicenseCheck` for the installation: answered-at (UTC now), application version, contract version, answered status, answered compatibility | — | one record per installation, last write wins | — |
| 5 | answer | `200` `LegitimacyCheckResponse` with the stored status, computed compatibility, domain, client ID | — | `Information`: installation id, versions, status, compatibility |

- A suspended installation goes through the same steps (spec I-11).
- `upgrade_required` is a normal `200` answer; the installation decides it is an
  unsuccessful check (spec FR-007).
- Concurrent calls for one installation: each answered `200`; one record remains;
  never `500` (spec I-14; mechanism: `db-designer`).
- `500` only for an unhandled exception, with an empty body; nothing recorded.
- Log lines never include domain, client ID or body (S-08).

## 6. Installation side of the channel (`IControlPlaneClient`)

The client posts `LegitimacyCheckRequest` to
`{ControlPlane:Address}/service/v1/legitimacy-checks` and classifies the reply.
This classification is part of the contract because tests of both sides rely on it:

| HTTP result | Classification (spec FR-007) |
|---|---|
| connection refused, DNS failure, TLS failure (including an untrusted certificate) | `unreachable` |
| no complete answer within 30 s (connect, TLS, headers and body) | `timeout` |
| `200` with a body that parses and passes VR-003 | known answer → successful, or `upgrade required` if compatibility is `upgrade_required` |
| `200` with a body that does not parse or fails VR-003 | `unparseable answer` |
| `404` with JSON body `{"outcome":"unknown_installation"}` | `unknown installation` |
| any other status — `404` without that body (wrong address, proxy), `400`, `3xx`, `5xx` | `error answer` |

- Redirects are **not followed**: a `3xx` is an `error answer`.
- Certificate validation is the platform's; no custom callback (S-05).
- The client reports the category only; the response body, headers and exception
  text never leave the client and are never logged (S-08).
- Application version sent: the installation's release version `MAJOR.MINOR.PATCH`
  (spec I-7) — the assembly informational version without any `+metadata` or
  pre-release suffix. Contract version: the `Contracts` constant.

## 7. Installation private endpoints

Bound to the private port by the private route group filter comparing
`HttpContext.Connection.LocalPort` with `Hosting:PrivatePort`; `Host` and
`X-Forwarded-Host` are ignored (DC-6).

### GET /health/live

- Private port: `200`, `text/plain`, body `Healthy`. Touches no dependency.
- Public port: `404`, empty body.

### GET /health/ready

| State (spec FR-013) | Status | Body |
|---|---|---|
| database unreachable | `503` | `Unhealthy` |
| read-only (spec FR-008), or the last check since startup was unsuccessful | `200` | `Degraded` |
| otherwise | `200` | `Healthy` |

- If the database is unreachable the state is `Unhealthy` regardless of the rest.
- Before the first check since startup completes, only the stored state decides.
- `text/plain`; the body is exactly one of the three words — no reason, time,
  version or exception (S-09).
- Public port: `404`, empty body.
- Other methods on either path: `404` or `405`; nothing changes.

## 8. Installation public port

Every request — any method, any path, including `/health/live`, `/health/ready`
and `/service/…` — answers `404` with an empty body (spec I-13). No cookie is set,
no redirect, nothing read or written. HTTPS redirection, HSTS, the error page and
the fallback policy arrive with US-008.

## 9. GET /installations/{id} (changed)

Everything of US-002, US-003 and US-004 stays. Added below the status block, a
section `id="installation-last-check"` with heading `Installation.LastCheck.Title`:

| Record | Rendered |
|---|---|
| exists | `id="installation-last-check-time"` — date and time to the minute in UTC, marked "UTC" (spec I-12); `id="installation-last-check-application-version"`; `id="installation-last-check-contract-version"`; `id="installation-last-check-status"` — `Installation.Status.Active` / `Installation.Status.Suspended` (US-002 keys); `id="installation-last-check-compatibility"` — `Installation.Compatibility.Supported` / `.UpgradeRecommended` / `.UpgradeRequired`; each with its label key |
| none | `id="installation-last-check-none"` — `Installation.LastCheck.NotCalledYet`; none of the value elements |

- Versions are rendered with Razor's default HTML encoding (S-12).
- Labels: `Installation.LastCheck.Time`, `.ApplicationVersion`,
  `.ContractVersion`, `.Status`, `.Compatibility`.
- All new keys exist in `uk` and `en` (TC-8).
- No new query parameter, no new link; the installations list is unchanged.

## 10. Configuration contract

Settings tests and deployment rely on (spec FR-001, FR-005):

| Host | Key | Rule |
|---|---|---|
| Installation | `Installation:Id` | required, UUID |
| Installation | `ControlPlane:Address` | required, absolute `https://` URI, no user info, query or fragment |
| Installation | `Hosting:PrivatePort` | required, 1–65535, differs from every public endpoint port |
| Installation | `ConnectionStrings:Installation` | required, non-empty |
| Control Plane | `Compatibility:MinimumSupportedVersion` | optional, `MAJOR.MINOR.PATCH` |
| Control Plane | `Compatibility:RecommendedVersion` | optional, `MAJOR.MINOR.PATCH` |

Invalid required installation settings, or a set but invalid Control Plane version
setting, stop the host at startup; the log names the key and the rule, never the
value.

## 11. Response models

- `LegitimacyCheckRequest`, `LegitimacyCheckResponse`, `ServiceOutcome` — in
  `ClassroomAgent.Contracts` (§4); wire types only.
- Control Plane service result (AD-9): `LegitimacyCheckResult` — `Known(response)`
  | `UnknownInstallation`. Validation failures stop at binding.
- `InstallationDetailDto` gains `LastCheck` (nullable): `AnsweredAt` (UTC),
  `ApplicationVersion`, `ContractVersion`, `Status`, `Compatibility`.
- Installation port result: `ControlPlaneCheckReply` — `Answer(status,
  compatibility, domain, clientId)` | `Failure(category)` with category ∈
  {`Unreachable`, `Timeout`, `ErrorAnswer`, `UnparseableAnswer`,
  `UnknownInstallation`}. `upgrade required` is decided by the use case from the
  answer. No HTTP or `Contracts` type crosses into `Application` beyond the port
  (AD-4: the port returns `Application` models).
- Read-only query result: `LegitimacyMode` — `IsReadOnly`, `Reason` ∈
  {`NotYetConfirmed`, `SuspendedByOwner`, `GracePeriodExpired`}, `LastSuccessfulCheckAt`.

## 12. Auth model

| Operation | Authentication | Policy | Anonymous list | Antiforgery |
|---|---|---|---|---|
| POST `/service/v1/legitimacy-checks` | none | `AllowAnonymous` | SC-4 "Legitimacy check and Admin login check" | exempt, SC-4 |
| GET `/installations/{id}` | Owner cookie | `Owner` | — | GET |
| GET `/health/live`, `/health/ready` | none | anonymous | SC-4 "Liveness and readiness" | GET |

TC-5 cases: the channel endpoint answers without a session and without a token
(allowed) and is the only such POST in the Control Plane; the detail page keeps its
US-002 allowed/forbidden tests; private endpoints answer on the private port and
`404` on the public port, also with a forged `Host` / `X-Forwarded-Host`.

## 13. Error model

| Surface | Form |
|---|---|
| Channel | `400` / `404` with `ServiceOutcome`; `500` empty body |
| Detail page | host error page, as US-002 |
| Private endpoints | status and one-word body |
| Installation public port | `404`, empty body |

## 14. Acceptance Criterion → operation map

| AC | Operations | Key assertions |
|---|---|---|
| AC-001 | installation startup | each missing/invalid key of §10 → host fails to start, log names the key without the value; all valid → starts |
| AC-002 | installation background service → POST channel (substituted port) | first call right after start; next after 6 h (success) / 15 min (failure) on a fake clock; never two concurrent; exception keeps the schedule |
| AC-003 | POST `/service/v1/legitimacy-checks` | `200` with exactly `status`, `compatibility`, `domain`, `clientId`; suspended installation answered with `suspended` |
| AC-004 | POST channel with configured versions | contract version ≠ 1 → `upgrade_required`; below minimum → `upgrade_required`; below recommended → `upgrade_recommended`; else `supported`; unset settings impose nothing |
| AC-005 | POST channel ×2 | one `InstanceLicenseCheck` holding the second call's time, versions and answer; `Information` log; no audit row |
| AC-006 | POST channel | unknown UUID → `404` `unknown_installation`, no record, `Warning`; invalid/missing/malformed body → `400` `invalid_request`, no record, body not logged; never `500` |
| AC-007 | client + use case | `200` known answer → `LegitimacyState` time/status/compat/domain/clientId; `suspended` is success; `upgrade_recommended` → `Warning` |
| AC-008 | client classification §6 | each row of the table → category; last success unchanged; `upgrade_required` stores the rest; `Error` log with category only |
| AC-009 | read-only query | not yet confirmed / suspended / >7 days (strict) / otherwise |
| AC-010 | use case logging | enter → one `Warning`, leave → one `Information`, result change → `Information`, repeats silent |
| AC-011 | installation restart | one `LegitimacyState`; 2 days ago → not read-only; 8 days ago → read-only, with the Control Plane unreachable |
| AC-012 | GET `/installations/{id}` | record → the five value elements with UTC minute time and translated labels; none → `installation-last-check-none`; `uk` and `en` keys; Owner-only as US-002 |
| AC-013 | POST channel; enumeration tests | no session, no token → processed; GET/PUT/DELETE → not handled; the only anonymous POST in the Control Plane |
| AC-014 | GET `/health/live`, `/health/ready` | private port: `200 Healthy`; ready → `503 Unhealthy` / `200 Degraded` / `200 Healthy`; public port → `404`, also with forged `Host` |
| AC-015 | all log lines | no domain, client ID, email or body in either host's log |

## 15. Compatibility

- **Control Plane:** additive. One changed operation, `GET /installations/{id}`,
  gains a section; earlier assertions stay valid. One new anonymous,
  antiforgery-exempt endpoint — already foreseen by SC-4. The setup gate is
  unchanged (§3).
- **Installation:** new host; nothing existed before.
- **Channel evolution:** additive changes keep `/service/v1/…` and contract
  version `1`; a breaking change adds a path version and a contract version while
  the Control Plane keeps serving the old one (DC-12).

## 16. Open questions

None blocking. Notes for later stages:

- **`db-designer`:** `InstanceLicenseCheck` upsert under concurrency (one row per
  installation, last write wins, no `500`); `LegitimacyState` single-row guarantee;
  first installation migration.
- **`404` vs `405`** for wrong methods is left to the host routing; tests assert the
  outcome class, not the exact code.
