using VirtualGameCard.Application.Common;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Auth.Commands;

public sealed record LogoutCommand(Guid SessionId, string? RefreshToken);

public sealed class LogoutCommandHandler(IRefreshSessionRepository sessions)
{
    public async Task<Result<bool>> HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var session = !string.IsNullOrWhiteSpace(command.RefreshToken)
            ? await sessions.GetByTokenHashAsync(
                SecureToken.Hash(command.RefreshToken),
                cancellationToken
            )
            : await sessions.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is not null && session.Id != command.SessionId)
            session = await sessions.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is not null)
            await sessions.RevokeAsync(session, cancellationToken);
        return Result<bool>.Success(true);
    }
}
