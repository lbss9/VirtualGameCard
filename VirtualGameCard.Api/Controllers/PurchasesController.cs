using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VirtualGameCard.Api.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Application.Purchases.Commands;
using VirtualGameCard.Application.Purchases.Queries;
using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Api.Controllers;

[Authorize, ApiController]
public sealed class PurchasesController(
    PurchaseCardCommandHandler purchase,
    GetPurchasesQueryHandler list,
    GetPurchaseByIdQueryHandler detail,
    SimulatePurchaseApprovalCommandHandler simulateApproval,
    ICurrentUser current,
    IWebHostEnvironment environment,
    IConfiguration configuration
) : ControllerBase
{
    public sealed record PurchaseRequest(int Amount, string Platform, string PaymentMethod);

    [EnableRateLimiting("purchase"), HttpPost("api/cards/purchase")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseData>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Purchase(
        PurchaseRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey
    )
    {
        if (
            request.Amount is < 5 or > 250
            || request.Amount % 5 != 0
            || !TryPlatform(request.Platform, out var platform)
            || !TryMethod(request.PaymentMethod, out var method)
        )
            return ApiResponse
                .Failure("Dados da compra inválidos.", "VALIDATION_ERROR", 400, Request.Path)
                .AsResult(400);
        var result = await purchase.HandleAsync(
            new PurchaseCardCommand(
                request.Amount * 100L,
                method,
                platform,
                idempotencyKey ?? string.Empty
            )
        );
        if (!result.IsSuccess)
            return result.Error!.ToActionResult(HttpContext);
        return StatusCode(
            201,
            ApiResponse.Success(
                HttpContext,
                ToDetail(result.Value!),
                "Compra criada com sucesso.",
                "PURCHASE_CREATED",
                201
            )
        );
    }

    [HttpGet("api/purchases")]
    [ProducesResponseType(typeof(ApiResponse<PurchasePageData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (current.Id is not Guid id)
            return ApiResponse
                .Failure("Autenticação necessária.", "UNAUTHORIZED", 401, Request.Path)
                .AsResult(401);
        var result = await list.HandleAsync(new GetPurchasesQuery(id, page, pageSize));
        if (!result.IsSuccess)
            return result.Error!.ToActionResult(HttpContext);
        var value = result.Value!;
        var data = new PurchasePageData(
            value
                .Items.Select(x => new PurchaseListItemData(
                    x.Id,
                    (int)(x.AmountInCents / 100),
                    PlatformName(x.Platform),
                    MethodName(x.PaymentMethod),
                    x.Status.ToString().ToLowerInvariant(),
                    x.CreatedAt
                ))
                .ToList(),
            value.Total,
            value.Page,
            value.PageSize,
            value.TotalPages
        );
        return Ok(
            ApiResponse.Success(HttpContext, data, "Compras carregadas.", "PURCHASES_RETRIEVED")
        );
    }

    [HttpGet("api/purchases/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(Guid id)
    {
        if (current.Id is not Guid userId)
            return ApiResponse
                .Failure("Autenticação necessária.", "UNAUTHORIZED", 401, Request.Path)
                .AsResult(401);
        var result = await detail.HandleAsync(new GetPurchaseByIdQuery(userId, id));
        if (!result.IsSuccess)
            return result.Error!.ToActionResult(HttpContext);
        return Ok(
            ApiResponse.Success(
                HttpContext,
                ToDetail(result.Value!),
                "Compra carregada.",
                "PURCHASE_RETRIEVED"
            )
        );
    }

    [EnableRateLimiting("sensitive"), HttpPost("api/purchases/{id:guid}/simulate-approval")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SimulateApproval(Guid id, CancellationToken cancellationToken)
    {
        var enabled =
            environment.IsDevelopment()
            || configuration.GetValue<bool>("Features:AllowPaymentSimulation");

        if (!enabled)
            return ApiResponse
                .Failure(
                    "Simulação de pagamento indisponível neste ambiente.",
                    "PAYMENT_SIMULATION_DISABLED",
                    StatusCodes.Status403Forbidden,
                    Request.Path
                )
                .AsResult(StatusCodes.Status403Forbidden);

        if (current.Id is not Guid userId)
            return ApiResponse
                .Failure("Autenticação necessária.", "UNAUTHORIZED", 401, Request.Path)
                .AsResult(401);

        var result = await simulateApproval.HandleAsync(
            new SimulatePurchaseApprovalCommand(userId, id),
            cancellationToken
        );

        return result.IsSuccess
            ? Ok(
                ApiResponse.Success(
                    HttpContext,
                    ToDetail(result.Value!),
                    "Pagamento confirmado por simulação.",
                    "PAYMENT_SIMULATED"
                )
            )
            : result.Error!.ToActionResult(HttpContext);
    }

    private static PurchaseData ToDetail(
        VirtualGameCard.Application.Purchases.DTOs.PurchaseDetail x
    ) =>
        new(
            x.Id,
            (int)(x.AmountInCents / 100),
            PlatformName(x.Platform),
            MethodName(x.PaymentMethod),
            x.StatusName,
            x.Code,
            x.PaymentReference,
            x.CreatedAt
        );

    private static string MethodName(PaymentMethod x) => x == PaymentMethod.Pix ? "pix" : "card";

    private static string PlatformName(GiftCardPlatform x) =>
        x switch
        {
            GiftCardPlatform.Playstation => "playstation",
            GiftCardPlatform.GooglePlay => "google-play",
            _ => x.ToString().ToLowerInvariant(),
        };

    private static bool TryMethod(string value, out PaymentMethod result)
    {
        result = value.ToLowerInvariant() switch
        {
            "pix" => PaymentMethod.Pix,
            "card" => PaymentMethod.CreditCard,
            _ => 0,
        };
        return result != 0;
    }

    private static bool TryPlatform(string value, out GiftCardPlatform result)
    {
        var normalized = value.Replace("-", "");
        return Enum.TryParse(normalized, true, out result) && Enum.IsDefined(result);
    }
}
