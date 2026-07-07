using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtualGameCard.Api.Common;
using VirtualGameCard.Application.Notifications;

namespace VirtualGameCard.Api.Controllers;

[Authorize, ApiController, Route("api/notifications")]
public sealed class NotificationsController(
    GetNotificationsQueryHandler get,
    MarkNotificationReadCommandHandler markRead,
    MarkAllNotificationsReadCommandHandler markAllRead
) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<NotificationsResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var result = await get.HandleAsync();
        return result.IsSuccess
            ? Ok(
                ApiResponse.Success(
                    HttpContext,
                    result.Value,
                    "Notificações carregadas.",
                    "NOTIFICATIONS_RETRIEVED"
                )
            )
            : result.Error!.ToActionResult(HttpContext);
    }

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Read(Guid id)
    {
        var result = await markRead.HandleAsync(new MarkNotificationReadCommand(id));
        return result.IsSuccess ? NoContent() : result.Error!.ToActionResult(HttpContext);
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReadAll()
    {
        var result = await markAllRead.HandleAsync();
        return result.IsSuccess ? NoContent() : result.Error!.ToActionResult(HttpContext);
    }
}
