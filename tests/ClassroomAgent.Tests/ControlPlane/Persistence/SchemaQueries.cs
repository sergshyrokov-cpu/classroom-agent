using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Persistence;

/// <summary>Catalogue queries for the schema assertions of db-design §3 and §4.</summary>
public static partial class SchemaQueries
{
    /// <summary>"name type maxLength nullable default" per column, sorted; "-" for none; defaults reduced to their literal.</summary>
    public static async Task<IReadOnlyList<string>> ColumnsAsync(
        ControlPlaneTestHost host,
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

    public static Task<string?> PrimaryKeyAsync(
        ControlPlaneTestHost host,
        string table,
        string constraint,
        CancellationToken cancellationToken) =>
        host.ScalarAsync<string>(
            """
            SELECT string_agg(k.column_name, ',')
            FROM information_schema.table_constraints c
            JOIN information_schema.key_column_usage k ON k.constraint_name = c.constraint_name
            WHERE c.table_name = @table AND c.constraint_name = @constraint AND c.constraint_type = 'PRIMARY KEY'
            """,
            cancellationToken,
            ("table", table),
            ("constraint", constraint));

    public static Task<string?> IndexDefinitionAsync(
        ControlPlaneTestHost host,
        string index,
        CancellationToken cancellationToken) =>
        host.ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND indexname = @index",
            cancellationToken,
            ("index", index));

    [GeneratedRegex(@"^[^:]+")]
    private static partial Regex DefaultLiteral();
}
