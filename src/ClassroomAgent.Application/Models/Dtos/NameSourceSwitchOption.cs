using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-042 <c>NameSourceSwitchOption</c>: one source, whether it is the effective one, and its link.</summary>
public sealed record NameSourceSwitchOption(ReportNameSource Source, bool IsCurrent, string Path);
