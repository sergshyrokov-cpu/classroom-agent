using ClassroomAgent.ControlPlane.Services;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Services;

/// <summary>AC-007, OD-005: setup-code format and the forgiving comparison.</summary>
public sealed class SetupCodeTests
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    [Theory]
    [InlineData("01234-56789-ABCD-EFGH-JKMN-PQRS", true)]
    [InlineData("01234-56789-abcd-efgh-jkmn-pqrs", true)]
    [InlineData("0123456789ABCDEFGHJKMNPQRS", true)]
    [InlineData("0123456789abcdefghjkmnpqrs", true)]
    [InlineData("01234 56789 ABCD EFGH JKMN PQRS", true)]
    [InlineData(" 01234-56789-ABCD-EFGH-JKMN-PQRS ", true)]
    [InlineData("01234-56789-ABCD-EFGH-JKMN-PQRT", false)]
    [InlineData("01234-56789-ABCD-EFGH-JKMN", false)]
    [InlineData("01234-56789-ABCD-EFGH-JKMN-PQRSX", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Code_IgnoresHyphensSpacesAndCase(string? submitted, bool expected)
    {
        Assert.Equal(expected, SetupCodeComparer.Matches(TestData.SetupCode, submitted));
    }

    [Fact]
    public void ProductionGenerator_Produces26CrockfordBase32Characters()
    {
        var generator = new SetupCodeGenerator();

        var first = generator.Generate();
        var second = generator.Generate();

        foreach (var code in new[] { first, second })
        {
            var groups = code.Split('-');
            Assert.True(groups.Length > 1, code);
            Assert.All(groups, g => Assert.InRange(g.Length, 4, 5));
            var characters = string.Concat(groups);
            Assert.Equal(26, characters.Length);
            Assert.All(characters, c => Assert.Contains(c, CrockfordAlphabet));
        }

        Assert.NotEqual(first, second);
    }
}
