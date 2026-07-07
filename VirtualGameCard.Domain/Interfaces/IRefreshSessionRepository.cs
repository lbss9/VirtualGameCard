using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Domain.Interfaces;

public interface IRefreshSessionRepository
{
    Task AddAsync(RefreshSession session, CancellationToken cancellationToken = default);
    Task<RefreshSession?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    );
    Task<RefreshSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default
    );
    Task<bool> RotateAsync(
        RefreshSession current,
        RefreshSession replacement,
        CancellationToken cancellationToken = default
    );
    Task RevokeAsync(RefreshSession session, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(
        Guid userId,
        Guid? exceptSessionId = null,
        CancellationToken cancellationToken = default
    );
    Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
