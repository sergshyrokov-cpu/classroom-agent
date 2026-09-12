# Report Templates

Reference copies of the Excel report templates a school actually uses. They are
**samples, not a specification**: `ReportTemplate` is a configurable resource
owned by the Dean (`trebovaniya.md` sections 3 and 8), and a school may use a
different layout. These files exist so that export work has a concrete target
instead of an abstraction.

| File | Origin |
|---|---|
| `school-journal-full.xlsx` | originally `Шаблон.xlsx` in the repository root; the full-journal layout used with the Python prototype's `exports.py` |

Rules:

- These files are checked in because they carry no student data — headers and
  layout only. Verify that before adding another one.
- Generated exports (`journal_*.xlsx`, `classroom_activity_report.xlsx`) are
  **not** versioned: they contain real personal data and stay git-ignored.
- `.gitignore` ignores `*.xlsx` everywhere and re-includes only this directory,
  so a new template belongs here and nowhere else.
