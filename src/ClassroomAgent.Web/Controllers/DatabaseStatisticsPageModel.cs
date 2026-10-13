using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Web.Controllers;

/// <summary>
/// The view model of the Database page (US-024 entity model §5): the query's DTO and the US-019 one-time message key —
/// from TempData after an accepted press, or the role's refusal key on the <c>409</c> re-render (api-design §2.4).
/// </summary>
public sealed record DatabaseStatisticsPageModel(DatabaseStatisticsView Statistics, string? SynchronizationMessageKey);
