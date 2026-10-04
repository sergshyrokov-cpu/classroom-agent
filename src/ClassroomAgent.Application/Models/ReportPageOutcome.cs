namespace ClassroomAgent.Application.Models;

/// <summary>How the report page answers (US-027 api-design §2.5): <c>200</c>, <c>400</c> or <c>404</c>.</summary>
public enum ReportPageOutcome
{
    Shown,

    Invalid,

    NotFound,
}
