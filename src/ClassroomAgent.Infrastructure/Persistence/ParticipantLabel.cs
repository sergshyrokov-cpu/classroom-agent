using System.Linq.Expressions;
using ClassroomAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// The one label key and order of people in the course pages (US-020 db-design §3.4), shared so the teacher list and the
/// course list's teacher choice cannot drift apart (US-022 db-design §3.3): profile name, else email, Ukrainian-aware
/// under <c>course_name_uk</c>, people with neither last, ties by id.
/// </summary>
internal static class ParticipantLabel
{
    private const string Collation = ClassroomAgentDbContext.CourseListCollation;

    /// <summary>The participant's columns the pages read, with the label key.</summary>
    public static readonly Expression<Func<ClassroomParticipant, LabeledParticipant>> Projection = p => new LabeledParticipant
    {
        Id = p.Id,
        Surname = p.Surname,
        GivenName = p.GivenName,
        Email = p.Email,
        Label = p.Surname != null && p.Surname != string.Empty && p.GivenName != null && p.GivenName != string.Empty
            ? p.Surname + " " + p.GivenName
            : p.Surname != null && p.Surname != string.Empty
                ? p.Surname
                : p.GivenName != null && p.GivenName != string.Empty
                    ? p.GivenName
                    : p.Email != null && p.Email != string.Empty
                        ? p.Email
                        : null,
    };

    /// <summary>Total and stable: no label last, then the label under the collation, then the id.</summary>
    public static IOrderedQueryable<LabeledParticipant> OrderByLabel(this IQueryable<LabeledParticipant> people) =>
        people
            .OrderBy(x => x.Label == null ? 1 : 0)
            .ThenBy(x => EF.Functions.Collate(x.Label, Collation))
            .ThenBy(x => x.Id);
}
