using System.Net;
using ClassroomAgent.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// One installation host over its own migrated PostgreSQL database (TC-2), with a manual clock and a
/// scripted Control Plane client (US-005 test strategy §3). A test creates the host, may seed the
/// database and script replies, then starts it — the legitimacy check begins at start (FR-003).
/// </summary>
public sealed class InstallationTestHost : IAsyncDisposable
{
    private readonly string _root;
    private InstallationFactory? _factory;
    private PostgreSqlFixture? _ownedDatabase;

    private InstallationTestHost(string connectionString, Guid installationId, DateTimeOffset now)
    {
        ConnectionString = connectionString;
        InstallationId = installationId;
        Time = new ManualTimeProvider(now);
        ControlPlane = new FakeControlPlaneClient(Time);
        _root = Path.Combine(Path.GetTempPath(), "classroom-agent-tests", Guid.NewGuid().ToString("N"));
        LogDirectory = Directory.CreateDirectory(Path.Combine(_root, "logs")).FullName;
        KeyDirectory = Path.Combine(_root, "keys");
        InstallationConfigurationKeys.PlaceDefaultOAuthSecret();
        Settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [InstallationConfigurationKeys.InstallationId] = installationId.ToString("D"),
            [InstallationConfigurationKeys.ControlPlaneAddress] = InstallationConfigurationKeys.ControlPlaneAddressValue,
            [InstallationConfigurationKeys.PrivatePort] = InstallationConfigurationKeys.PrivatePortValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [PushTestData.PrivateAddressKey] = InstallationConfigurationKeys.PrivateAddressValue,
            [InstallationConfigurationKeys.ConnectionString] = connectionString,
            [InstallationConfigurationKeys.LogDirectory] = LogDirectory,

            // US-008 spec FR-001: the four settings this Story makes required. The key directory is not
            // created here — VR-003 requires the host to create it when absent.
            [InstallationConfigurationKeys.DataProtectionKeyDirectory] = KeyDirectory,
            [InstallationConfigurationKeys.PublicBaseAddress] = InstallationConfigurationKeys.PublicBaseAddressValue,
            [InstallationConfigurationKeys.OAuthClientId] = InstallationConfigurationKeys.OAuthClientIdValue,
            [InstallationConfigurationKeys.OAuthClientSecretReference] = InstallationConfigurationKeys.OAuthClientSecretReferenceValue,
        };
    }

    /// <summary>A whole-second UTC start, so stored <c>timestamptz</c> values compare exactly.</summary>
    public static DateTimeOffset DefaultStart { get; } = new(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);

    public string ConnectionString { get; }

    public Guid InstallationId { get; }

    public ManualTimeProvider Time { get; }

    public FakeControlPlaneClient ControlPlane { get; }

    public string LogDirectory { get; }

    /// <summary>The Data Protection key ring directory of US-008 spec FR-001; the host creates it (VR-003).</summary>
    public string KeyDirectory { get; }

    /// <summary>Settings passed to the host; a test may change or remove one before <see cref="Start"/>.</summary>
    public Dictionary<string, string?> Settings { get; }

    /// <summary>Extra test-only registrations, applied last (US-007 test strategy §3); set before <see cref="Start"/>.</summary>
    public Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? ConfigureServices { get; set; }

    /// <summary>
    /// US-008: when set before <see cref="Start"/>, the real <c>ControlPlaneClient</c> runs over this
    /// transport instead of the substituted port, so the reply classification of api-design 2.4 is proven
    /// on production code (test strategy 2.2).
    /// </summary>
    public HttpMessageHandler? ControlPlaneHandler { get; set; }

    /// <summary>US-008: the real Google handler, driven offline. Call <see cref="UseGoogleStub"/> after starting.</summary>
    public GoogleSignInStub Google { get; } = new();

    public IServiceProvider Services => Factory.Services;

    private InstallationFactory Factory => _factory ?? throw new InvalidOperationException("The host is not started.");

    /// <summary>Creates a database, applies the installation migrations (unless told not to) and prepares a host.</summary>
    public static async Task<InstallationTestHost> CreateAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        bool migrate = true,
        DateTimeOffset? now = null)
    {
        var connectionString = await database.CreateDatabaseAsync(cancellationToken);
        if (migrate)
        {
            await MigrateAsync(connectionString, cancellationToken);
        }

        return new InstallationTestHost(connectionString, Guid.NewGuid(), now ?? DefaultStart) { _ownedDatabase = database };
    }

    /// <summary>Creates, migrates and starts in one step.</summary>
    public static async Task<InstallationTestHost> StartAsync(PostgreSqlFixture database, CancellationToken cancellationToken)
    {
        var host = await CreateAsync(database, cancellationToken);
        host.Start();
        return host;
    }

    /// <summary>A new host over the same database and installation id — a restart (AC-011).</summary>
    public static InstallationTestHost Restart(InstallationTestHost previous, DateTimeOffset now) =>
        new(previous.ConnectionString, previous.InstallationId, now);

    /// <summary>Builds and starts the host; throws when the host refuses to start (AC-001).</summary>
    public void Start()
    {
        _factory = new InstallationFactory(Settings, Time, ControlPlane, ConfigureServices, ControlPlaneHandler);
        try
        {
            _ = _factory.Server;
        }
        catch
        {
            _factory.Dispose();
            _factory = null;
            throw;
        }
    }

    /// <summary>Starts the host with the real Control Plane client over a scripted transport (US-008).</summary>
    public static async Task<InstallationTestHost> StartWithControlPlaneHttpAsync(
        PostgreSqlFixture database,
        HttpMessageHandler handler,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly)
    {
        var host = await CreateAsync(database, cancellationToken);
        await ReadOnlyModeHost.SeedAsync(host, cause, cancellationToken);
        host.ControlPlaneHandler = handler;
        host.Start();
        return host;
    }

    /// <summary>Points the started host's Google handler at <see cref="Google"/> (US-008 test strategy 2.2).</summary>
    public InstallationTestHost UseGoogleStub()
    {
        Google.Apply(Services);
        return this;
    }

    public IServiceScope CreateScope() => Factory.Services.CreateScope();

    /// <summary>A browser-like client on the public port: keeps cookies, remembers the antiforgery token, follows nothing.</summary>
    public FormClient CreateClient() =>
        new(Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost"),
        }));

    /// <summary>
    /// The translated text of a key in a culture, resolved the way the host resolves it — by resource base
    /// name and assembly, so no marker type is named here (US-008 test strategy 2.1). Fails when missing.
    /// </summary>
    public string Text(string key, string culture)
    {
        var localizer = Localizer();
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
            var text = localizer[key];
            Assert.False(text.ResourceNotFound, $"Translation key '{key}' is missing for '{culture}'.");
            Assert.False(string.IsNullOrWhiteSpace(text.Value), $"Translation key '{key}' is empty for '{culture}'.");
            return text.Value;
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>The keys and values defined for exactly that culture, parent cultures excluded.</summary>
    public IReadOnlyDictionary<string, string> AllTexts(string culture)
    {
        var localizer = Localizer();
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
            return localizer.GetAllStrings(includeParentCultures: false)
                .ToDictionary(s => s.Name, s => s.Value, StringComparer.Ordinal);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    public Task<IReadOnlyList<AppUserRow>> AppUsersAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT id, email, normalized_email, role, sign_in_method, password_hash, access_failed_count,
                   lockout_end, ui_language, is_disabled, last_successful_sign_in_at, created_at, updated_at
            FROM app_user ORDER BY id
            """,
            r => new AppUserRow(
                r.GetInt64(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.GetInt32(6),
                r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7),
                r.GetString(8),
                r.GetBoolean(9),
                r.IsDBNull(10) ? null : r.GetFieldValue<DateTimeOffset>(10),
                r.GetFieldValue<DateTimeOffset>(11),
                r.GetFieldValue<DateTimeOffset>(12)),
            cancellationToken);

    public Task<IReadOnlyList<InstallationAuditRow>> AuditRowsAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT id, occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                   outcome, refusal_category, request_id, created_at, updated_at
            FROM audit_event ORDER BY id
            """,
            r => new InstallationAuditRow(
                r.GetInt64(0),
                r.GetFieldValue<DateTimeOffset>(1),
                r.GetString(2),
                r.IsDBNull(3) ? null : r.GetInt64(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                r.IsDBNull(7) ? null : r.GetInt64(7),
                r.GetString(8),
                r.IsDBNull(9) ? null : r.GetString(9),
                r.IsDBNull(10) ? null : r.GetString(10),
                r.GetFieldValue<DateTimeOffset>(11),
                r.GetFieldValue<DateTimeOffset>(12)),
            cancellationToken);

    /// <summary>Every stored audit row as its full JSON text, for "no personal data" checks (SC-11, S-13).</summary>
    public Task<IReadOnlyList<string>> AuditRowsAsJsonAsync(CancellationToken cancellationToken) =>
        QueryAsync("SELECT row_to_json(a)::text FROM audit_event a", r => r.GetString(0), cancellationToken);

    /// <summary>Seeds an <c>app_user</c> row directly — for the "revoked Admin keeps the row" scenarios (AC-008).</summary>
    public Task<int> InsertAppUserAsync(
        CancellationToken cancellationToken,
        string email = SignInTestData.AdminEmail,
        string role = "admin",
        string signInMethod = "google",
        string? passwordHash = null,
        string uiLanguage = "uk",
        bool isDisabled = false,
        DateTimeOffset? lastSuccessfulSignInAt = null,
        DateTimeOffset? stampedAt = null) =>
        ExecuteAsync(
            """
            INSERT INTO app_user (email, normalized_email, role, sign_in_method, password_hash, security_stamp,
                                  concurrency_stamp, access_failed_count, ui_language, is_disabled,
                                  last_successful_sign_in_at, created_at, updated_at)
            VALUES (@email, @email, @role, @signInMethod, @passwordHash, @securityStamp, @concurrencyStamp,
                    0, @uiLanguage, @isDisabled, @lastSignIn, @stamp, @stamp)
            """,
            cancellationToken,
            ("email", email.ToLowerInvariant()),
            ("role", role),
            ("signInMethod", signInMethod),
            ("passwordHash", passwordHash),
            ("securityStamp", Guid.NewGuid().ToString("N")),
            ("concurrencyStamp", Guid.NewGuid().ToString("N")),
            ("uiLanguage", uiLanguage),
            ("isDisabled", isDisabled),
            ("lastSignIn", lastSuccessfulSignInAt),
            ("stamp", stampedAt ?? Time.GetUtcNow()));

    /// <summary>The installation's tables, for "nothing else was created" assertions.</summary>
    public Task<IReadOnlyList<string>> TableNamesAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name",
            r => r.GetString(0),
            cancellationToken);

    /// <summary>Sends a request as if it arrived on the private port (DC-6, TC-5).</summary>
    public Task<RawResponse> SendPrivateAsync(
        string method,
        string path,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null,
        string? contentType = null) =>
        SendAsync(method, path, InstallationConfigurationKeys.PrivatePortValue, cancellationToken, headers, body, contentType);

    /// <summary>Sends a request as if it arrived on the public HTTPS port.</summary>
    public Task<RawResponse> SendPublicAsync(
        string method,
        string path,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null,
        string? contentType = null) =>
        SendAsync(method, path, 443, cancellationToken, headers, body, contentType);

    /// <summary>Sends a plain-HTTP request to the public port — for the HTTPS redirection of AC-013.</summary>
    public async Task<RawResponse> SendPublicHttpAsync(string method, string path, CancellationToken cancellationToken)
    {
        var context = await Factory.Server.SendAsync(
            c =>
            {
                c.Request.Method = method;
                c.Request.Scheme = "http";
                c.Request.Host = new HostString("school-one.example.test", 80);
                c.Request.Path = path;
                c.Connection.LocalPort = 80;
            },
            cancellationToken);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var headers = context.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        return new RawResponse((HttpStatusCode)context.Response.StatusCode, context.Response.ContentType, body, headers);
    }

    /// <summary>Waits until the check finished and scheduled the next one at that instant.</summary>
    public Task WaitForNextCheckAtAsync(DateTimeOffset dueAt, CancellationToken cancellationToken) =>
        Time.WaitForTimerAtAsync(dueAt, cancellationToken);

    public async Task<int> ExecuteAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = Command(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<T?> ScalarAsync<T>(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = Command(connection, sql, parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? default : (T)value;
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = Command(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    public Task<IReadOnlyList<LegitimacyStateRow>> LegitimacyStatesAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            "SELECT id, singleton, last_successful_check_at, status, compatibility, domain, client_id, created_at, updated_at FROM legitimacy_state ORDER BY id",
            r => new LegitimacyStateRow(
                r.GetInt64(0),
                r.GetBoolean(1),
                r.IsDBNull(2) ? null : r.GetFieldValue<DateTimeOffset>(2),
                r.GetString(3),
                r.GetString(4),
                r.GetString(5),
                r.GetString(6),
                r.GetFieldValue<DateTimeOffset>(7),
                r.GetFieldValue<DateTimeOffset>(8)),
            cancellationToken);

    /// <summary>Seeds the single <c>legitimacy_state</c> row directly.</summary>
    public Task<int> InsertLegitimacyStateAsync(
        CancellationToken cancellationToken,
        DateTimeOffset? lastSuccessfulCheckAt,
        string status = "active",
        string compatibility = "supported",
        string domain = InstallationTestData.Domain,
        string clientId = InstallationTestData.ClientId,
        DateTimeOffset? stampedAt = null) =>
        ExecuteAsync(
            """
            INSERT INTO legitimacy_state (last_successful_check_at, status, compatibility, domain, client_id, created_at, updated_at)
            VALUES (@last, @status, @compatibility, @domain, @clientId, @stamp, @stamp)
            """,
            cancellationToken,
            ("last", lastSuccessfulCheckAt),
            ("status", status),
            ("compatibility", compatibility),
            ("domain", domain),
            ("clientId", clientId),
            ("stamp", stampedAt ?? lastSuccessfulCheckAt ?? Time.GetUtcNow()));

    /// <summary>Stops the host, flushing its log sinks.</summary>
    public async Task StopAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }
    }

    /// <summary>
    /// Stops the host and returns every log event written to its log directory. Use
    /// <see cref="WaitForLogEventAsync"/> instead when the line is written by a background activity, which the
    /// stop would cancel before it logs.
    /// </summary>
    public async Task<IReadOnlyList<LogEvent>> ReadLogEventsAsync(CancellationToken cancellationToken) =>
        LogEvent.Parse(await ReadLogFilesAsync(cancellationToken));

    public async Task<IReadOnlyList<string>> ReadLogFilesAsync(CancellationToken cancellationToken)
    {
        await StopAsync();
        return await HostLogs.ReadFilesAsync(LogDirectory, cancellationToken);
    }

    /// <summary>
    /// Waits until the running host has logged that event the expected number of times, then returns every
    /// event written so far.
    /// </summary>
    public Task<IReadOnlyList<LogEvent>> WaitForLogEventAsync(
        string eventName,
        CancellationToken cancellationToken,
        int count = 1) =>
        HostLogs.WaitForEventAsync(LogDirectory, eventName, count, cancellationToken);

    /// <summary>The text of every log file of the running host, without stopping it.</summary>
    public Task<IReadOnlyList<string>> ReadLogFilesWhileRunningAsync(CancellationToken cancellationToken) =>
        HostLogs.ReadFilesAsync(LogDirectory, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            NpgsqlConnection.ClearPool(connection);
        }

        if (_ownedDatabase is not null)
        {
            await _ownedDatabase.DropDatabaseAsync(ConnectionString, CancellationToken.None);
        }

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A sink may still hold a file open on Windows; the directory is in the temp area.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    private async Task<RawResponse> SendAsync(
        string method,
        string path,
        int localPort,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? headers,
        string? body = null,
        string? contentType = null)
    {
        var context = await Factory.Server.SendAsync(
            c =>
            {
                c.Request.Method = method;
                c.Request.Scheme = localPort == InstallationConfigurationKeys.PrivatePortValue ? "http" : "https";

                // The public port is addressed by the school's own host: the HSTS middleware skips localhost by
                // design, and US-008 AC-013 asks for the header the school's visitors would receive.
                c.Request.Host = localPort == InstallationConfigurationKeys.PrivatePortValue
                    ? new HostString("localhost", localPort)
                    : new HostString("school-one.example.test", localPort);
                c.Request.Path = path;
                c.Connection.LocalPort = localPort;
                if (body is not null)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(body);
                    c.Request.Body = new MemoryStream(bytes);
                    c.Request.ContentLength = bytes.Length;
                    c.Request.ContentType = contentType ?? "application/json; charset=utf-8";
                }

                foreach (var (name, value) in headers ?? new Dictionary<string, string>())
                {
                    c.Request.Headers[name] = value;
                }
            },
            cancellationToken);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync(cancellationToken);
        var responseHeaders = context.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        return new RawResponse((HttpStatusCode)context.Response.StatusCode, context.Response.ContentType, responseBody, responseHeaders);
    }

    /// <summary>
    /// The installation's shared translations, addressed by resource base name and assembly rather than by a
    /// marker type: <c>package-map.md</c> places them in <c>Application.Localization</c>, and naming the type
    /// here would mean this stage creating it (US-008 test strategy 2.1).
    /// </summary>
    private Microsoft.Extensions.Localization.IStringLocalizer Localizer() =>
        Factory.Services.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizerFactory>()
            .Create("Localization.SharedResource", "ClassroomAgent.Application");

    private static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<ClassroomAgentDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var context = new ClassroomAgentDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static NpgsqlCommand Command(NpgsqlConnection connection, string sql, (string Name, object? Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }

    /// <summary>A response read straight off the test server.</summary>
    public sealed record RawResponse(
        HttpStatusCode Status,
        string? ContentType,
        string Body,
        IReadOnlyDictionary<string, string> Headers);
}
