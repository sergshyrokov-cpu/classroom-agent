using Npgsql;
using Testcontainers.PostgreSql;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// One PostgreSQL container for the test assembly; every test gets its own
/// database on it (TC-2). A container that fails to start is an environment
/// failure — there is no fallback provider.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    // Every test runs its own host and database, and tests run in parallel.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithCommand("-c", "max_connections=1000")
        .Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Creates an empty database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        var name = "cp_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
    }
}
