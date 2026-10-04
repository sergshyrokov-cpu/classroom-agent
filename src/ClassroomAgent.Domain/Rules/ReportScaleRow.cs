namespace ClassroomAgent.Domain.Rules;

/// <summary>One row of a grading scale, <c>from % – to % → label</c> (US-027 spec FR-015, entity model §1.2).</summary>
public sealed record ReportScaleRow(int FromPercent, int ToPercent, string Label);
