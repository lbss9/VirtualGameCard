using System.Security.Cryptography;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Purchases.Commands;

public sealed record ProcessPaymentApprovedMessageCommand(
    Guid PurchaseId,
    Guid PaymentId,
    string IdempotencyKey,
    DateTime ApprovedAtUtc
);

public sealed class ProcessPaymentApprovedMessageCommandHandler(
    IGiftCardPurchaseRepository purchases,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork
)
{
    public async Task<Result<bool>> HandleAsync(
        ProcessPaymentApprovedMessageCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (
            command.PurchaseId == Guid.Empty
            || command.PaymentId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.IdempotencyKey)
        )
            return Result<bool>.Failure(
                Error.Validation("Mensagem de pagamento aprovada inválida.", "PAYMENT_APPROVED_INVALID")
            );

        var purchase = await purchases.GetByIdAsync(command.PurchaseId);
        if (purchase is null)
            return Result<bool>.Failure(
                Error.NotFound("Compra não encontrada.", "PURCHASE_NOT_FOUND")
            );

        if (purchase.Status != GiftCardStatus.Pending)
            return Result<bool>.Success(true);

        return await unitOfWork.ExecuteAsync(
            async _ =>
            {
                var code = GenerateCardCode();
                var paidAt =
                    command.ApprovedAtUtc.Kind == DateTimeKind.Utc
                        ? command.ApprovedAtUtc
                        : command.ApprovedAtUtc.ToUniversalTime();

                if (
                    !await purchases.TryTransitionPaymentAsync(
                        purchase.Id,
                        GiftCardStatus.Approved,
                        code,
                        paidAt
                    )
                )
                    return Result<bool>.Success(true);

                await notifications.AddAsync(
                    new Notification
                    {
                        UserId = purchase.UserId,
                        Title = "Seu card está pronto!",
                        Message = $"Seu {purchase.Platform} Card está disponível em Minhas Compras.",
                        Kind = NotificationKind.Purchase,
                    }
                );

                return Result<bool>.Success(true);
            },
            cancellationToken
        );
    }

    private static string GenerateCardCode()
    {
        var value = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        return string.Join('-', value.Chunk(4).Select(chars => new string(chars)));
    }
}
