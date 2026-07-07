using VirtualGameCard.Application.Common;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Auth.Commands;

public record ResetPasswordCommand(string Token, string NewPassword);

public class ResetPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository,
    IRefreshSessionRepository sessions,
    INotificationRepository notifications,
    VirtualGameCard.Application.Interfaces.IUnitOfWork unitOfWork
)
{
    private static readonly ResetPasswordCommandValidator _validator = new();

    public async Task<Result<bool>> HandleAsync(ResetPasswordCommand command)
    {
        var validation = await _validator.ValidateAsync(command);
        if (!validation.IsValid)
            return Result<bool>.Failure(Error.Validation(validation.Errors[0].ErrorMessage));

        var tokenHash = SecureToken.Hash(command.Token);
        var resetToken = await tokenRepository.GetByHashAsync(tokenHash);

        if (resetToken is null)
            return Result<bool>.Failure(
                Error.Validation(
                    "Token inválido. Solicite a redefinição novamente.",
                    "RESET_TOKEN_INVALID"
                )
            );
        if (resetToken.UsedAt is not null)
            return Result<bool>.Failure(
                Error.Validation("Este token já foi utilizado.", "RESET_TOKEN_INVALID")
            );
        if (resetToken.ExpiresAt <= DateTime.UtcNow)
            return Result<bool>.Failure(
                Error.Validation("Este token expirou.", "RESET_TOKEN_EXPIRED")
            );

        var user = await userRepository.GetByIdAsync(resetToken.UserId);
        if (user is null)
            return Result<bool>.Failure(
                Error.NotFound("Usuário não encontrado.", "USER_NOT_FOUND")
            );

        if (BCrypt.Net.BCrypt.Verify(command.NewPassword, user.PasswordHash))
            return Result<bool>.Failure(
                Error.Validation("A nova senha deve ser diferente.", "PASSWORD_REUSE")
            );

        return await unitOfWork.ExecuteAsync(async ct =>
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.NewPassword);
            resetToken.UsedAt = DateTime.UtcNow;
            await userRepository.UpdateAsync(user);
            await tokenRepository.UpdateAsync(resetToken);
            await tokenRepository.InvalidateAllForUserAsync(user.Id);
            await sessions.RevokeAllForUserAsync(user.Id, cancellationToken: ct);
            await notifications.AddAsync(
                new VirtualGameCard.Domain.Entities.Notification
                {
                    UserId = user.Id,
                    Title = "Senha redefinida",
                    Message = "Sua senha foi redefinida e as sessões anteriores foram encerradas.",
                    Kind = VirtualGameCard.Domain.Entities.NotificationKind.Security,
                }
            );
            return Result<bool>.Success(true);
        });
    }
}
