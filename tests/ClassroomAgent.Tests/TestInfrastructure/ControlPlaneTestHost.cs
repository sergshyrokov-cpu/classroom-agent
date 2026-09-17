using System.Globalization;
using System.Net;
using ClassroomAgent.ControlPlane.Localization;
using ClassroomAgent.ControlPlane.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// One started Control Plane over its own migrated PostgreSQL database, with helpers for
/// HTTP, raw SQL and translations. Every test builds its own, so no test depends on
/// another's state or on execution order (TC-2).
/// </summary>
public sealed class ControlPlaneTestHost : IAsyncDisposable
{
    private readonly string _root;
    private ControlPlaneFactory? _factory;

    /// <summary>Set on the host that created the database; only that host drops it on disposal.</summary>
    private PostgreSqlFixture? _ownedDatabase;

    private ControlPlaneTestHost(string connectionString, string setupCode)
    {
        var now = DateTimeOffset.UtcNow;
        ConnectionString = connectionString;
        Time = new TestTimeProvider(new DateTimeOffset(now.Ticks - (now.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero));
        CodeGenerator = new FixedSetupCodeGenerator(setupCode);
        _root = Path.Combine(Path.GetTempPath(), "classroom-agent-tests", Guid.NewGuid().ToString("N"));
        KeyDirectory = Directory.CreateDirectory(Path.Combine(_root, "keys")).FullName;
        LogDirectory = Directory.CreateDirectory(Path.Combine(_root, "logs")).FullName;
    }

    public string ConnectionString { get; }

    public TestTimeProvider Time { get; }

    public FixedSetupCodeGenerator CodeGenerator { get; }

    public CapturingOperatorConsole OperatorConsole { get; } = new();

    public string KeyDirectory { get; }

    public string LogDirectory { get; }

    public IServiceProvider Services => Factory.Services;

    private ControlPlaneFactory Factory => _factory ?? throw new InvalidOperationException("The host has been stopped.");

    /// <summary>Creates and migrates a fresh database, then starts the host.</summary>
    public static async Task<ControlPlaneTestHost> StartAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        string setupCode = TestData.SetupCode,
        IReadOnlyDictionary<string, string>? extraSettings = null)
    {
        var connectionString = await database.CreateDatabaseAsync(cancellationToken);
        await MigrateAsync(connectionString, cancellationToken);
        var host = Start(connectionString, setupCode, extraSettings);
        host._ownedDatabase = database;
        return host;
    }

    /// <summary>Starts another host over the same database — a restart of the Control Plane.</summary>
    public static ControlPlaneTestHost Restart(ControlPlaneTestHost previous, string setupCode) =>
        Start(previous.ConnectionString, setupCode);

    public FormClient CreateClient() =>
        new(Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost"),
        }));

    public IServiceScope CreateScope() => Factory.Services.CreateScope();

    /// <summary>An HTTP handler into the in-process Control Plane — for the installation's real client (US-005 contract tests).</summary>
    public HttpMessageHandler CreateServerHandler() => Factory.Server.CreateHandler();

    /// <summary>Runs the first-run setup over HTTP and returns the signed-in client.</summary>
    public async Task<FormClient> CreateOwnerAsync(CancellationToken cancellationToken)
    {
        var client = CreateClient();
        await client.GetAsync("/setup", cancellationToken);
        var response = await client.PostFormAsync("/setup", TestData.SetupFields(CodeGenerator.Code), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/", response.LocationPath);
        return client;
    }

    /// <summary>Opens the sign-in page with a new client and submits the credentials.</summary>
    public async Task<(FormClient Client, PageResponse Response)> SignInAsync(
        string login,
        string password,
        CancellationToken cancellationToken)
    {
        var client = CreateClient();
        await client.GetAsync("/sign-in", cancellationToken);
        var response = await client.PostFormAsync("/sign-in", TestData.SignInFields(login, password), cancellationToken);
        return (client, response);
    }

    /// <summary>The translated text of a key in a culture (<c>uk</c> or <c>en</c>); fails when missing.</summary>
    public string Text(string key, string culture)
    {
        var localizer = Factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var text = localizer[key];
            Assert.False(text.ResourceNotFound, $"Translation key '{key}' is missing for '{culture}'.");
            Assert.False(string.IsNullOrWhiteSpace(text.Value), $"Translation key '{key}' is empty for '{culture}'.");
            return text.Value;
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>The keys and values defined for exactly that culture, parent cultures excluded.</summary>
    public IReadOnlyDictionary<string, string> AllTexts(string culture)
    {
        var localizer = Factory.Services.GetRequiredService<IStringLocalizer<SharedResource>>();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            return localizer.GetAllStrings(includeParentCultures: false)
                .ToDictionary(s => s.Name, s => s.Value, StringComparer.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

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

    public Task<long> OwnerCountAsync(CancellationToken cancellationToken) =>
        ScalarAsync<long>("SELECT count(*) FROM owner", cancellationToken);

    public async Task<OwnerRow?> OwnerAsync(CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            "SELECT id, user_name, normalized_user_name, password_hash, security_stamp, access_failed_count, lockout_end, ui_language FROM owner",
            r => new OwnerRow(
                r.GetInt64(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                r.GetString(4),
                r.GetInt32(5),
                r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6),
                r.GetString(7)),
            cancellationToken);
        return rows.SingleOrDefault();
    }

    public Task<IReadOnlyList<AuditRow>> AuditRowsAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            "SELECT id, occurred_at, actor_type, actor_id, action, target_type, target_id, outcome, refusal_category, request_id, created_at, updated_at FROM audit_event ORDER BY id",
            r => new AuditRow(
                r.GetInt64(0),
                r.GetFieldValue<DateTimeOffset>(1),
                r.GetString(2),
                r.IsDBNull(3) ? null : r.GetInt64(3),
                r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetInt64(6),
                r.GetString(7),
                r.IsDBNull(8) ? null : r.GetString(8),
                r.IsDBNull(9) ? null : r.GetString(9),
                r.GetFieldValue<DateTimeOffset>(10),
                r.GetFieldValue<DateTimeOffset>(11)),
            cancellationToken);

    /// <summary>Every stored audit row as its full JSON text, for "no personal data" checks.</summary>
    public Task<IReadOnlyList<string>> AuditRowsAsJsonAsync(CancellationToken cancellationToken) =>
        QueryAsync("SELECT row_to_json(a)::text FROM audit_event a", r => r.GetString(0), cancellationToken);

    /// <summary>Stops the host, flushing its log sinks. The database and directories stay.</summary>
    public async Task StopAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }
    }

    /// <summary>Stops the host and returns the text of every file in its log directory.</summary>
    public async Task<IReadOnlyList<string>> ReadLogFilesAsync(CancellationToken cancellationToken)
    {
        await StopAsync();
        var contents = new List<string>();
        foreach (var file in Directory.EnumerateFiles(LogDirectory, "*", SearchOption.AllDirectories))
        {
            await using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            contents.Add(await reader.ReadToEndAsync(cancellationToken));
        }

        return contents;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            NpgsqlConnection.ClearPool(connection);
        }

        if (_ownedDatabase is not null)
        {
            // A restarted host over the same database is disposed first (declared later), so the
            // database is dropped only once nothing uses it.
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

    private static ControlPlaneTestHost Start(
        string connectionString,
        string setupCode,
        IReadOnlyDictionary<string, string>? extraSettings = null)
    {
        var host = new ControlPlaneTestHost(connectionString, setupCode);
        var settings = new Dictionary<string, string>
        {
            [ConfigurationKeys.ConnectionString] = connectionString,
            [ConfigurationKeys.DataProtectionKeyDirectory] = host.KeyDirectory,
            [ConfigurationKeys.LogDirectory] = host.LogDirectory,
        };
        foreach (var (key, value) in extraSettings ?? new Dictionary<string, string>())
        {
            settings[key] = value;
        }
        host._factory = new ControlPlaneFactory(settings, host.Time, host.CodeGenerator, host.OperatorConsole);
        _ = host._factory.Server;
        return host;
    }

    private static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        // Migrations carry explicit table and column names, so the provider alone applies
        // them. Drift between the model and the migrations is asserted by MigrationTests.
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var context = new ControlPlaneDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static NpgsqlCommand Command(
        NpgsqlConnection connection,
        string sql,
        (string Name, object? Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }
}
