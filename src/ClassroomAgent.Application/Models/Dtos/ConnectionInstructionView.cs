using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// What the super-admin instruction shows (US-010 openapi <c>ConnectionInstructionPageModel</c>; AD-8): a DTO,
/// never the entity.
/// </summary>
/// <remarks>
/// It is defined as much by what it cannot carry (api-design §2.7): **no secret** — not the service-account
/// key, not the reference to it, not the OAuth client secret (SC-7, a Hard Stop; PC-9) — and neither of the two
/// identifiers §6 (v78) warns against confusing with the client ID: the installation's OAuth web client id and
/// the <c>Installation</c> UUID of the service channel are absent by construction, and so is the service
/// account's own <c>…@….iam.gserviceaccount.com</c> address.
///
/// The instruction's prose is not here either. The technical-account statements, the super-admin's two actions
/// and every label are translated text the view renders from named keys, because <c>Application</c> holds no
/// user-visible sentence (AD-6, spec FR-012, I-8).
/// </remarks>
/// <param name="State">Complete, or not yet confirmed (spec FR-002).</param>
/// <param name="InstallationDomain">The school's domain from <c>LegitimacyState</c>, or null while no check has succeeded.</param>
/// <param name="ServiceAccountClientId">This <c>Installation</c>'s service-account client ID, or null likewise.</param>
/// <param name="Scopes">The six scopes of <c>trebovaniya.md</c> §6, from the constant in <c>Domain</c> (spec FR-004).</param>
public sealed record ConnectionInstructionView(
    ConnectionInstructionState State,
    string? InstallationDomain,
    string? ServiceAccountClientId,
    IReadOnlyList<string> Scopes);
