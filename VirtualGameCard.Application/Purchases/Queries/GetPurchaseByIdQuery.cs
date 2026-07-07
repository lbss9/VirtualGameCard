using FluentValidation;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Purchases.DTOs;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Purchases.Queries;

public record GetPurchaseByIdQuery(Guid UserId, Guid PurchaseId);

public sealed class GetPurchaseByIdQueryValidator : AbstractValidator<GetPurchaseByIdQuery>
{
    public GetPurchaseByIdQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PurchaseId).NotEmpty().WithErrorCode("INVALID_PURCHASE_ID");
    }
}

public class GetPurchaseByIdQueryHandler(IGiftCardPurchaseRepository purchaseRepository)
{
    private static readonly GetPurchaseByIdQueryValidator Validator = new();

    public async Task<Result<PurchaseDetail>> HandleAsync(GetPurchaseByIdQuery query)
    {
        var validation = await Validator.ValidateAsync(query);
        if (!validation.IsValid)
            return Result<PurchaseDetail>.Failure(
                Error.Validation(
                    "Identificador da compra inválido.",
                    validation.Errors[0].ErrorCode
                )
            );
        var purchase = await purchaseRepository.GetByIdForUserAsync(query.PurchaseId, query.UserId);

        if (purchase is null)
            return Result<PurchaseDetail>.Failure(
                Error.NotFound("Compra não encontrada.", "PURCHASE_NOT_FOUND")
            );

        return Result<PurchaseDetail>.Success(
            new PurchaseDetail(
                purchase.Id,
                purchase.AmountInCents,
                purchase.PaymentMethod,
                purchase.Platform,
                purchase.Status,
                purchase.Code,
                purchase.Status.ToString().ToLowerInvariant(),
                purchase.PaymentReference,
                purchase.CreatedAt
            )
        );
    }
}
