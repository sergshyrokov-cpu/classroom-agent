namespace ClassroomAgent.ControlPlane.Services;

/// <summary>One AllowedAdmin entry on the installation detail page (US-003 api-design §6). No internal key, no added-by id.</summary>
public sealed record AllowedAdminItemDto(Guid Identifier, string Email, DateTimeOffset AddedAt);
