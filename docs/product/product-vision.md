# Product Vision

## What this is

A system that lets school staff see what is actually happening in Google
Classroom: which courses exist, who teaches and attends them, what work was set
and graded, and who was present at the video lessons.

The school runs its teaching through Google Workspace for Education. Google
Classroom holds the data but does not answer the questions a head of studies
asks — "show me the gradebook for this course for the term, in the format we
print", "who was actually present in October", "which courses does this teacher
run". This system reads that data into its own database and answers those
questions.

## Where it comes from

A working Python/Streamlit prototype already does a narrow version of this for
one school (domain `dac.ukr.education`). It synchronizes Classroom into a local
SQLite cache and produces attendance and gradebook reports. It is a prototype:
the domain is hard-coded, synchronization blocks the UI, report templates are
baked into code, and there is no access control.

This project is the rewrite into a stable C#/.NET system, not an extension of
the prototype. The prototype is evidence of what the data looks like and which
API calls work — not a design to preserve.

## Who it is for

| | |
|---|---|
| **Dean** (учебная часть) | the primary user. Looks at courses, gradebooks and attendance; exports reports; triggers a refresh when the data looks stale. Does this daily. |
| **Admin** | a Google Workspace domain administrator at the school. Installs and configures: connects the installation to the school's Workspace, creates Dean accounts. Rarely present after setup, but sees the same data as the Dean. |
| **Owner** | the person who builds, hosts and supports the system for ~10 schools. Decides which schools run it and with which domain. Never looks at a school's teaching data. |

Teachers and students are **not users of the first version**. They appear as
data — names on a roster, grades in a journal — but have no accounts. Giving
teachers their own view is a later epic, deferred because scoping visibility by
Classroom roster kept producing contradictions in the role model while the only
readers are the Dean and the Admin.

## What success looks like

1. A Dean produces the printed academic journal the school actually uses,
   from real Classroom data, without retyping anything.
2. Attendance for a period comes from real Google Meet records, not from someone
   ticking boxes.
3. A new school is brought online by the Owner creating an `Installation` and
   the school's super-admin authorizing a service account once — no code
   changes, no hard-coded domain.
4. The Owner can suspend a school's access without logging into its server.

## What this is deliberately not

- **Not a Google Workspace management tool.** It reads. It never creates
  accounts, never edits groups, never writes a grade back to Classroom. Every
  API scope is read-only.
- **Not a multi-tenant SaaS.** Each school gets its own installation and its own
  database. Physical isolation of school data is a requirement, not a
  deployment preference.
- **Not a replacement for Classroom.** Teachers keep working in Classroom. This
  system is the reporting and oversight layer above it.
- **Not a real-time dashboard.** Meet attendance data arrives from Google with
  up to ~24 hours of delay; the product is honest about that rather than
  pretending to be live.

## Constraints that shape the product

- The Owner hosts and pays for every installation, which is why the stack is
  licence-free (PostgreSQL) and why key management stays on the Owner's side.
- The system handles personal data of students who may be minors. Access is
  limited to Admin and Dean, and a retention policy is an open requirement that
  must be settled before production.
- Google's domain-wide delegation is authorized by the school's own super-admin.
  The Owner never has access to a school's Google console, and never needs it.

## Source of truth

The full requirements live in `trebovaniya.md` (Russian, versioned). This
document is a summary for orientation; where the two differ, `trebovaniya.md`
wins.
