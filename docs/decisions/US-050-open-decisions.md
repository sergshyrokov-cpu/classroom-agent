---
artifact_type: open_decisions
story: US-050
version: 1
status: APPROVED
created_at: 2026-10-16T07:50:00Z
updated_at: 2026-10-16T07:55:03Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-050-browser-security-headers.md
    version: null
  - path: trebovaniya.md
    version: 96
supersedes: null
---

# US-050 Open Decisions — Every response carries the browser security headers

The Story carries no Open Decision: the rule and its values are fixed by
`trebovaniya.md` §8 v95 and SC-15. No new Open Decision was raised while writing
the Specification.

`trebovaniya.md` §7 (v96) has no open item this Story depends on; none concerns
response headers, the Content-Security-Policy, framing or inline code.

The interpretations the Specification makes (I-1 `default-src 'self'`, I-2 the
Google origin rather than the full address, I-3 static files carry the headers,
I-4 one value per header) are listed in its §11 for the reviewer and the Owner;
none of them needs a decision beyond approval of the Specification.
