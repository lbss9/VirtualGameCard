using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Domain.Interfaces;

public interface IPaymentWebhookEventRepository
{
    Task<bool> TryAddAsync(PaymentWebhookEvent paymentEvent);
}
