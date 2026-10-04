namespace ClassroomAgent.Application.Models.Requests;

/// <summary>The posted template form as raw name/value pairs in posted order (US-027 api-design §2.7): structure and fields are decided in <c>Application</c>; names outside the form are ignored.</summary>
public sealed record ReportTemplateFormInput(
    IReadOnlyList<KeyValuePair<string, string?>> Fields);
