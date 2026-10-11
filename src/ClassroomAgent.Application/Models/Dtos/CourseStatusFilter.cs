namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-020 <c>CourseStatusFilter</c>: the five course states and <c>all</c>, in display order (spec FR-004).</summary>
public enum CourseStatusFilter
{
    Active,

    Archived,

    Provisioned,

    Declined,

    Suspended,

    All,
}
