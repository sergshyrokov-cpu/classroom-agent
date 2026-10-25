namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// The start of a course membership for journals, reports and Meet (trebovaniya.md §3 «Начало участия», v98;
/// BR-051; US-056 spec FR-001, db-design §1).
/// </summary>
public static class MembershipStart
{
    public static DateTimeOffset Of(
        DateTimeOffset firstSeenAt,
        DateTimeOffset courseFirstLoadedAt,
        DateTimeOffset? courseCreationTime) =>
        firstSeenAt <= courseFirstLoadedAt ? courseCreationTime ?? firstSeenAt : firstSeenAt;
}
