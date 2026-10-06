namespace ClassroomAgent.Application.UseCases;

/// <summary>US-028 entity model §3.2: builds the file name of an exported journal. Skeleton (OD-005).</summary>
public static class JournalExportFileName
{
    public static string Build(string courseName, DateOnly from, DateOnly to, string fallback) =>
        throw new NotImplementedException("US-028 IMPLEMENTATION");
}
