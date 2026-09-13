# API Conventions

Explicit decisions for HTTP APIs in this project. `openapi-designer` enforces
these; `dotnet-implementor` implements to them; `security-reviewer` checks
against them.

Note the split from `trebovaniya.md` section 8: the UI is **server-rendered
Razor**, and the REST API serves that UI. These conventions govern the REST API.
Razor page routes are not `/api/v1/…` and are not part of the versioned
contract.

## AC-1 Versioning

- URI-path versioning: every API endpoint is under `/api/v1/…`.
- A breaking change to an existing contract requires a new version prefix and an
  approved decision; it is never made in place.

## AC-2 Media type

- Request and response bodies are `application/json` (UTF-8).
- `Content-Type: application/json` is required on requests with a body;
  otherwise respond `415`.
- Exception: report export endpoints respond with the generated file
  (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` for
  Excel, `…wordprocessingml.document` for Word) plus
  `Content-Disposition: attachment`. Errors from those endpoints are still JSON
  per AC-6.

## AC-3 Resource naming

- Plural nouns: `/api/v1/courses`, `/api/v1/courses/{id}`.
- Kebab-case for multi-word path segments; `camelCase` for JSON field names.
- No verbs in paths. The two unavoidable actions get explicit approved shapes:
  - `POST /api/v1/sync` — enqueue a synchronization run (AD-5: returns
    immediately, does not wait)
  - `POST /api/v1/workspace-connection/test` — the "Проверить доступ" check from
    Epic 6

## AC-4 HTTP methods & success codes

| Method | Use | Success |
|---|---|---|
| `POST /collection` | create | `201 Created`, `Location` header, created resource body |
| `GET /collection` | list | `200 OK` |
| `GET /collection/{id}` | read one | `200 OK` |
| `PUT /collection/{id}` | full replace | `200 OK` (or `204` if no body) |
| `PATCH /collection/{id}` | partial update | `200 OK` |
| `DELETE /collection/{id}` | delete | `204 No Content` |
| `POST /api/v1/sync` | enqueue background work | `202 Accepted` with the `SyncState` id |

## AC-5 Error codes

| Status | When |
|---|---|
| `400 Bad Request` | request-shape / validation failure, malformed JSON |
| `401 Unauthorized` | authentication required or failed |
| `403 Forbidden` | authenticated but not permitted by the role matrix |
| `404 Not Found` | resource does not exist (or is not visible to the caller) |
| `409 Conflict` | uniqueness or state conflict — **including a write attempted while the installation is in read-only mode** (AD-6) |
| `415 Unsupported Media Type` | missing/wrong `Content-Type` |
| `429 Too Many Requests` | only if this API ever rate-limits its own clients; Google's `429` is handled internally (AD-5) and never forwarded |
| `500 Internal Server Error` | unmapped exception (must not leak internals) |

Read-only mode returns `409`, not `403`: the caller has the right, the
installation is temporarily refusing writes. The error `message` says so
plainly and names the reason (grace period expired / suspended by the Owner).

## AC-6 Error body

All error responses use exactly this JSON shape:

```json
{
  "timestamp": "2026-09-12T10:15:30Z",
  "status": 409,
  "error": "Conflict",
  "message": "The installation is in read-only mode: legitimacy has not been confirmed since 2026-09-05.",
  "path": "/api/v1/sync"
}
```

- `message` is safe to display to a Dean or Admin. It never contains stack
  traces, SQL, class or namespace names, file paths, Google API raw errors,
  service-account identifiers, or secrets.
- Validation failures may add a `fieldErrors` array of
  `{ "field": "...", "message": "..." }`.
- A Google permission failure surfaced to the Admin (AD-5) carries a message
  naming the missing capability in plain terms — "the service account is not
  authorized to read Admin Reports for this domain" — never the raw exception.

## AC-7 Authentication

- Cookie-based authentication for both roles. The session cookie is `httpOnly`;
  no `Authorization` header is expected (`trebovaniya.md` section 8).
- Admin authenticates through Google OAuth (external login); Dean through local
  login/password. Both end in the same cookie.
- The Control Plane ↔ Data Plane channel is **not** part of this API and does
  not use these conventions: it is protected by network isolation
  (`trebovaniya.md` section 9) and speaks the types in
  `ClassroomAgent.Contracts`.

## AC-8 Pagination

- Any endpoint returning a collection that can grow unbounded — courses,
  participants, submissions, Meet sessions — is paginated from day one.
- Query params: `page` (0-based, default `0`), `size` (default `20`, max `100`).
- Response body:

```json
{
  "content": [ ... ],
  "page": 0,
  "size": 20,
  "totalElements": 137,
  "totalPages": 7
}
```

- Sorting via `sort=field,asc|desc` when the design lists sortable fields.
- Report exports are **not** paginated — they render the full selected period by
  design.

## AC-9 Authorization is declared, not improvised

Every endpoint declares the roles allowed to reach it, matching the permission
matrix in `trebovaniya.md` section 2. `openapi-designer` records the required
role per operation; `dotnet-implementor` implements it as an authorization
policy (`security-conventions.md` SC-4). An endpoint with no declared policy is
a Critical finding, not a default-allow. Anonymous access is allowed only for
the closed list of endpoints in SC-4.

## AC-10 Exception-handler location

Exception → HTTP mapping happens in the single `IExceptionHandler` per host
(`architecture.md` AD-9). Controllers do not `try/catch` to build error
responses.
