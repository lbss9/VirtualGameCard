using VirtualGameCard.Application.Auth;
using VirtualGameCard.Application.Auth.DTOs;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Auth.Commands;

public record LoginCommand(string Email, string Password);

public class LoginCommandHandler(IUserRepository userRepository, AuthSessionIssuer sessions)
{
    private static readonly LoginCommandValidator Validator = new();
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(
        "timing-protection-not-a-real-password"
    );

    public async Task<Result<AuthSessionResult>> HandleAsync(LoginCommand command)
    {
        var validation = await Validator.ValidateAsync(command);
        if (!validation.IsValid)
            return Result<AuthSessionResult>.Failure(
                Error.Validation(validation.Errors[0].ErrorMessage)
            );
        var user = await userRepository.GetByEmailAsync(command.Email.Trim().ToLowerInvariant());
        var passwordMatches = BCrypt.Net.BCrypt.Verify(
            command.Password,
            user?.PasswordHash ?? DummyHash
        );

        if (user is null || !passwordMatches)
            return Result<AuthSessionResult>.Failure(
                Error.Unauthorized("E-mail ou senha inválidos.", "INVALID_CREDENTIALS")
            );

        return Result<AuthSessionResult>.Success(await sessions.IssueAsync(user));
    }
}
