namespace VirtualGameCard.Application.Purchases.Messages;

public sealed record PaymentApprovedMessage(
    Guid PurchaseId,
    Guid PaymentId,
    string IdempotencyKey,
    DateTime ApprovedAtUtc
);
