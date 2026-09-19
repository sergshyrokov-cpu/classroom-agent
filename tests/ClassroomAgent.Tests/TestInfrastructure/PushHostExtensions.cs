using System.Net;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-006 helpers over a started Control Plane: registering with a push address, changing it through the
/// form, and reading the stored column (api-design §7; db-design §3.1).
/// </summary>
public static class PushHostExtensions
{
    /// <summary>Registers an installation, optionally with a push address, and returns its UUID.</summary>
    public static async Task<Guid> RegisterInstallationWithPushAddressAsync(
        this ControlPlaneTestHost host,
        FormClient owner,
        CancellationToken cancellationToken,
        string? pushAddress = PushTestData.Address,
        string name = InstallationTestData.Name,
        string domain = InstallationTestData.Domain,
        string clientId = InstallationTestData.ClientId)
    {
        await owner.GetAsync("/installations/new", cancellationToken);
        var fields = InstallationTestData.RegisterFields(name, domain, clientId).ToList();
        fields.Add(new(PushTestData.FieldName, pushAddress ?? string.Empty));
        var response = await owner.PostFormAsync("/installations", fields, cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        return InstallationHostExtensions.IdentifierFromLocation(response);
    }

    /// <summary>Submits the push address form (an empty value clears the address).</summary>
    public static Task<PageResponse> ChangePushAddressAsync(
        this FormClient owner,
        Guid installation,
        string address,
        CancellationToken cancellationToken) =>
        owner.PostFromPageAsync(
            PushTestData.PushAddressPath(installation),
            PushTestData.PushAddressPath(installation),
            PushTestData.AddressFields(address),
            cancellationToken);

    /// <summary>The stored <c>push_address</c>, or null when not set.</summary>
    public static Task<string?> PushAddressAsync(
        this ControlPlaneTestHost host,
        Guid installation,
        CancellationToken cancellationToken) =>
        host.ScalarAsync<string>(
            "SELECT push_address FROM installation WHERE identifier = @identifier",
            cancellationToken,
            ("identifier", installation));

    /// <summary>Sets the column directly — for scenarios that need an address without going through the form.</summary>
    public static Task<int> SetPushAddressDirectlyAsync(
        this ControlPlaneTestHost host,
        Guid installation,
        string? address,
        CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            "UPDATE installation SET push_address = @address WHERE identifier = @identifier",
            cancellationToken,
            ("address", address),
            ("identifier", installation));
}
