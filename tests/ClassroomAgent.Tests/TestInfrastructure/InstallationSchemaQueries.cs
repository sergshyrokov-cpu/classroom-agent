using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Catalogue queries for the installation schema assertions of US-008 db-design 3 and 4 — the same shape as
/// the Control Plane's <c>SchemaQueries</c>, over <see cref="InstallationTestHost"/>.
/// </summary>
public static partial class InstallationSchemaQueries
{
    /// <summary>"name type maxLength nullable default" per column, sorted; "-" for none.</summary>
    public static async Task<IReadOnlyList<string>> ColumnsAsync(
        InstallationTestHost host,
        string table,
        CancellationToken cancellationToken)
    {
        var rows = await host.QueryAsync(
            """
            SELECT column_name, data_type, character_maximum_length, is_nullable, column_default, is_identity
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table
            """,
            r =>
            {
                var identity = r.GetString(5) == "YES";
                var rawDefault = r.IsDBNull(4) ? null : r.GetString(4);
                var columnDefault = identity || rawDefault is null ? "-" : DefaultLiteral().Match(rawDefault).Value;
                var length = r.IsDBNull(2) ? "-" : r.GetInt32(2).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return $"{r.GetString(0)} {r.GetString(1)} {length} {r.GetString(3)} {columnDefault}";
            },
            cancellationToken,
            ("table", table));
        return rows.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>The names of the check constraints on a table, sorted.</summary>
    public static async Task<IReadOnlyList<string>> CheckConstraintsAsync(
        InstallationTestHost host,
        string table,
        CancellationToken cancellationToken)
    {
        var rows = await host.QueryAsync(
            """
            SELECT conname::text FROM pg_constraint
            WHERE conrelid = @table::regclass AND contype = 'c'
            """,
            r => r.GetString(0),
            cancellationToken,
            ("table", table));
        return rows.Order(StringComparer.Ordinal).ToList();
    }

    public static Task<string?> PrimaryKeyAsync(
        InstallationTestHost host,
        string table,
        CancellationToken cancellationToken) =>
        host.ScalarAsync<string>(
            """
            SELECT conname::text FROM pg_constraint
            WHERE conrelid = @table::regclass AND contype = 'p'
            """,
            cancellationToken,
            ("table", table));

    /// <summary>The index definitions of a table, sorted — for "exactly these indexes" assertions (PC-7).</summary>
    public static async Task<IReadOnlyList<string>> IndexNamesAsync(
        InstallationTestHost host,
        string table,
        CancellationToken cancellationToken)
    {
        var rows = await host.QueryAsync(
            "SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename = @table",
            r => r.GetString(0),
            cancellationToken,
            ("table", table));
        return rows.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>The foreign keys of a table; US-008 declares none on either new table (db-design 5).</summary>
    public static async Task<IReadOnlyList<string>> ForeignKeysAsync(
        InstallationTestHost host,
        string table,
        CancellationToken cancellationToken)
    {
        var rows = await host.QueryAsync(
            """
            SELECT conname::text FROM pg_constraint
            WHERE conrelid = @table::regclass AND contype = 'f'
            """,
            r => r.GetString(0),
            cancellationToken,
            ("table", table));
        return rows.Order(StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex(@"^[^:]+")]
    private static partial Regex DefaultLiteral();
}
