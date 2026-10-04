namespace ClassroomAgent.Application.Exceptions;

/// <summary>
/// The database refused a second template with the same normalized name (US-027 db-design §2.1): thrown by the unit
/// of work for <c>uq_report_template_normalized_name</c> only, so <c>Application</c> never sees a provider type.
/// </summary>
public sealed class UniqueReportTemplateNameViolationException : Exception
{
    public UniqueReportTemplateNameViolationException()
    {
    }

    public UniqueReportTemplateNameViolationException(Exception innerException)
        : base("A report template with this normalized name already exists.", innerException)
    {
    }
}
