using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Domain.Interfaces;

public interface IEmailVerificationTokenRepository
{
    Task AddAsync(EmailVerificationToken token);
    Task<EmailVerificationToken?> GetActiveByHashAsync(string hash);
    Task UpdateAsync(EmailVerificationToken token);
}
