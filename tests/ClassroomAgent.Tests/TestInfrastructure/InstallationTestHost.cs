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
        Settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [InstallationConfigurationKeys.InstallationId] = installationId.ToString("D"),
            [InstallationConfigurationKeys.ControlPlaneAddress] = InstallationConfigurationKeys.ControlPlaneAddressValue,
            [InstallationConfigurationKeys.PrivatePort] = InstallationConfigurationKeys.PrivatePortValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [PushTestData.PrivateAddressKey] = InstallationConfigurationKeys.PrivateAddressValue,
            [InstallationConfigurationKeys.ConnectionString] = connectionString,
            [InstallationConfigurationKeys.LogDirectory] = LogDirectory,
        };
    }

    /// <summary>A whole-second UTC start, so stored <c>timestamptz</c> values compare exactly.</summary>
    public static DateTimeOffset DefaultStart { get; } = new(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);

    public string ConnectionString { get; }

    public Guid InstallationId { get; }

    public ManualTimeProvider Time { get; }

    public FakeControlPlaneClient ControlPlane { get; }

    public string LogDirectory { get; }

    /// <summary>Settings passed to the host; a test may change or remove one before <see cref="Start"/>.</summary>
    public Dictionary<string, string?> Settings { get; }

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
        _factory = new InstallationFactory(Settings, Time, ControlPlane);
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

    public IServiceScope CreateScope() => Factory.Services.CreateScope();

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

    /// <summary>Stops the host and returns every log event written to its log directory.</summary>
    public async Task<IReadOnlyList<LogEvent>> ReadLogEventsAsync(CancellationToken cancellationToken) =>
        LogEvent.Parse(await ReadLogFilesAsync(cancellationToken));

    public async Task<IReadOnlyList<string>> ReadLogFilesAsync(CancellationToken cancellationToken)
    {
        await StopAsync();
        var contents = new List<string>();
        foreach (var file in Directory.EnumerateFiles(LogDirectory, "*", SearchOption.AllDirectories))
        {
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
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
                c.Request.Host = new HostString("localhost", localPort);
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
