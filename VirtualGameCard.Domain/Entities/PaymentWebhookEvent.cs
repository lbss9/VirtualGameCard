namespace VirtualGameCard.Domain.Entities;

public sealed class PaymentWebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProviderEventId { get; set; } = string.Empty;
    public Guid PurchaseId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
