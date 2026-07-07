using System.Security.Cryptography;
using FluentValidation;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Purchases.Commands;

public sealed record ProcessPaymentWebhookCommand(
    string EventId,
    string PaymentReference,
    string Status,
    string Signature
);

public sealed class ProcessPaymentWebhookCommandValidator
    : AbstractValidator<ProcessPaymentWebhookCommand>
{
    public ProcessPaymentWebhookCommandValidator()
    {
        RuleFor(x => x.EventId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PaymentReference).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Status).Must(x => x is "approved" or "failed");
        RuleFor(x => x.Signature).NotEmpty().MaximumLength(256);
    }
}

public sealed class ProcessPaymentWebhookCommandHandler(
    IPaymentWebhookVerifier verifier,
    IPaymentWebhookEventRepository events,
    IGiftCardPurchaseRepository purchases,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork
)
{
    private static readonly ProcessPaymentWebhookCommandValidator Validator = new();

    public async Task<Result<bool>> HandleAsync(
        ProcessPaymentWebhookCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await Validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<bool>.Failure(
                Error.Validation("Evento de pagamento inválido.", "PAYMENT_EVENT_INVALID")
            );

        var payload = $"{command.EventId}.{command.PaymentReference}.{command.Status}";
        if (!verifier.IsValid(payload, command.Signature))
            return Result<bool>.Failure(
                Error.Unauthorized("Assinatura do webhook inválida.", "WEBHOOK_SIGNATURE_INVALID")
            );
        var purchase = await purchases.GetByPaymentReferenceAsync(command.PaymentReference);
        if (purchase is null)
            return Result<bool>.Failure(
                Error.NotFound("Pagamento não encontrado.", "PAYMENT_NOT_FOUND")
            );
        if (purchase.Status != GiftCardStatus.Pending)
            return Result<bool>.Success(true);

        return await unitOfWork.ExecuteAsync(
            async _ =>
            {
                var targetStatus =
                    command.Status == "approved" ? GiftCardStatus.Approved : GiftCardStatus.Failed;
                var code = targetStatus == GiftCardStatus.Approved ? GenerateCardCode() : null;
                DateTime? paidAt = targetStatus == GiftCardStatus.Approved ? DateTime.UtcNow : null;
                if (
                    !await events.TryAddAsync(
                        new PaymentWebhookEvent
                        {
                            ProviderEventId = command.EventId,
                            PurchaseId = purchase.Id,
                        }
                    )
                )
                    return Result<bool>.Success(true);
                if (
                    !await purchases.TryTransitionPaymentAsync(
                        purchase.Id,
                        targetStatus,
                        code,
                        paidAt
                    )
                )
                    return Result<bool>.Success(true);
                await notifications.AddAsync(
                    new Notification
                    {
                        UserId = purchase.UserId,
                        Title =
                            targetStatus == GiftCardStatus.Approved
                                ? "Seu card está pronto!"
                                : "Pagamento não aprovado",
                        Message =
                            targetStatus == GiftCardStatus.Approved
                                ? $"Seu {purchase.Platform} Card está disponível em Minhas Compras."
                                : "Não conseguimos aprovar o pagamento. Nenhum card foi emitido.",
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
