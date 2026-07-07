using System.Security.Cryptography;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Application.Purchases.DTOs;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Purchases.Commands;

public sealed record SimulatePurchaseApprovalCommand(Guid UserId, Guid PurchaseId);

public sealed class SimulatePurchaseApprovalCommandHandler(
    IGiftCardPurchaseRepository purchases,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork
)
{
    public async Task<Result<PurchaseDetail>> HandleAsync(
        SimulatePurchaseApprovalCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var purchase = await purchases.GetByIdForUserAsync(command.PurchaseId, command.UserId);
        if (purchase is null)
            return Result<PurchaseDetail>.Failure(
                Error.NotFound("Compra não encontrada.", "PURCHASE_NOT_FOUND")
            );

        if (purchase.Status == GiftCardStatus.Approved)
            return Result<PurchaseDetail>.Success(ToDetail(purchase));

        if (purchase.Status != GiftCardStatus.Pending)
            return Result<PurchaseDetail>.Failure(
                Error.Conflict("Esta compra não pode mais ser aprovada.", "PURCHASE_NOT_PENDING")
            );

        var code = GenerateCardCode();

        var result = await unitOfWork.ExecuteAsync(
            async _ =>
            {
                var approved = await purchases.TryTransitionPaymentAsync(
                    purchase.Id,
                    GiftCardStatus.Approved,
                    code,
                    DateTime.UtcNow
                );

                if (!approved)
                {
                    var current = await purchases.GetByIdForUserAsync(command.PurchaseId, command.UserId);
                    return current is null
                        ? Result<PurchaseDetail>.Failure(
                            Error.NotFound("Compra não encontrada.", "PURCHASE_NOT_FOUND")
                        )
                        : Result<PurchaseDetail>.Success(ToDetail(current));
                }

                await notifications.AddAsync(
                    new Notification
                    {
                        UserId = command.UserId,
                        Title = "Pagamento confirmado",
                        Message = $"Seu {purchase.Platform} Card está disponível em Minhas Compras.",
                        Kind = NotificationKind.Purchase,
                    }
                );

                var updated =
                    await purchases.GetByIdForUserAsync(command.PurchaseId, command.UserId)
                    ?? purchase;
                updated.Status = GiftCardStatus.Approved;
                updated.Code = code;
                updated.PaidAt ??= DateTime.UtcNow;
                return Result<PurchaseDetail>.Success(ToDetail(updated));
            },
            cancellationToken
        );

        return result;
    }

    private static PurchaseDetail ToDetail(GiftCardPurchase purchase) =>
        new(
            purchase.Id,
            purchase.AmountInCents,
            purchase.PaymentMethod,
            purchase.Platform,
            purchase.Status,
            purchase.Code,
            purchase.Status.ToString().ToLowerInvariant(),
            purchase.PaymentReference,
            purchase.CreatedAt
        );

    private static string GenerateCardCode()
    {
        var value = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        return string.Join('-', value.Chunk(4).Select(chars => new string(chars)));
    }
}
