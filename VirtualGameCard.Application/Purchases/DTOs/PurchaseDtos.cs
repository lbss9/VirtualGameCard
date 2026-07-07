using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Application.Purchases.DTOs;

/// <summary>Item da listagem de compras (sem o código do card).</summary>
public record PurchaseSummary(
    Guid Id,
    long AmountInCents,
    PaymentMethod PaymentMethod,
    GiftCardPlatform Platform,
    GiftCardStatus Status,
    DateTime CreatedAt
);

/// <summary>Detalhe completo de uma compra, incluindo o código.</summary>
public record PurchaseDetail(
    Guid Id,
    long AmountInCents,
    PaymentMethod PaymentMethod,
    GiftCardPlatform Platform,
    GiftCardStatus Status,
    string? Code,
    string StatusName,
    string PaymentReference,
    DateTime CreatedAt
);

/// <summary>Resultado paginado genérico.</summary>
public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize, int TotalPages);
