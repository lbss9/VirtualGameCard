using Microsoft.EntityFrameworkCore;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;
using VirtualGameCard.Infrastructure.Data;

namespace VirtualGameCard.Infrastructure.Repositories;

public sealed class EmailVerificationTokenRepository(AppDbContext db)
    : IEmailVerificationTokenRepository
{
    public async Task AddAsync(EmailVerificationToken token)
    {
        db.EmailVerificationTokens.Add(token);
        await db.SaveChangesAsync();
    }

    public Task<EmailVerificationToken?> GetActiveByHashAsync(string hash) =>
        db.EmailVerificationTokens.FirstOrDefaultAsync(x =>
            x.TokenHash == hash && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow
        );

    public async Task UpdateAsync(EmailVerificationToken token)
    {
        db.EmailVerificationTokens.Update(token);
        await db.SaveChangesAsync();
    }
}
