using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// What the host needs to issue a session, as a DTO — never the entity (AD-8). It carries no password field,
/// no security stamp and no Google token (S-04, S-09).
/// </summary>
public sealed record SignedInUser(long Id, string Email, AppRole Role, UiLanguage UiLanguage, string SecurityStamp);
