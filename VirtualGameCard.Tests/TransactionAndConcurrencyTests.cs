using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Infrastructure.Data;
using VirtualGameCard.Infrastructure.Repositories;
using Xunit;

namespace VirtualGameCard.Tests;

public sealed class TransactionAndConcurrencyTests
{
    [Fact]
    public async Task Unit_of_work_rolls_back_all_writes_when_a_composite_operation_fails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var unitOfWork = new EfUnitOfWork(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteAsync<int>(async ct =>
            {
                db.Users.Add(new User { Email = "rollback@example.com", PasswordHash = "hash" });
                await db.SaveChangesAsync(ct);
                throw new InvalidOperationException("simulated downstream failure");
            })
        );

        Assert.Empty(await db.Users.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Stale_concurrent_refresh_can_rotate_a_session_only_once()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vgc-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            var user = new User { Email = "rotation@example.com", PasswordHash = "hash" };
            var original = new RefreshSession
            {
                UserId = user.Id,
                User = user,
                FamilyId = Guid.NewGuid(),
                TokenHash = "original",
                ExpiresAt = DateTime.UtcNow.AddDays(1),
            };
            await using (var setup = new AppDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.AddRange(user, original);
                await setup.SaveChangesAsync();
            }

            await using var firstDb = new AppDbContext(options);
            await using var secondDb = new AppDbContext(options);
            var firstRepository = new RefreshSessionRepository(firstDb);
            var secondRepository = new RefreshSessionRepository(secondDb);
            var firstView = (await firstRepository.GetByTokenHashAsync("original"))!;
            var secondView = (await secondRepository.GetByTokenHashAsync("original"))!;

            var firstRotated = await firstRepository.RotateAsync(
                firstView,
                Replacement(original, "one")
            );
            var secondRotated = await secondRepository.RotateAsync(
                secondView,
                Replacement(original, "two")
            );

            Assert.True(firstRotated);
            Assert.False(secondRotated);
            await firstDb.DisposeAsync();
            await secondDb.DisposeAsync();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task Idempotent_order_and_payment_transition_succeed_only_once()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var user = new User { Email = "payment@example.com", PasswordHash = "hash" };
        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
        }

        await using var firstDb = new AppDbContext(options);
        await using var secondDb = new AppDbContext(options);
        var firstRepository = new GiftCardPurchaseRepository(firstDb);
        var secondRepository = new GiftCardPurchaseRepository(secondDb);
        GiftCardPurchase Order() =>
            new()
            {
                UserId = user.Id,
                AmountInCents = 2_500,
                Platform = GiftCardPlatform.Steam,
                PaymentMethod = PaymentMethod.Pix,
                IdempotencyKey = "same-request",
                PaymentReference = $"pay_{Guid.NewGuid():N}",
            };

        var first = await firstRepository.AddIdempotentAsync(Order());
        var replay = await secondRepository.AddIdempotentAsync(Order());
        Assert.True(first.Created);
        Assert.False(replay.Created);
        Assert.Equal(first.Purchase.Id, replay.Purchase.Id);

        var approved = await firstRepository.TryTransitionPaymentAsync(
            first.Purchase.Id,
            GiftCardStatus.Approved,
            "AAAA-BBBB-CCCC-DDDD",
            DateTime.UtcNow
        );
        var contradictory = await secondRepository.TryTransitionPaymentAsync(
            first.Purchase.Id,
            GiftCardStatus.Failed,
            null,
            null
        );
        Assert.True(approved);
        Assert.False(contradictory);
    }

    private static RefreshSession Replacement(RefreshSession original, string hash) =>
        new()
        {
            UserId = original.UserId,
            FamilyId = original.FamilyId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        };
}
