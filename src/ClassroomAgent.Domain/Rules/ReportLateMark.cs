using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Rules;

/// <summary>The late mark (US-027 entity model §1.2): <see cref="Text"/> is set exactly when the kind is own.</summary>
public sealed record ReportLateMark(ReportLateMarkKind Kind, string? Text);
