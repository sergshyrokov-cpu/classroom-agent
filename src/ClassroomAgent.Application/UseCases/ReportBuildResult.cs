using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// What <see cref="ReportBuilder"/> produced (US-028 entity model §3.3): the report, or the not-found keys in the order
/// template, course — never both.
/// </summary>
internal sealed record ReportBuildResult(Report? Report, IReadOnlyList<ReportMessageKey> NotFound);
