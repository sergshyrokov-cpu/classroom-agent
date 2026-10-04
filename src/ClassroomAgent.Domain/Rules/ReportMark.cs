using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Rules;

/// <summary>What one cell state is shown as (US-027 entity model §1.2): <see cref="Text"/> is set exactly when the kind is own.</summary>
public sealed record ReportMark(ReportMarkKind Kind, string? Text);
