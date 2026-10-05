---
artifact_type: test_strategy
story: US-040
version: 1
status: DRAFT
created_at: 2026-10-05T06:47:34Z
updated_at: 2026-10-05T06:47:34Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-040-spec.md
    version: 2
  - path: docs/designs/api/US-040-openapi.yaml
    version: 1
  - path: docs/designs/api/US-040-api-design.md
    version: 1
  - path: docs/designs/database/US-040-db-design.md
    version: 1
  - path: docs/decisions/US-040-open-decisions.md
    version: 2
supersedes: null
---

# US-040 Test Strategy — Responses are not cached

## 1. Scope

The host-wide `Cache-Control: no-store` rule of both hosts (spec FR-001 …
FR-006) and its single exception, served static files (FR-004). No persistence,
no new endpoint, no Application-layer behaviour.

## 2. Test levels

| Level | Why | Classes |
|---|---|---|
| Integration over HTTP (`WebApplicationFactory`, real PostgreSQL via Testcontainers, TC-2) | The rule is observable only on real responses produced by the whole pipeline: authentication, authorization, antiforgery, exception handler, status-code re-execution, static files | `Web/Security/NoStoreResponseTests`, `ControlPlane/Security/NoStoreResponseTests` |
| Architecture (reflection) | AC-004 "not by attributes on individual controllers or actions" | `Architecture/NoStoreRuleTests` |

No unit level: there is no use case or rule in `Application` to test (TC-1).

## 3. Scenarios

**Enumeration (VR-004, AC-004).** Every routed endpoint of the host
(`HostEndpoint.All`, the US-008/TC-5 helper), every declared method except
HEAD/OPTIONS (GET and POST for an endpoint accepting any method), requested
with the sample path of its pattern. Every response, whatever its status,
must carry `no-store`. Run for each kind of caller:

- installation public port: Admin, Dean, anonymous;
- installation private port: one run (no session applies there);
- Control Plane: Owner, anonymous with the Owner existing, before setup (setup
  gate).

Non-GET requests carry an empty form body and no antiforgery token, so they
also exercise the refusal paths (400, 404, 415, 302 /setup).

**Response kinds (VR-002), each its own test:**

| Kind | Installation | Control Plane |
|---|---|---|
| signed-in page `200` | Admin `/`, Dean `/`, Dean journal with student data | Owner `/`, `/installations` |
| anonymous page `200` | `/sign-in` | `/sign-in`, `/setup` before setup |
| redirect | anonymous `/` → `302 /sign-in`; plain HTTP → HTTPS redirection | anonymous `/` → `302 /sign-in`; before setup `/` → `302 /setup` |
| `403` | Dean → `/settings/deans` | — (one role; no `403` exists) |
| `404` | `/no-such-page`, `/api/v1/sync` | `/no-such-page`, `/api/v1/installations` |
| antiforgery `400` | POST `/settings/deans` without token | POST `/sign-out` without token |
| read-only `409` | create a Dean while suspended | — (no read-only mode) |
| exception handler `500` | journal with table `course` dropped under the running host | `/installations` with table `installation` dropped |
| private port | health checks, push receiver, 404 (enumeration) | — |

**Static files (VR-003, AC-003, D-4).** Every file of each host's `wwwroot`
answers `200` without `no-store`; a missing file under `/css` gets an ordinary
response (not `200`) with `no-store`.

**Header check (VR-001).** `NoStore.IsIn`: comma-split, trimmed,
case-insensitive directive `no-store`; other directives allowed (FR-006).

## 4. Negative, boundary, validation, security, persistence

- Negative: every refusal kind above is a negative scenario of the host; the
  static-file test is the negative of the rule.
- Boundary: the static exception is bounded by "a served file" — the missing
  file test sits on that boundary.
- Validation: no input is added (spec §6).
- Security: the Story is itself a confidentiality control (SR-001); covered by
  all of the above, for every caller kind and authentication state.
- Persistence: none (DB_DESIGN `NOT_APPLICABLE`).

## 5. Fixtures

Existing only: `PostgreSqlFixture`, `InstallationTestHost`,
`ControlPlaneTestHost`, `JournalHostExtensions` (actors and the seeded
journal), `DeanAccountHostExtensions` (read-only Admin), `HostEndpoint`. New
helper: `TestInfrastructure/NoStore.cs` (VR-001 check, enumeration loop, static
file list). No Google port is called live (TC-4).

## 6. Excluded scenarios

- **A downloaded file** (VR-002 "where one exists"): no download endpoint
  exists yet; the export Stories US-028 … US-030 will add them, and the
  enumeration test covers them automatically.
- **An `/api/v1` success response**: no `/api/v1` operation exists on either
  host; the `/api/v1` `404` is tested, and a later operation is covered by the
  enumeration.
- **Kestrel-level responses** (malformed request `400`, `431`): outside the
  rule by OD-001 (a).
- **A `403` and a `409` on the Control Plane**: no such response exists there.

## 7. Known limitations

- The enumeration uses a sample path per pattern; an endpoint with a route
  constraint the sample does not satisfy answers `404` from routing. That
  response still passes through the host's pipeline, so the header check
  remains meaningful; the per-kind tests prove the real pages.
- `NoStoreRuleTests` passes before implementation (no `[ResponseCache]` exists
  today). It is a guard against a future per-page decision, not red-phase
  evidence; the behaviour is proven by the enumeration tests.

## 8. Open Decisions affecting testing

None open. OD-001 (a) defines the excluded Kestrel responses.
