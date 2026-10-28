using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.Models;

/// <summary>US-065 entity model §3: an exclusion entered in advance that has no Google user id yet — a read model.</summary>
public sealed record UnmatchedExclusion(long Id, EmailFingerprint Fingerprint);
