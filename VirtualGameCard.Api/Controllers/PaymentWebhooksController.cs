using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VirtualGameCard.Api.Common;
using VirtualGameCard.Api.Observability;
using VirtualGameCard.Application.Purchases.Commands;

namespace VirtualGameCard.Api.Controllers;

[ApiController, Route("api/payments/webhooks")]
public sealed class PaymentWebhooksController(ProcessPaymentWebhookCommandHandler handler)
    : ControllerBase
{
    public sealed record PaymentEventRequest(
        string EventId,
        string PaymentReference,
        string Status
    );

    [EnableRateLimiting("webhook"), HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PaymentEventData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Process(
        PaymentEventRequest request,
        [FromHeader(Name = "X-Payment-Signature")] string? signature,
        CancellationToken cancellationToken
    )
    {
        var result = await handler.HandleAsync(
            new ProcessPaymentWebhookCommand(
                request.EventId,
                request.PaymentReference,
                request.Status.ToLowerInvariant(),
                signature ?? string.Empty
            ),
            cancellationToken
        );
        AppMetrics
            .PaymentWebhookEvents.WithLabels(
                request.Status.ToLowerInvariant(),
                result.IsSuccess ? "success" : "failure"
            )
            .Inc();
        if (!result.IsSuccess)
            return result.Error!.ToActionResult(HttpContext);
        return Ok(
            ApiResponse.Success(
                HttpContext,
                new PaymentEventData(true),
                "Evento processado.",
                "PAYMENT_EVENT_PROCESSED"
            )
        );
    }
}
