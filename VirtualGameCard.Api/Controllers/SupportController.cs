using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VirtualGameCard.Api.Common;
using VirtualGameCard.Application.Support;

namespace VirtualGameCard.Api.Controllers;

[Authorize, ApiController, Route("api/support/tickets")]
public sealed class SupportController(CreateSupportTicketCommandHandler handler) : ControllerBase
{
    public sealed record CreateRequest(string Subject, string Category, string Message);

    [EnableRateLimiting("support"), HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SupportTicketResult>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        CreateRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await handler.HandleAsync(
            new CreateSupportTicketCommand(request.Subject, request.Category, request.Message),
            cancellationToken
        );
        if (!result.IsSuccess)
            return result.Error!.ToActionResult(HttpContext);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse.Success(
                HttpContext,
                result.Value,
                "Chamado aberto com sucesso.",
                "SUPPORT_TICKET_CREATED",
                StatusCodes.Status201Created
            )
        );
    }
}
