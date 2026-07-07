using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Domain.Interfaces;

public interface IGiftCardPurchaseRepository
{
    Task AddAsync(GiftCardPurchase purchase);
    Task<(GiftCardPurchase Purchase, bool Created)> AddIdempotentAsync(GiftCardPurchase purchase);
    Task<GiftCardPurchase?> GetByIdempotencyKeyAsync(Guid userId, string key);
    Task<GiftCardPurchase?> GetByIdAsync(Guid id);
    Task<GiftCardPurchase?> GetByPaymentReferenceAsync(string paymentReference);
    Task UpdateAsync(GiftCardPurchase purchase);
    Task<bool> TryTransitionPaymentAsync(
        Guid purchaseId,
        GiftCardStatus status,
        string? code,
        DateTime? paidAt
    );

    /// <summary>Página de compras do usuário, mais recentes primeiro.</summary>
    Task<List<GiftCardPurchase>> GetPageByUserAsync(Guid userId, int page, int pageSize);

    Task<int> CountByUserAsync(Guid userId);

    /// <summary>Compra do usuário pelo id (null se não existir ou for de outro usuário).</summary>
    Task<GiftCardPurchase?> GetByIdForUserAsync(Guid id, Guid userId);
}
