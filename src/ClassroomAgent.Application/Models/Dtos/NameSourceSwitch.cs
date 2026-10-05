namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-042 <c>NameSourceSwitch</c>: the two links of the report page's switch, <c>profile</c> then <c>email</c>.</summary>
public sealed record NameSourceSwitch(IReadOnlyList<NameSourceSwitchOption> Options);
