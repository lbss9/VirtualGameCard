using Microsoft.EntityFrameworkCore;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;
using VirtualGameCard.Infrastructure.Data;

namespace VirtualGameCard.Infrastructure.Repositories;

public sealed class RefreshSessionRepository(AppDbContext db) : IRefreshSessionRepository
{
    public async Task AddAsync(
        RefreshSession session,
        CancellationToken cancellationToken = default
    )
    {
        db.RefreshSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshSession?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    ) =>
        db
            .RefreshSessions.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public Task<RefreshSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default
    ) => db.RefreshSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

    public async Task<bool> RotateAsync(
        RefreshSession current,
        RefreshSession replacement,
        CancellationToken cancellationToken = default
    )
    {
        if (!db.Database.IsRelational())
        {
            if (current.RevokedAt is not null)
                return false;
            current.RevokedAt = DateTime.UtcNow;
            current.ReplacedBySessionId = replacement.Id;
            db.RefreshSessions.Add(replacement);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db
            .RefreshSessions.Where(x => x.Id == current.Id && x.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.RevokedAt, DateTime.UtcNow)
                        .SetProperty(x => x.ReplacedBySessionId, replacement.Id),
                cancellationToken
            );
        if (updated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
        db.RefreshSessions.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RevokeAsync(
        RefreshSession session,
        CancellationToken cancellationToken = default
    )
    {
        session.RevokedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        Guid? exceptSessionId = null,
        CancellationToken cancellationToken = default
    )
    {
        var sessions = await db
            .RefreshSessions.Where(x =>
                x.UserId == userId
                && x.RevokedAt == null
                && (!exceptSessionId.HasValue || x.Id != exceptSessionId)
            )
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
            session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken = default
    )
    {
        var sessions = await db
            .RefreshSessions.Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
            session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> IsActiveAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default
    ) =>
        db
            .RefreshSessions.AsNoTracking()
            .AnyAsync(
                x => x.Id == sessionId && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow,
                cancellationToken
            );
}
