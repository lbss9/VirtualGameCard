using VirtualGameCard.Application.Auth.DTOs;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Auth.Commands;

public record ForgotPasswordCommand(string Email);

public class ForgotPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository
)
{
    private static readonly ForgotPasswordCommandValidator Validator = new();

    /// <summary>Validade do token de redefinição.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public async Task<Result<ForgotPasswordResult>> HandleAsync(ForgotPasswordCommand command)
    {
        var validation = await Validator.ValidateAsync(command);
        if (!validation.IsValid)
            return Result<ForgotPasswordResult>.Failure(
                Error.Validation(validation.Errors[0].ErrorMessage)
            );
        var user = await userRepository.GetByEmailAsync(command.Email.Trim().ToLowerInvariant());

        // Nunca revelamos se o e-mail existe — resposta sempre "de sucesso".
        if (user is null)
            return Result<ForgotPasswordResult>.Success(new ForgotPasswordResult(null, null));

        var rawToken = SecureToken.Generate();
        var expiresAt = DateTime.UtcNow.Add(Lifetime);

        await tokenRepository.AddAsync(
            new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = SecureToken.Hash(rawToken),
                ExpiresAt = expiresAt,
            }
        );

        // Em produção este token seria enviado por e-mail, não retornado.
        return Result<ForgotPasswordResult>.Success(new ForgotPasswordResult(rawToken, expiresAt));
    }
}
