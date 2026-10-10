namespace ClassroomAgent.Application.Models;

/// <summary>One page of rows and the size of the whole list (API-8).</summary>
public sealed record PagedRows<T>(IReadOnlyList<T> Rows, long TotalElements);
