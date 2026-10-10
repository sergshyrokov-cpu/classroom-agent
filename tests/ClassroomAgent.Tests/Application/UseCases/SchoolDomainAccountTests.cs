using ClassroomAgent.Domain.Rules;
using static ClassroomAgent.Tests.TestInfrastructure.MeetTestData;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-031 spec FR-006, OD-005, AC-004, AC-005: an email is a domain account's when the part after its last <c>@</c>
/// equals the school's domain exactly, compared case-insensitively. A subdomain, another domain, a lookalike suffix
/// and a value with no <c>@</c> are not.
/// </summary>
public sealed class SchoolDomainAccountTests
{
    [Theory]
    [InlineData("teacher1@" + SchoolDomain)]
    [InlineData("TEACHER1@SCHOOL-ONE.EXAMPLE.TEST")]
    [InlineData("teacher1@School-One.Example.Test")]
    [InlineData("odd@local@" + SchoolDomain)]
    public void AnAddressOfTheSchoolsDomain_IsADomainAccount(string email) =>
        Assert.True(SchoolDomainAccount.IsDomainAccount(email, SchoolDomain));

    /// <summary>The comparison is case-insensitive on the stored domain's side too.</summary>
    [Fact]
    public void TheSchoolsDomainInOtherCase_StillMatches() =>
        Assert.True(SchoolDomainAccount.IsDomainAccount("teacher1@" + SchoolDomain, SchoolDomain.ToUpperInvariant()));

    [Theory]
    [InlineData("teacher1@" + Subdomain)]
    [InlineData("teacher1@" + OtherDomain)]
    [InlineData("teacher1@x" + SchoolDomain)]
    [InlineData("teacher1@" + SchoolDomain + ".evil.test")]
    [InlineData("teacher1@example.test")]
    [InlineData(SchoolDomain)]
    [InlineData("teacher1@")]
    [InlineData("")]
    [InlineData(null)]
    public void AnyOtherValue_IsNotADomainAccount(string? email) =>
        Assert.False(SchoolDomainAccount.IsDomainAccount(email, SchoolDomain));
}
