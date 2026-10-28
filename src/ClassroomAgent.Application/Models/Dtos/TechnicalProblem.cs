namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-062 OpenAPI <c>TechnicalProblem</c>: <see cref="Details"/> is always null for a Dean.</summary>
public sealed record TechnicalProblem(
    TechnicalProblemKind Kind,
    DateTimeOffset? EmptySince,
    DateTimeOffset? LastCheckAt,
    DateTimeOffset? ReadOnlyFrom,
    TechnicalDetails? Details);
