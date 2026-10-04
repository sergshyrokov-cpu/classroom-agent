namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi <c>ScaleRange</c>.</summary>
public sealed record ScaleRange(
    int From,
    int To,
    string Label);
