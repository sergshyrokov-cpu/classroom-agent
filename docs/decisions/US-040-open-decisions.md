---
artifact_type: open_decisions
story: US-040
version: 2
status: DRAFT
created_at: 2026-10-04T21:09:54Z
updated_at: 2026-10-04T21:11:03Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-040-no-store-responses.md
    version: null
  - path: trebovaniya.md
    version: 85
supersedes: null
---

# US-040 Open Decisions — Responses are not cached

The Story records no Open Decisions. Writing the Specification raised one,
OD-001, resolved by the Owner as (a) on 2026-10-05.

## OD-001 Responses produced before the application pipeline

**Status:** resolved — (a).

**Question.** `trebovaniya.md` v85 §8 and SC-14 say "every response of both
hosts". Kestrel writes a few error responses itself, before a request reaches
the application pipeline — for example `400 Bad Request` for a malformed
request line and `431` for oversized headers. A host-wide middleware rule never
sees them, so they leave without `Cache-Control: no-store`. Are they covered by
the requirement?

**Options.**

- **(a) Excluded.** These responses carry no data and nothing that depends on
  the user — only a fixed status line with an empty body — so there is nothing
  to cache. The Specification stays as written; the rule covers every response
  that passes through the application pipeline. *Recommended.*
- **(b) Covered.** The Story must find a mechanism outside the pipeline
  (e.g. at the reverse proxy in front of the host). No artifact defines one
  today; it would need a deployment-convention change and likely a new
  requirement version.

**Impact.** FR-001, FR-002, §8 Error Handling of the Specification.

**Resolution.** (a) Excluded — the Owner, 2026-10-05. Responses that Kestrel
writes before the application pipeline are outside the rule; every response
that passes through the pipeline is covered.
