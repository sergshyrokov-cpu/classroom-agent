namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// US-066 spec FR-004.1, OD-001 a: how a namesake's email is written next to the name — «&lt;name&gt; (&lt;email&gt;)»,
/// one line, the same in every language (FR-004.2). Used by the views and the workbook mapper alike (api-design D-3).
/// </summary>
public static class NamesakeLabel
{
    public static string Compose(string name, string? namesakeEmail) =>
        namesakeEmail is null ? name : name + " (" + namesakeEmail + ")";
}
