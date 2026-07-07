using Microsoft.EntityFrameworkCore;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;
using VirtualGameCard.Infrastructure.Data;

namespace VirtualGameCard.Infrastructure.Repositories;

public class PasswordResetTokenRepository(AppDbContext db) : IPasswordResetTokenRepository
{
    public async Task AddAsync(PasswordResetToken token)
    {
        db.PasswordResetTokens.Add(token);
        await db.SaveChangesAsync();
    }

    public Task<PasswordResetToken?> GetActiveByHashAsync(string tokenHash) =>
        db.PasswordResetTokens.FirstOrDefaultAsync(t =>
            t.TokenHash == tokenHash && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow
        );

    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash) =>
        db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public async Task UpdateAsync(PasswordResetToken token)
    {
        db.PasswordResetTokens.Update(token);
        await db.SaveChangesAsync();
    }

    public async Task InvalidateAllForUserAsync(Guid userId)
    {
        var active = await db
            .PasswordResetTokens.Where(x => x.UserId == userId && x.UsedAt == null)
            .ToListAsync();
        foreach (var token in active)
            token.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
