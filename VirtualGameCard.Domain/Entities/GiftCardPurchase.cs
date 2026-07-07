namespace VirtualGameCard.Domain.Entities;

public class GiftCardPurchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public long AmountInCents { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CreditCard;
    public string? Code { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PaymentReference { get; set; } = string.Empty;
    public GiftCardStatus Status { get; set; } = GiftCardStatus.Pending;
    public GiftCardPlatform Platform { get; set; } = GiftCardPlatform.Steam;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public User User { get; set; } = null!;
}
