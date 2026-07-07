using Microsoft.EntityFrameworkCore;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;
using VirtualGameCard.Infrastructure.Data;

namespace VirtualGameCard.Infrastructure.Repositories;

public sealed class PaymentWebhookEventRepository(AppDbContext db) : IPaymentWebhookEventRepository
{
    public async Task<bool> TryAddAsync(PaymentWebhookEvent paymentEvent)
    {
        if (db.Database.IsRelational())
        {
            var affected = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "PaymentWebhookEvents" ("Id", "ProviderEventId", "PurchaseId", "ReceivedAt")
                VALUES ({paymentEvent.Id}, {paymentEvent.ProviderEventId}, {paymentEvent.PurchaseId}, {paymentEvent.ReceivedAt})
                ON CONFLICT ("ProviderEventId") DO NOTHING
                """
            );
            return affected == 1;
        }
        if (
            await db.PaymentWebhookEvents.AnyAsync(x =>
                x.ProviderEventId == paymentEvent.ProviderEventId
            )
        )
            return false;
        db.PaymentWebhookEvents.Add(paymentEvent);
        await db.SaveChangesAsync();
        return true;
    }
}
