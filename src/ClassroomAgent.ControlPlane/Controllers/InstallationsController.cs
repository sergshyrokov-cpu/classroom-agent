using ClassroomAgent.ControlPlane.Security;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// Installation list, registration, detail, name and client ID pages (US-002 FR-001 …
/// FR-011): HTTP mapping only; the rules are in <see cref="InstallationRegistry"/>. A
/// non-UUID <c>{id}</c> matches no route and reaches the 404 catch-all.
/// </summary>
[Authorize(Policy = OwnerSession.OwnerPolicy)]
[Route("installations")]
public sealed class InstallationsController(InstallationRegistry registry) : Controller
{
    private const string ListView = "~/Views/Installations/Index.cshtml";
    private const string NewView = "~/Views/Installations/New.cshtml";
    private const string DetailView = "~/Views/Installations/Detail.cshtml";
    private const string NameView = "~/Views/Installations/Name.cshtml";
    private const string ClientIdView = "~/Views/Installations/ClientId.cshtml";

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        View(ListView, await registry.ListAsync(cancellationToken));

    [HttpGet("new")]
    public IActionResult New() => View(NewView, new RegisterInstallationPageModel(null, null, null));

    [HttpPost]
    public async Task<IActionResult> Register([FromForm] RegisterInstallationRequest request, CancellationToken cancellationToken)
    {
        var page = new RegisterInstallationPageModel(request.Name, request.Domain, request.ClientId);
        if (!ModelState.IsValid)
        {
            return Form(NewView, page, StatusCodes.Status400BadRequest);
        }

        if (OwnerSession.OwnerId(User) is not { } ownerId)
        {
            return Forbid();
        }

        var result = await registry.RegisterAsync(
            request.Name!,
            request.Domain!,
            request.ClientId!,
            ownerId,
            HttpContext.TraceIdentifier,
            cancellationToken);

        if (result.Identifier is { } identifier)
        {
            return Redirect(DetailPath(identifier));
        }

        if (result.DomainTaken)
        {
            ModelState.AddModelError(nameof(RegisterInstallationRequest.Domain), "Installation.Domain.Taken");
        }

        if (result.ClientIdTaken)
        {
            ModelState.AddModelError(nameof(RegisterInstallationRequest.ClientId), "Installation.ClientId.Taken");
        }

        return Form(NewView, page, StatusCodes.Status409Conflict);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken) =>
        await registry.GetAsync(id, cancellationToken) is { } installation
            ? View(DetailView, new InstallationDetailPageModel(
                installation,
                InstallationStatusNotice.KeyFor(Request.Query["notice"], installation.Status)))
            : NotFound();

    [HttpGet("{id:guid}/name")]
    public async Task<IActionResult> NameForm(Guid id, CancellationToken cancellationToken) =>
        await registry.GetAsync(id, cancellationToken) is { } installation
            ? View(NameView, new RenameInstallationPageModel(id, installation.Name))
            : NotFound();

    [HttpPost("{id:guid}/name")]
    public async Task<IActionResult> Rename(
        Guid id,
        [FromForm] RenameInstallationRequest request,
        CancellationToken cancellationToken)
    {
        // An unknown installation is 404 even when the submitted name is invalid (api-design §4).
        if (await registry.GetAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Form(NameView, new RenameInstallationPageModel(id, request.Name), StatusCodes.Status400BadRequest);
        }

        if (OwnerSession.OwnerId(User) is not { } ownerId)
        {
            return Forbid();
        }

        var result = await registry.RenameAsync(id, request.Name!, ownerId, HttpContext.TraceIdentifier, cancellationToken);
        return result == RenameInstallationResult.NotFound ? NotFound() : Redirect(DetailPath(id));
    }

    [HttpGet("{id:guid}/client-id")]
    public async Task<IActionResult> ClientIdForm(Guid id, CancellationToken cancellationToken) =>
        await registry.GetAsync(id, cancellationToken) is { } installation
            ? View(ClientIdView, new ChangeInstallationClientIdPageModel(id, installation.ClientId))
            : NotFound();

    [HttpPost("{id:guid}/client-id")]
    public async Task<IActionResult> ChangeClientId(
        Guid id,
        [FromForm] ChangeInstallationClientIdRequest request,
        CancellationToken cancellationToken)
    {
        if (await registry.GetAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        var page = new ChangeInstallationClientIdPageModel(id, request.ClientId);
        if (!ModelState.IsValid)
        {
            return Form(ClientIdView, page, StatusCodes.Status400BadRequest);
        }

        if (OwnerSession.OwnerId(User) is not { } ownerId)
        {
            return Forbid();
        }

        var result = await registry.ChangeClientIdAsync(id, request.ClientId!, ownerId, HttpContext.TraceIdentifier, cancellationToken);
        switch (result)
        {
            case ChangeClientIdResult.NotFound:
                return NotFound();
            case ChangeClientIdResult.ClientIdTaken:
                ModelState.AddModelError(nameof(ChangeInstallationClientIdRequest.ClientId), "Installation.ClientId.Taken");
                return Form(ClientIdView, page, StatusCodes.Status409Conflict);
            default:
                return Redirect(DetailPath(id));
        }
    }

    private static string DetailPath(Guid identifier) => $"/installations/{identifier:D}";

    private ViewResult Form(string viewName, object model, int statusCode)
    {
        var view = View(viewName, model);
        view.StatusCode = statusCode;
        return view;
    }
}
