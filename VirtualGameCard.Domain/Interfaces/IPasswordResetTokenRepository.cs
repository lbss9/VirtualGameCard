using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Domain.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token);

    /// <summary>Retorna um token ativo (não usado e não expirado) pelo hash, ou null.</summary>
    Task<PasswordResetToken?> GetActiveByHashAsync(string tokenHash);
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash);

    Task UpdateAsync(PasswordResetToken token);
    Task InvalidateAllForUserAsync(Guid userId);
}
