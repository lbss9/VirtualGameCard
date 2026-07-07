using FluentValidation;
using VirtualGameCard.Application.Auth.DTOs;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Auth.Commands;

public sealed record RefreshSessionCommand(string RefreshToken);

public sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionCommandValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
}

public sealed class RefreshSessionCommandHandler(
    IRefreshSessionRepository sessions,
    IJwtService jwtService
)
{
    private static readonly RefreshSessionCommandValidator Validator = new();

    public async Task<Result<AuthSessionResult>> HandleAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (!(await Validator.ValidateAsync(command, cancellationToken)).IsValid)
            return Invalid();

        var current = await sessions.GetByTokenHashAsync(
            SecureToken.Hash(command.RefreshToken),
            cancellationToken
        );
        if (current is null)
            return Invalid();

        if (current.RevokedAt is not null)
        {
            await sessions.RevokeFamilyAsync(current.FamilyId, cancellationToken);
            return Result<AuthSessionResult>.Failure(
                Error.Unauthorized("Sessão reutilizada e revogada.", "REFRESH_TOKEN_REUSED")
            );
        }
        if (current.ExpiresAt <= DateTime.UtcNow)
            return Result<AuthSessionResult>.Failure(
                Error.Unauthorized("Sessão expirada.", "REFRESH_TOKEN_EXPIRED")
            );

        var raw = SecureToken.Generate();
        var expiresAt = DateTime.UtcNow.AddDays(30);
        var replacement = new RefreshSession
        {
            UserId = current.UserId,
            FamilyId = current.FamilyId,
            TokenHash = SecureToken.Hash(raw),
            ExpiresAt = expiresAt,
        };
        if (!await sessions.RotateAsync(current, replacement, cancellationToken))
        {
            await sessions.RevokeFamilyAsync(current.FamilyId, cancellationToken);
            return Result<AuthSessionResult>.Failure(
                Error.Unauthorized("Sessão reutilizada e revogada.", "REFRESH_TOKEN_REUSED")
            );
        }
        return Result<AuthSessionResult>.Success(
            new AuthSessionResult(
                new AuthResponse(
                    jwtService.GenerateToken(current.User, replacement.Id),
                    current.UserId
                ),
                raw,
                expiresAt
            )
        );
    }

    private static Result<AuthSessionResult> Invalid() =>
        Result<AuthSessionResult>.Failure(
            Error.Unauthorized("Sessão inválida.", "REFRESH_TOKEN_INVALID")
        );
}
