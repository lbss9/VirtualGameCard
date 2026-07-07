namespace VirtualGameCard.Application.Purchases.Messages;

public sealed record PaymentRequestedMessage(
    Guid PurchaseId,
    Guid UserId,
    long AmountInCents,
    string Platform,
    string PaymentMethod,
    string IdempotencyKey,
    DateTime RequestedAtUtc
);
