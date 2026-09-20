using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Configuration;

/// <summary>
/// US-008 AC-001: the installation starts only with its four new required settings — the Data Protection key
/// directory, the public base address, the OAuth client id and the secret-store reference — and a refusal
/// names the offending key, never its value (spec FR-001, VR-001 … VR-004; S-08, S-18; DC-3 v78).
/// </summary>
public sealed class InstallationOAuthSettingsTests(PostgreSqlFixture database)
{
    private static readonly string[] NewRequiredKeys =
    [
        InstallationConfigurationKeys.DataProtectionKeyDirectory,
        InstallationConfigurationKeys.PublicBaseAddress,
        InstallationConfigurationKeys.OAuthClientId,
        InstallationConfigurationKeys.OAuthClientSecretReference,
    ];

    public static TheoryData<string> RequiredKeys => new(NewRequiredKeys);

    [Fact]
    public async Task AllRequiredSettingsValid_HostStarts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        host.Start();

        Assert.NotNull(host.Services);
    }

    [Theory]
    [MemberData(nameof(RequiredKeys))]
    public async Task MissingRequiredSetting_HostDoesNotStart_NamingTheKey(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[key] = null;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(key, ExceptionText(exception), StringComparison.Ordinal);
    }

    /// <summary>VR-001: absolute <c>https</c>, host and optional port, nothing else.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("school-one.example.test")]
    [InlineData("/sign-in")]
    [InlineData("http://school-one.example.test")]
    [InlineData("ftp://school-one.example.test")]
    [InlineData("https://admin:secret@school-one.example.test")]
    [InlineData("https://school-one.example.test/installation")]
    [InlineData("https://school-one.example.test/?lang=uk")]
    [InlineData("https://school-one.example.test/#top")]
    public async Task InvalidPublicBaseAddress_HostDoesNotStart(string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.PublicBaseAddress] = value;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(InstallationConfigurationKeys.PublicBaseAddress, ExceptionText(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://school-one.example.test")]
    [InlineData("https://school-one.example.test/")]
    [InlineData("https://school-one.example.test:9443")]
    [InlineData("https://10.0.0.5/")]
    public async Task BoundaryValidPublicBaseAddress_HostStarts(string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.PublicBaseAddress] = value;

        host.Start();

        Assert.NotNull(host.Services);
    }

    /// <summary>VR-002: both are non-empty after trimming; neither is validated against Google (spec I-5).</summary>
    [Theory]
    [InlineData(InstallationConfigurationKeys.OAuthClientId, "")]
    [InlineData(InstallationConfigurationKeys.OAuthClientId, "   ")]
    [InlineData(InstallationConfigurationKeys.OAuthClientSecretReference, "")]
    [InlineData(InstallationConfigurationKeys.OAuthClientSecretReference, "   ")]
    public async Task BlankOAuthSetting_HostDoesNotStart(string key, string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[key] = value;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(key, ExceptionText(exception), StringComparison.Ordinal);
    }

    /// <summary>
    /// Spec I-5: credentials Google would reject still start the host — a mistyped secret must not take the
    /// school's read-only views down with it. The reference itself resolves (OD-004 makes an unresolvable one a
    /// start-up failure, which <see cref="AReferenceNamingAnAbsentOrEmptySecret_HostDoesNotStart"/> covers); what
    /// is wrong here is the client id and the secret's value.
    /// </summary>
    [Fact]
    public async Task OAuthCredentialsAreNotVerifiedAgainstGoogle_HostStarts()
    {
        var ct = TestContext.Current.CancellationToken;
        var variable = "CA_TEST_OAUTH_SECRET_" + Guid.NewGuid().ToString("N");
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.OAuthClientId] = "not-a-real-client-id";
        host.Settings[InstallationConfigurationKeys.OAuthClientSecretReference] = variable;
        Environment.SetEnvironmentVariable(variable, "not-a-real-client-secret");
        try
        {
            host.Start();

            Assert.NotNull(host.Services);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    /// <summary>VR-003: a path the process can create; a file where a directory belongs is a refusal.</summary>
    [Fact]
    public async Task KeyDirectoryThatIsAFile_HostDoesNotStart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        var file = Path.Combine(Path.GetTempPath(), "classroom-agent-tests", Guid.NewGuid().ToString("N") + ".not-a-directory");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, "occupied", ct);
        host.Settings[InstallationConfigurationKeys.DataProtectionKeyDirectory] = file;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(
            InstallationConfigurationKeys.DataProtectionKeyDirectory,
            ExceptionText(exception),
            StringComparison.Ordinal);
    }

    /// <summary>AC-001, S-18: the key ring is written to the configured directory, not held in memory.</summary>
    [Fact]
    public async Task KeyRing_IsPersistedToTheConfiguredDirectory()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Start();
        using var client = host.CreateClient();

        await client.GetAsync(SignInTestData.SignInPath, ct);

        Assert.True(Directory.Exists(host.KeyDirectory), $"The key directory '{host.KeyDirectory}' was not created.");
        Assert.NotEmpty(Directory.GetFiles(host.KeyDirectory, "*.xml", SearchOption.AllDirectories));
    }

    /// <summary>AC-001, S-18: the key ring is not in the database — no table and no row holds it.</summary>
    [Fact]
    public async Task KeyRing_IsNotInTheDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Start();
        using var client = host.CreateClient();
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var tables = await host.TableNamesAsync(ct);

        Assert.DoesNotContain(tables, t => t.Contains("key", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tables, t => t.Contains("dataprotection", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>VR-004: the school default is optional and Ukrainian when unset.</summary>
    [Fact]
    public async Task DefaultLanguageIsOptional_HostStartsWithoutIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        Assert.False(host.Settings.ContainsKey(InstallationConfigurationKeys.DefaultLanguage));

        host.Start();

        Assert.NotNull(host.Services);
    }

    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    [InlineData("UK")]
    [InlineData("En")]
    public async Task ValidDefaultLanguage_HostStarts(string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.DefaultLanguage] = value;

        host.Start();

        Assert.NotNull(host.Services);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ru")]
    [InlineData("uk-UA")]
    [InlineData("ukrainian")]
    public async Task InvalidDefaultLanguage_HostDoesNotStart(string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.DefaultLanguage] = value;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(InstallationConfigurationKeys.DefaultLanguage, ExceptionText(exception), StringComparison.Ordinal);
    }

    /// <summary>AC-001, SC-10: a refusal names the key and never prints the value — client id and reference included.</summary>
    [Theory]
    [InlineData(InstallationConfigurationKeys.PublicBaseAddress, "http://forbidden-scheme.example.test")]
    [InlineData(InstallationConfigurationKeys.DefaultLanguage, "klingon")]
    public async Task Refusal_NamesTheKeyAndNotTheValue(string key, string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[key] = value;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        var text = ExceptionText(exception);
        Assert.Contains(key, text, StringComparison.Ordinal);
        Assert.DoesNotContain(value, text, StringComparison.Ordinal);
    }

    /// <summary>AC-001, S-08, SC-10: neither the client id nor the secret reference reaches the log.</summary>
    [Fact]
    public async Task ClientIdAndSecretReference_AreNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Start();
        using var client = host.CreateClient();
        await client.GetAsync(SignInTestData.SignInPath, ct);

        var files = await host.ReadLogFilesAsync(ct);

        var log = string.Join("\n", files);
        Assert.DoesNotContain(InstallationConfigurationKeys.OAuthClientIdValue, log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InstallationConfigurationKeys.OAuthClientSecretReferenceValue, log, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC-001, S-08, OD-004 (option 1): the reference names an environment variable, and the secret it holds is
    /// what the Google handler is configured with. The variable name is unique per test, so nothing collides with
    /// a test running beside it.
    /// </summary>
    [Fact]
    public async Task TheSecret_IsResolvedFromTheNamedEnvironmentVariable()
    {
        var ct = TestContext.Current.CancellationToken;
        var variable = "CA_TEST_OAUTH_SECRET_" + Guid.NewGuid().ToString("N");
        const string secret = "a-synthetic-client-secret-no-project-has";
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.OAuthClientSecretReference] = variable;
        Environment.SetEnvironmentVariable(variable, secret);
        try
        {
            host.Start();

            Assert.Equal(secret, GoogleSignInStub.ClientSecretOf(host.Services));
            Assert.Equal(InstallationConfigurationKeys.OAuthClientIdValue, GoogleSignInStub.ClientIdOf(host.Services));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    /// <summary>AC-001, OD-004: a reference naming a variable that is absent or empty stops the start (fail fast).</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AReferenceNamingAnAbsentOrEmptySecret_HostDoesNotStart(string? value)
    {
        var ct = TestContext.Current.CancellationToken;
        var variable = "CA_TEST_OAUTH_SECRET_" + Guid.NewGuid().ToString("N");
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.OAuthClientSecretReference] = variable;
        Environment.SetEnvironmentVariable(variable, value);
        try
        {
            var exception = Assert.ThrowsAny<Exception>(host.Start);

            Assert.Contains(
                InstallationConfigurationKeys.OAuthClientSecretReference,
                ExceptionText(exception),
                StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    /// <summary>AC-001, S-08, SC-10: the secret's own value never reaches a log, a start-up refusal included.</summary>
    [Fact]
    public async Task TheSecretValue_IsNeverLogged()
    {
        var ct = TestContext.Current.CancellationToken;
        var variable = "CA_TEST_OAUTH_SECRET_" + Guid.NewGuid().ToString("N");
        const string secret = "canary-client-secret-must-never-be-logged";
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.OAuthClientSecretReference] = variable;
        Environment.SetEnvironmentVariable(variable, secret);
        try
        {
            host.Start();
            using var client = host.CreateClient();
            await client.StartGoogleSignInAsync(ct);

            var files = await host.ReadLogFilesAsync(ct);

            var log = string.Join("\n", files);
            Assert.DoesNotContain(secret, log, StringComparison.Ordinal);
            Assert.DoesNotContain(variable, log, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    /// <summary>AC-001, S-08: no configuration key carries the secret itself — only a reference to it (SC-7).</summary>
    [Fact]
    public async Task TheSecretItself_IsNotAConfigurationSetting()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        Assert.DoesNotContain(
            host.Settings.Keys,
            key => key.Equals("GoogleOAuth:ClientSecret", StringComparison.OrdinalIgnoreCase));
        host.Start();

        Assert.NotNull(host.Services);
    }

    private static string ExceptionText(Exception exception)
    {
        var text = new System.Text.StringBuilder();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            text.AppendLine(current.Message);
        }

        return text.ToString();
    }
}
