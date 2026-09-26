using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-012 AC-004: the one password policy of SC-2, applied wherever a password is set (spec FR-005, VR-002).
/// The boundaries are the policy: 15 to 128 characters counted in characters, no composition rule, and the
/// containment rule only for a login or local part of at least 4 characters (SC-2 v65).
/// </summary>
public sealed class DeanPasswordPolicyTests
{
    private const string Login = DeanAccountTestData.DeanEmail;

    /// <summary>AC-004: one character short of the minimum is refused.</summary>
    [Fact]
    public void FourteenCharacters_AreTooShort() =>
        Assert.Equal(
            PasswordPolicyViolation.TooShort,
            DeanPasswordPolicy.Check(DeanAccountTestData.TooShortPassword, Login));

    /// <summary>AC-004: exactly the minimum is accepted — the boundary belongs to the allowed side.</summary>
    [Fact]
    public void FifteenCharacters_AreAccepted() =>
        Assert.Null(DeanPasswordPolicy.Check(DeanAccountTestData.ShortestAllowedPassword, Login));

    /// <summary>AC-004: exactly the maximum is accepted.</summary>
    [Fact]
    public void OneHundredAndTwentyEightCharacters_AreAccepted() =>
        Assert.Null(DeanPasswordPolicy.Check(new string('x', 128), Login));

    /// <summary>AC-004: one character past the maximum is refused.</summary>
    [Fact]
    public void OneHundredAndTwentyNineCharacters_AreTooLong() =>
        Assert.Equal(PasswordPolicyViolation.TooLong, DeanPasswordPolicy.Check(new string('x', 129), Login));

    /// <summary>
    /// AC-004: the length is counted in **characters, not bytes** (spec FR-005). Fifteen non-ASCII characters
    /// are fifteen characters; a byte count would make this pass at fourteen and fail here.
    /// </summary>
    [Fact]
    public void FifteenNonAsciiCharacters_AreAccepted() =>
        Assert.Null(DeanPasswordPolicy.Check(new string('\u0449', 15), Login));

    /// <summary>AC-004: fourteen non-ASCII characters are still too short.</summary>
    [Fact]
    public void FourteenNonAsciiCharacters_AreTooShort() =>
        Assert.Equal(PasswordPolicyViolation.TooShort, DeanPasswordPolicy.Check(new string('\u0449', 14), Login));

    /// <summary>AC-004: spaces are allowed and are not trimmed away (spec FR-005).</summary>
    [Fact]
    public void FifteenSpaces_AreAccepted() =>
        Assert.Null(DeanPasswordPolicy.Check(new string(' ', 15), Login));

    /// <summary>
    /// AC-004: there is **no composition rule** — no required digit, upper-case letter or symbol. A password of
    /// fifteen identical lower-case letters is acceptable, and a test that demanded more would be inventing a
    /// requirement (spec FR-005, S-06).
    /// </summary>
    [Fact]
    public void NoCompositionRule_IsApplied() =>
        Assert.Null(DeanPasswordPolicy.Check("aaaaaaaaaaaaaaa", Login));

    /// <summary>AC-004: the password may not equal the login, compared case-insensitively.</summary>
    [Theory]
    [InlineData(DeanAccountTestData.DeanEmail)]
    [InlineData(DeanAccountTestData.DeanEmailMixedCase)]
    public void APasswordEqualToTheLogin_IsRefused(string password) =>
        Assert.Equal(PasswordPolicyViolation.EqualsLogin, DeanPasswordPolicy.Check(password, Login));

    /// <summary>AC-004: the password may not contain the login.</summary>
    [Fact]
    public void APasswordContainingTheLogin_IsRefused() =>
        Assert.Equal(
            PasswordPolicyViolation.ContainsLogin,
            DeanPasswordPolicy.Check("prefix " + DeanAccountTestData.DeanEmail, Login));

    /// <summary>AC-004: it may not contain the local part of the email either, case-insensitively.</summary>
    [Fact]
    public void APasswordContainingTheLocalPart_IsRefused() =>
        Assert.Equal(
            PasswordPolicyViolation.ContainsLogin,
            DeanPasswordPolicy.Check("my DEAN password is long", Login));

    /// <summary>
    /// AC-004: the containment rule applies at exactly four characters — the boundary SC-2 v65 fixes, so a
    /// four-character local part is checked for containment.
    /// </summary>
    [Fact]
    public void AFourCharacterLocalPart_IsCheckedForContainment() =>
        Assert.Equal(
            PasswordPolicyViolation.ContainsLogin,
            DeanPasswordPolicy.Check("carries abcd inside it", "abcd@school-one.example.test"));

    /// <summary>
    /// AC-004: a local part of three characters is compared for **equality only** — otherwise the rule would
    /// forbid almost any password (SC-2 v65).
    /// </summary>
    [Fact]
    public void AThreeCharacterLocalPart_IsNotCheckedForContainment() =>
        Assert.Null(DeanPasswordPolicy.Check("carries abc inside it", "abc@school-one.example.test"));

    /// <summary>AC-004: and that three-character local part is still refused when the password equals it.</summary>
    [Fact]
    public void AShortLoginIsStillRefusedWhenThePasswordEqualsIt() =>
        Assert.Equal(
            PasswordPolicyViolation.EqualsLogin,
            DeanPasswordPolicy.Check("abc@school-one.example.test", "abc@school-one.example.test"));

    /// <summary>AC-004: an ordinary long password unrelated to the login is accepted.</summary>
    [Fact]
    public void AnUnrelatedLongPassword_IsAccepted() =>
        Assert.Null(DeanPasswordPolicy.Check(DeanAccountTestData.NewPassword, Login));
}
