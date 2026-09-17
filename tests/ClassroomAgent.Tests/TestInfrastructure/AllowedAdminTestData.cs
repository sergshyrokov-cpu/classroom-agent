namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>Synthetic AllowedAdmin emails for the US-003 tests, in the domain of <see cref="InstallationTestData.Domain"/> (TC-4).</summary>
public static class AllowedAdminTestData
{
    public const string Email = "ivan.petrenko@school-one.example.test";

    public const string OtherEmail = "olena.koval@school-one.example.test";

    public const string ThirdEmail = "admin-2_x@school-one.example.test";

    /// <summary>An email in <see cref="InstallationTestData.OtherDomain"/>.</summary>
    public const string OtherSchoolEmail = "petro.bondar@school-two.example.test";

    public static IReadOnlyList<KeyValuePair<string, string>> AddFields(string email) => [new("email", email)];

    public static string DetailPath(Guid installation) => $"/installations/{installation:D}";

    public static string AddFormPath(Guid installation) => $"/installations/{installation:D}/admins/new";

    public static string AddPath(Guid installation) => $"/installations/{installation:D}/admins";

    public static string RevocationPath(Guid installation, Guid admin) =>
        $"/installations/{installation:D}/admins/{admin:D}/revocation";
}
