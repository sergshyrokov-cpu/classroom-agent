# Testing Conventions

Explicit testing decisions for this project. `test-writer` enforces these;
`dotnet-implementor` implements to them; `security-reviewer` checks that the
security-relevant ones actually hold.

Derived from `trebovaniya.md` sections 5 and 9, and from the workflow order in
`docs/workflow/stage-map.yaml`: `TEST_WRITING` runs **before** `IMPLEMENTATION`.

## TC-1 Tests come before the implementation

- Tests are written against the approved Specification, the API design and the
  database design — never against finished code.
- A test that only restates what the implementation happens to do is worthless;
  it must fail if the Specification is violated.
- All business logic in the Application layer has automated unit tests.
  Use cases are the unit under test, ports are substituted.

## TC-2 Integration tests run against real PostgreSQL

- **Testcontainers**, each test class getting an isolated database.
- The EF Core **InMemory provider is forbidden**: it does not enforce
  constraints, unique indexes or cascade rules, so it hides exactly the defects
  these tests exist to catch (`persistence-conventions.md` PC-1).
- Integration tests therefore require a running Docker daemon. A failure to
  start a container is an environment failure, never a reason to fall back to
  InMemory.
- Schema in tests comes from the same EF Core migrations as production
  (PC-2). `EnsureCreated()` is forbidden here too.

## TC-3 The API contract is tested

- Every endpoint in the approved OpenAPI contract has at least one test
  asserting its status codes and error-body shape
  (`api-conventions.md` AC-5, AC-6).
- Error responses are asserted against AC-6 explicitly: `timestamp`, `status`,
  `error`, `message`, `path`, and `fieldErrors` where validation applies.
- A test asserts that an error body never carries a stack trace, SQL, a class or
  namespace name, a file path or a service-account identifier (SC-10).
- Pagination defaults and limits (AC-8: `page` 0, `size` 20, max 100) are
  covered for at least one paginated endpoint.

## TC-4 No test touches a live Google API

- The ports `IClassroomReader`, `IMeetReportsReader` and
  `IWorkspaceCredentialProvider` (`architecture.md` AD-4) are substituted in
  every automated test.
- Fixtures use **synthetic** data: never a real roster, never a real student
  name or email, never a real service-account key, never a real school domain.
- Verifying scopes or domain-wide delegation against a live domain is a manual
  task for the Owner (`trebovaniya.md` section 6), not a test.
- The same applies to the Control Plane channel: `IControlPlaneClient` is
  substituted; no test calls a deployed Control Plane.

## TC-5 Authorization and read-only mode are tested as behaviour

- For each protected endpoint, one test proves an allowed role succeeds and one
  proves a forbidden role is refused (`security-conventions.md` SC-4,
  `api-conventions.md` AC-9). An endpoint with no authorization test is treated
  as an endpoint with no policy.
- Read-only mode is tested in the Application layer: a blocked write must fail
  with the conflict behaviour of AD-6 / SC-5 even when the HTTP endpoint is
  called directly. Asserting that a Razor button is hidden is not a test of
  read-only mode.
- `AllowedAdmin` is asserted to be checked on **every** login, not only the
  first (SC-3) — the first-login-only mistake must fail a test. An Admin login
  while the substituted `IControlPlaneClient` does not answer is asserted to be
  refused.

## TC-6 Layout

- Tests live in `tests/ClassroomAgent.Tests/`. For a production class
  `ClassroomAgent.<Project>.<Namespace>.<Name>`, its tests live in
  `ClassroomAgent.Tests.<Project>.<Namespace>` (`package-map.md`).
- Integration tests spanning layers may sit in a `ClassroomAgent.Tests.<Feature>`
  namespace under the same root.
- xUnit with `Microsoft.AspNetCore.Mvc.Testing` for endpoint tests. Adding a
  testing package requires an approved decision, like any other dependency.

## TC-7 A Story's tests are its evidence

- Every Acceptance Criterion maps to at least one passing test; the mapping is
  recorded in the `ac_test_matrix` artifact.
- `dotnet test` green, with no skipped, ignored or commented-out tests, is part
  of the Definition of Done in `AGENTS.md`.
- A test disabled to make a build pass is a defect, not a workaround: either the
  Specification is wrong (loop back to `SPECIFICATION`) or the implementation is.
