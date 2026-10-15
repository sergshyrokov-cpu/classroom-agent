using System.Reflection;
using ClassroomAgent.Application.Validation;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-038 entity model §1 … §5, spec FR-008, FR-010, FR-013, VR-002, VR-004, S-05, S-06; AC-006, AC-007, AC-009: the
/// fingerprint, the salt, the exclusion, the counts, the mark and the three audit factories, without any port.
/// </summary>
public sealed class ErasureInvariantTests
{
    private const string Email = "o.koval@school-one.example.test";

    /// <summary>FR-010, I-2: HMAC-SHA-256 keyed with the salt over the UTF-8 of the normalized email — 32 bytes.</summary>
    [Fact]
    public void TheFingerprint_IsHmacSha256_OfTheNormalizedEmail_KeyedWithTheSalt()
    {
        var salt = ErasureSalt.Generate();

        var fingerprint = EmailFingerprint.Of(Email, salt);

        Assert.Equal(32, fingerprint.Bytes.Length);
        Assert.Equal(ErasureTestData.Fingerprint(Email, salt.Value), fingerprint.Bytes.ToArray());
    }

    /// <summary>AC-007, VR-004: letter case and surrounding spaces do not change the fingerprint.</summary>
    [Fact]
    public void TheFingerprint_IgnoresLetterCaseAndSurroundingSpaces()
    {
        var salt = ErasureSalt.Generate();

        Assert.Equal(EmailFingerprint.Of(Email, salt), EmailFingerprint.Of("  O.Koval@SCHOOL-ONE.example.TEST ", salt));
    }

    /// <summary>AC-006: two installations' salts give the same email different fingerprints.</summary>
    [Fact]
    public void TwoSalts_GiveTheSameEmailDifferentFingerprints()
    {
        Assert.NotEqual(EmailFingerprint.Of(Email, ErasureSalt.Generate()), EmailFingerprint.Of(Email, ErasureSalt.Generate()));
    }

    /// <summary>Equality compares the bytes, not the buffer.</summary>
    [Fact]
    public void TwoFingerprintsOfOneEmail_AreEqual_AndHashAlike()
    {
        var salt = ErasureSalt.Generate();
        var first = EmailFingerprint.Of(Email, salt);
        var second = EmailFingerprint.Of(Email, salt);

        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, EmailFingerprint.Of("p.melnyk@school-one.example.test", salt));
    }

    /// <summary>S-05, S-06: the fingerprint's text shows nothing of its value.</summary>
    [Fact]
    public void TheFingerprintsText_ShowsNothingOfItsValue()
    {
        var fingerprint = EmailFingerprint.Of(Email, ErasureSalt.Generate());
        var bytes = fingerprint.Bytes.ToArray();

        var text = fingerprint.ToString();

        Assert.DoesNotContain(Convert.ToHexString(bytes), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToBase64String(bytes), text, StringComparison.Ordinal);
        Assert.DoesNotContain(bytes[0].ToString(System.Globalization.CultureInfo.InvariantCulture) + ",", text, StringComparison.Ordinal);
    }

    /// <summary>FR-010: a salt is 32 random bytes; two are different.</summary>
    [Fact]
    public void ASalt_Is32RandomBytes()
    {
        var first = ErasureSalt.Generate();
        var second = ErasureSalt.Generate();

        Assert.Equal(ErasureSalt.Length, first.Value.Length);
        Assert.Equal(32, ErasureSalt.Length);
        Assert.NotEqual(first.Value, second.Value);
    }

    /// <summary>S-05: the salt's text shows nothing of its value.</summary>
    [Fact]
    public void TheSaltsText_ShowsNothingOfItsValue()
    {
        var salt = ErasureSalt.Generate();

        Assert.DoesNotContain(Convert.ToHexString(salt.Value), salt.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToBase64String(salt.Value), salt.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>AC-006, FR-008: the exclusion holds the Google user id, the fingerprint and the date.</summary>
    [Fact]
    public void AnExclusion_HoldsTheGoogleUserId_TheFingerprint_AndTheDate()
    {
        var salt = ErasureSalt.Generate();
        var fingerprint = EmailFingerprint.Of(Email, salt);
        var at = InstallationTestHost.DefaultStart;

        var exclusion = ErasedParticipant.Record("100000000000000041", fingerprint, at);

        Assert.Equal("100000000000000041", exclusion.GoogleUserId);
        Assert.Equal(fingerprint.Bytes.ToArray(), exclusion.EmailFingerprint);
        Assert.Equal(at, exclusion.ErasedAt);
    }

    /// <summary>OD-001 a: an exclusion without fingerprint.</summary>
    [Fact]
    public void AnExclusion_MayHaveNoFingerprint()
    {
        Assert.Null(ErasedParticipant.Record("100000000000000041", null, InstallationTestHost.DefaultStart).EmailFingerprint);
    }

    /// <summary>Entity model §1: an empty, blank or over-long Google user id is rejected.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnExclusion_RejectsABlankGoogleUserId(string googleUserId)
    {
        Assert.ThrowsAny<ArgumentException>(() => ErasedParticipant.Record(googleUserId, null, InstallationTestHost.DefaultStart));
    }

    [Fact]
    public void AnExclusion_RejectsAGoogleUserIdOver64Characters()
    {
        Assert.Equal(64, ErasedParticipant.MaxGoogleUserIdLength);
        Assert.ThrowsAny<ArgumentException>(() => ErasedParticipant.Record(new string('1', 65), null, InstallationTestHost.DefaultStart));
    }

    /// <summary>AC-006, S-05: the only text the exclusion can hold is the Google user id — no name, no email.</summary>
    [Fact]
    public void TheExclusion_HasNoPropertyForANameOrAnEmail()
    {
        var properties = typeof(ErasedParticipant).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(
            [nameof(ErasedParticipant.GoogleUserId)],
            properties.Where(p => p.PropertyType == typeof(string)).Select(p => p.Name));
        Assert.DoesNotContain(properties, p => p.Name.Contains("Name", StringComparison.Ordinal) || p.Name == "Email");
    }

    /// <summary>Entity model §4: each count is non-negative.</summary>
    [Theory]
    [InlineData(-1, 0, 0, 0, 0)]
    [InlineData(0, -1, 0, 0, 0)]
    [InlineData(0, 0, -1, 0, 0)]
    [InlineData(0, 0, 0, -1, 0)]
    [InlineData(0, 0, 0, 0, -1)]
    public void ANegativeCount_IsRejected(int memberships, int submissions, int participations, int organizers, int owners)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ErasureCounts(memberships, submissions, participations, organizers, owners));
    }

    [Fact]
    public void TheCounts_KeepTheirValues()
    {
        var counts = new ErasureCounts(1, 2, 3, 4, 5);

        Assert.Equal((1, 2, 3, 4, 5), (counts.Memberships, counts.Submissions, counts.MeetParticipations, counts.ClearedMeetOrganizers, counts.ClearedCourseOwners));
        Assert.Equal(new ErasureCounts(1, 2, 3, 4, 5), counts);
    }

    /// <summary>VR-002: only an explicit "true", in any letter case, sets the mark.</summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("false", false)]
    [InlineData("on", false)]
    [InlineData("1", false)]
    [InlineData("yes", false)]
    public void OnlyTrue_SetsTheMark(string? value, bool expected)
    {
        Assert.Equal(expected, ErasureMark.IsSet(value));
    }

    /// <summary>AC-009, db-design §9: the erasure row — the Admin, the participant, succeeded, the five counts.</summary>
    [Fact]
    public void TheErasureAuditRow_CarriesTheParticipantIdAndTheCounts()
    {
        var at = InstallationTestHost.DefaultStart;

        var row = AuditEvent.ParticipantErased(7, 41, new ErasureCounts(2, 3, 4, 1, 1), at, "req");

        Assert.Equal(AuditAction.ParticipantErased, row.Action);
        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(7, row.ActorId);
        Assert.Equal(AppRole.Admin, row.ActorRole);
        Assert.Equal(AuditTargetType.ClassroomParticipant, row.TargetType);
        Assert.Equal(41, row.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.RefusalCategory);
        Assert.Equal(at, row.OccurredAt);
        Assert.Equal("req", row.RequestId);
        Assert.Equal((2, 3, 4, 1, 1), (row.ErasedMemberships, row.ErasedSubmissions, row.ErasedMeetParticipations, row.ClearedMeetOrganizers, row.ClearedCourseOwners));
    }

    [Fact]
    public void TheErasureAuditRow_NeedsTheCounts()
    {
        Assert.Throws<ArgumentNullException>(() => AuditEvent.ParticipantErased(7, 41, null!, InstallationTestHost.DefaultStart, null));
    }

    /// <summary>AC-009: the lifting row — the Admin, the exclusion, succeeded, no counts.</summary>
    [Fact]
    public void TheLiftingAuditRow_CarriesTheExclusionId()
    {
        var row = AuditEvent.ErasureExclusionLifted(7, 300, InstallationTestHost.DefaultStart, "req");

        Assert.Equal(AuditAction.ErasureExclusionLifted, row.Action);
        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(7, row.ActorId);
        Assert.Equal(AppRole.Admin, row.ActorRole);
        Assert.Equal(AuditTargetType.ErasedParticipant, row.TargetType);
        Assert.Equal(300, row.TargetId);
        Assert.Equal(AuditOutcome.Succeeded, row.Outcome);
        Assert.Null(row.ErasedMemberships);
        Assert.Null(row.ClearedCourseOwners);
    }

    /// <summary>I-12, db-design §9.4: a refused erasure names the requested participant; a refused lifting names nothing.</summary>
    [Fact]
    public void TheRefusalRows_NameTheParticipantForAnErasureOnly()
    {
        var at = InstallationTestHost.DefaultStart;

        var erasure = AuditEvent.ErasureRefused(AuditAction.ParticipantErased, 7, 41, AuditRefusalCategory.ReadOnlyMode, at, null);
        var lifting = AuditEvent.ErasureRefused(AuditAction.ErasureExclusionLifted, 7, null, AuditRefusalCategory.ReadOnlyMode, at, null);

        Assert.Equal((AuditOutcome.Refused, AuditRefusalCategory.ReadOnlyMode), (erasure.Outcome, erasure.RefusalCategory));
        Assert.Equal((AuditTargetType.ClassroomParticipant, 41L), (erasure.TargetType!.Value, erasure.TargetId!.Value));
        Assert.Null(erasure.ErasedMemberships);
        Assert.Equal((AuditOutcome.Refused, AuditRefusalCategory.ReadOnlyMode), (lifting.Outcome, lifting.RefusalCategory));
        Assert.Null(lifting.TargetType);
        Assert.Null(lifting.TargetId);
        Assert.Equal((AuditActorType.AppUser, AppRole.Admin), (lifting.ActorType, lifting.ActorRole!.Value));
    }

    /// <summary>Entity model §5: only the two actions, only the read-only category, a participant only with an erasure.</summary>
    [Fact]
    public void TheRefusalFactory_RejectsWhatTheDesignDoesNotAllow()
    {
        var at = InstallationTestHost.DefaultStart;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuditEvent.ErasureRefused(AuditAction.DeanAccountCreated, 7, null, AuditRefusalCategory.ReadOnlyMode, at, null));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuditEvent.ErasureRefused(AuditAction.ParticipantErased, 7, 41, AuditRefusalCategory.UnknownLogin, at, null));
        Assert.ThrowsAny<ArgumentException>(() =>
            AuditEvent.ErasureRefused(AuditAction.ErasureExclusionLifted, 7, 41, AuditRefusalCategory.ReadOnlyMode, at, null));
    }
}
