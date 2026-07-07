using Microsoft.Extensions.Logging;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Application.Purchases.Messages;

namespace VirtualGameCard.Infrastructure.Messaging;

public sealed class NoOpPaymentMessagePublisher(ILogger<NoOpPaymentMessagePublisher> logger)
    : IPaymentMessagePublisher
{
    public Task PublishPaymentRequestedAsync(
        PaymentRequestedMessage message,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation(
            "RabbitMQ desativado. Mensagem de pagamento não publicada. PurchaseId: {PurchaseId}, IdempotencyKey: {IdempotencyKey}",
            message.PurchaseId,
            message.IdempotencyKey
        );

        return Task.CompletedTask;
    }
}
