using VirtualGameCard.Application.Purchases.Messages;

namespace VirtualGameCard.Application.Interfaces;

public interface IPaymentMessagePublisher
{
    Task PublishPaymentRequestedAsync(
        PaymentRequestedMessage message,
        CancellationToken cancellationToken = default
    );
}
