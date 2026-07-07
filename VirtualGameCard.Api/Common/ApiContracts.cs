namespace VirtualGameCard.Api.Common;

public sealed record MessageData(string Message);

public sealed record EmailVerificationSentData(string Message, string? VerificationToken);

public sealed record ForgotPasswordData(string Message, string? ResetToken, DateTime? ExpiresAt);

public sealed record PurchaseData(
    Guid Id,
    int Amount,
    string Platform,
    string PaymentMethod,
    string Status,
    string? Code,
    string PaymentReference,
    DateTime CreatedAt
);

public sealed record PurchaseListItemData(
    Guid Id,
    int Amount,
    string Platform,
    string PaymentMethod,
    string Status,
    DateTime CreatedAt
);

public sealed record PurchasePageData(
    IReadOnlyList<PurchaseListItemData> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);

public sealed record PaymentEventData(bool Processed);
