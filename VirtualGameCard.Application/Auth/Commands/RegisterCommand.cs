using VirtualGameCard.Application.Auth;
using VirtualGameCard.Application.Auth.DTOs;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Auth.Commands;

public record RegisterCommand(string Email, string Password);

public class RegisterCommandHandler(
    IUserRepository userRepository,
    AuthSessionIssuer sessions,
    IUnitOfWork unitOfWork,
    INotificationRepository notifications
)
{
    private static readonly RegisterCommandValidator _validator = new();

    public async Task<Result<AuthSessionResult>> HandleAsync(RegisterCommand command)
    {
        var validation = await _validator.ValidateAsync(command);

        if (!validation.IsValid)
            return Result<AuthSessionResult>.Failure(
                Error.Validation(validation.Errors[0].ErrorMessage)
            );

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        if (await userRepository.ExistsByEmailAsync(normalizedEmail))
            return Result<AuthSessionResult>.Failure(
                Error.Conflict("E-mail já cadastrado.", "EMAIL_ALREADY_EXISTS")
            );

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.Password),
        };

        return await unitOfWork.ExecuteAsync(async ct =>
        {
            await userRepository.AddAsync(user);
            await notifications.AddAsync(
                new Notification
                {
                    UserId = user.Id,
                    Title = "Boas-vindas à VirtualGameCard!",
                    Message = "Sua conta foi criada. Confirme seu e-mail para realizar compras.",
                    Kind = NotificationKind.News,
                }
            );
            return Result<AuthSessionResult>.Success(
                await sessions.IssueAsync(user, cancellationToken: ct)
            );
        });
    }
}
