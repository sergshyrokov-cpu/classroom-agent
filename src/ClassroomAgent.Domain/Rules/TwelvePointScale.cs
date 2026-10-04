namespace ClassroomAgent.Domain.Rules;

/// <summary>The 12-point preset of US-027 OD-002 / spec FR-015: twelve rows covering 0–100, labels "1" … "12".</summary>
public static class TwelvePointScale
{
    public static IReadOnlyList<ReportScaleRow> Rows { get; } =
    [
        new(0, 8, "1"),
        new(9, 16, "2"),
        new(17, 25, "3"),
        new(26, 33, "4"),
        new(34, 41, "5"),
        new(42, 50, "6"),
        new(51, 58, "7"),
        new(59, 66, "8"),
        new(67, 75, "9"),
        new(76, 83, "10"),
        new(84, 91, "11"),
        new(92, 100, "12"),
    ];
}
