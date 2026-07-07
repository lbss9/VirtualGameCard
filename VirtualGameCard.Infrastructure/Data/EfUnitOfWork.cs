using Microsoft.EntityFrameworkCore;
using VirtualGameCard.Application.Interfaces;

namespace VirtualGameCard.Infrastructure.Data;

public sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default
    )
    {
        if (!db.Database.IsRelational())
            return await operation(cancellationToken);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                cancellationToken
            );
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
