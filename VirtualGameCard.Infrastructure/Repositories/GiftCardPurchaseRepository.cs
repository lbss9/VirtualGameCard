using Microsoft.EntityFrameworkCore;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;
using VirtualGameCard.Infrastructure.Data;

namespace VirtualGameCard.Infrastructure.Repositories;

public sealed class GiftCardPurchaseRepository(AppDbContext db) : IGiftCardPurchaseRepository
{
    public async Task AddAsync(GiftCardPurchase purchase)
    {
        db.GiftCardPurchases.Add(purchase);
        await db.SaveChangesAsync();
    }

    public async Task<(GiftCardPurchase Purchase, bool Created)> AddIdempotentAsync(
        GiftCardPurchase purchase
    )
    {
        db.GiftCardPurchases.Add(purchase);
        try
        {
            await db.SaveChangesAsync();
            return (purchase, true);
        }
        catch (DbUpdateException)
        {
            db.Entry(purchase).State = EntityState.Detached;
            var existing = await db
                .GiftCardPurchases.AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.UserId == purchase.UserId && x.IdempotencyKey == purchase.IdempotencyKey
                );
            if (existing is null)
                throw;
            return (existing, false);
        }
    }

    public Task<GiftCardPurchase?> GetByIdempotencyKeyAsync(Guid userId, string key) =>
        db
            .GiftCardPurchases.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == key);

    public Task<GiftCardPurchase?> GetByIdAsync(Guid id) =>
        db.GiftCardPurchases.SingleOrDefaultAsync(x => x.Id == id);

    public Task<GiftCardPurchase?> GetByPaymentReferenceAsync(string paymentReference) =>
        db.GiftCardPurchases.SingleOrDefaultAsync(x => x.PaymentReference == paymentReference);

    public async Task UpdateAsync(GiftCardPurchase purchase)
    {
        db.GiftCardPurchases.Update(purchase);
        await db.SaveChangesAsync();
    }

    public async Task<bool> TryTransitionPaymentAsync(
        Guid purchaseId,
        GiftCardStatus status,
        string? code,
        DateTime? paidAt
    )
    {
        if (db.Database.IsRelational())
        {
            var affected = await db
                .GiftCardPurchases.Where(x =>
                    x.Id == purchaseId && x.Status == GiftCardStatus.Pending
                )
                .ExecuteUpdateAsync(setters =>
                    setters
                        .SetProperty(x => x.Status, status)
                        .SetProperty(x => x.Code, code)
                        .SetProperty(x => x.PaidAt, paidAt)
                );
            return affected == 1;
        }
        var purchase = await db.GiftCardPurchases.SingleOrDefaultAsync(x => x.Id == purchaseId);
        if (purchase is null || purchase.Status != GiftCardStatus.Pending)
            return false;
        purchase.Status = status;
        purchase.Code = code;
        purchase.PaidAt = paidAt;
        await db.SaveChangesAsync();
        return true;
    }

    public Task<List<GiftCardPurchase>> GetPageByUserAsync(Guid userId, int page, int size) =>
        db
            .GiftCardPurchases.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

    public Task<int> CountByUserAsync(Guid userId) =>
        db.GiftCardPurchases.CountAsync(x => x.UserId == userId);

    public Task<GiftCardPurchase?> GetByIdForUserAsync(Guid id, Guid userId) =>
        db
            .GiftCardPurchases.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
}
