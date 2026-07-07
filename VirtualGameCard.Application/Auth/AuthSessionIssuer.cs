using VirtualGameCard.Application.Auth.DTOs;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Auth;

public sealed class AuthSessionIssuer(IJwtService jwtService, IRefreshSessionRepository sessions)
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public async Task<AuthSessionResult> IssueAsync(
        User user,
        Guid? familyId = null,
        CancellationToken cancellationToken = default
    )
    {
        var rawToken = SecureToken.Generate();
        var expiresAt = DateTime.UtcNow.Add(RefreshLifetime);
        var session = new RefreshSession
        {
            UserId = user.Id,
            FamilyId = familyId ?? Guid.NewGuid(),
            TokenHash = SecureToken.Hash(rawToken),
            ExpiresAt = expiresAt,
        };
        await sessions.AddAsync(session, cancellationToken);
        return new AuthSessionResult(
            new AuthResponse(jwtService.GenerateToken(user, session.Id), user.Id),
            rawToken,
            expiresAt
        );
    }
}
