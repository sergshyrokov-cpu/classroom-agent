using System.Net;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>US-003 helpers over a started Control Plane: adding and revoking over HTTP, reading and inserting <c>allowed_admin</c> rows.</summary>
public static class AllowedAdminHostExtensions
{
    private const string SelectAllowedAdmin =
        "SELECT id, identifier, installation_id, email, added_by_owner_id, created_at, updated_at FROM allowed_admin";

    /// <summary>Adds an entry through the form, asserts the redirect to the detail page and returns the stored row.</summary>
    public static async Task<AllowedAdminRow> AddAllowedAdminAsync(
        this ControlPlaneTestHost host,
        FormClient owner,
        Guid installation,
        string email,
        CancellationToken cancellationToken)
    {
        var response = await owner.PostFromPageAsync(
            AllowedAdminTestData.AddFormPath(installation),
            AllowedAdminTestData.AddPath(installation),
            AllowedAdminTestData.AddFields(email),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(AllowedAdminTestData.DetailPath(installation), response.LocationPath);
        var rows = await host.AllowedAdminsAsync(installation, cancellationToken);
        return Assert.Single(rows, r => r.Email == email.ToLowerInvariant());
    }

    /// <summary>Opens the confirmation page (for a fresh token) and submits the revocation.</summary>
    public static Task<PageResponse> RevokeAllowedAdminAsync(
        this FormClient owner,
        Guid installation,
        Guid admin,
        CancellationToken cancellationToken) =>
        owner.PostFromPageAsync(
            AllowedAdminTestData.RevocationPath(installation, admin),
            AllowedAdminTestData.RevocationPath(installation, admin),
            [],
            cancellationToken);

    public static Task<IReadOnlyList<AllowedAdminRow>> AllowedAdminsAsync(
        this ControlPlaneTestHost host,
        CancellationToken cancellationToken) =>
        host.QueryAsync(SelectAllowedAdmin + " ORDER BY id", Map, cancellationToken);

    public static Task<IReadOnlyList<AllowedAdminRow>> AllowedAdminsAsync(
        this ControlPlaneTestHost host,
        Guid installation,
        CancellationToken cancellationToken) =>
        host.QueryAsync(
            SelectAllowedAdmin + " WHERE installation_id = (SELECT id FROM installation WHERE identifier = @installation) ORDER BY id",
            Map,
            cancellationToken,
            ("installation", installation));

    /// <summary>Inserts an entry directly for the signed-in Owner — for list scenarios that need a given time or count.</summary>
    public static async Task<Guid> InsertAllowedAdminAsync(
        this ControlPlaneTestHost host,
        Guid installation,
        string email,
        CancellationToken cancellationToken,
        DateTimeOffset? createdAt = null)
    {
        var identifier = Guid.NewGuid();
        var time = createdAt ?? host.Time.GetUtcNow();
        await host.ExecuteAsync(
            """
            INSERT INTO allowed_admin (identifier, installation_id, email, added_by_owner_id, created_at, updated_at)
            VALUES (
                @identifier,
                (SELECT id FROM installation WHERE identifier = @installation),
                @email,
                (SELECT id FROM owner ORDER BY id LIMIT 1),
                @createdAt,
                @createdAt)
            """,
            cancellationToken,
            ("identifier", identifier),
            ("installation", installation),
            ("email", email),
            ("createdAt", time));
        return identifier;
    }

    private static AllowedAdminRow Map(Npgsql.NpgsqlDataReader r) =>
        new(
            r.GetInt64(0),
            r.GetGuid(1),
            r.GetInt64(2),
            r.GetString(3),
            r.GetInt64(4),
            r.GetFieldValue<DateTimeOffset>(5),
            r.GetFieldValue<DateTimeOffset>(6));
}
