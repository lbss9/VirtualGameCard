using FluentValidation;
using VirtualGameCard.Application.Auth.Commands;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Profile;

public sealed record ProfileDto(Guid UserId, string Email, bool EmailVerified, DateTime CreatedAt);

public sealed record GetProfileQuery;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

public sealed record SendEmailVerificationCommand;

public sealed record SimulateEmailVerificationCommand;

public sealed record VerifyEmailCommand(string Token);

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator() => RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
}

public sealed class GetProfileQueryHandler(ICurrentUser current, IUserRepository users)
{
    public async Task<Result<ProfileDto>> HandleAsync()
    {
        if (current.Id is not Guid id)
            return Result<ProfileDto>.Failure(Error.Unauthorized("Autenticação necessária."));
        var profile = await users.GetByIdAsync(
            id,
            x => new ProfileDto(x.Id, x.Email, x.IsAccountVerified, x.CreatedAt)
        );
        return profile is null
            ? Result<ProfileDto>.Failure(
                Error.NotFound("Usuário não encontrado.", "USER_NOT_FOUND")
            )
            : Result<ProfileDto>.Success(profile);
    }
}

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).SetValidator(new PasswordValueValidator());
    }

    private sealed class PasswordValueValidator : AbstractValidator<string>
    {
        public PasswordValueValidator()
        {
            RuleFor(x => x)
                .NotEmpty()
                .MinimumLength(8)
                .MaximumLength(128)
                .Matches("[A-Z]")
                .Matches("[a-z]")
                .Matches("[0-9]")
                .Matches("[^a-zA-Z0-9]");
        }
    }
}

public sealed class ChangePasswordCommandHandler(
    ICurrentUser current,
    IUserRepository users,
    IRefreshSessionRepository sessions,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork
)
{
    private static readonly ChangePasswordCommandValidator Validator = new();

    public async Task<Result<bool>> HandleAsync(
        ChangePasswordCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await Validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<bool>.Failure(
                Error.Validation(validation.Errors[0].ErrorMessage, "PASSWORD_POLICY_FAILED")
            );
        if (current.Id is not Guid id)
            return Result<bool>.Failure(Error.Unauthorized("Autenticação necessária."));
        var user = await users.GetByIdAsync(id);
        if (user is null)
            return Result<bool>.Failure(
                Error.NotFound("Usuário não encontrado.", "USER_NOT_FOUND")
            );
        if (!BCrypt.Net.BCrypt.Verify(command.CurrentPassword, user.PasswordHash))
            return Result<bool>.Failure(
                Error.Validation("A senha atual está incorreta.", "CURRENT_PASSWORD_INVALID")
            );
        if (BCrypt.Net.BCrypt.Verify(command.NewPassword, user.PasswordHash))
            return Result<bool>.Failure(
                Error.Validation("A nova senha deve ser diferente.", "PASSWORD_REUSE")
            );

        return await unitOfWork.ExecuteAsync(
            async ct =>
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.NewPassword);
                await users.UpdateAsync(user);
                await sessions.RevokeAllForUserAsync(id, cancellationToken: ct);
                await notifications.AddAsync(
                    new Notification
                    {
                        UserId = id,
                        Title = "Senha alterada",
                        Message =
                            "Sua senha foi alterada com sucesso. Entre novamente nos seus dispositivos.",
                        Kind = NotificationKind.Security,
                    }
                );
                return Result<bool>.Success(true);
            },
            cancellationToken
        );
    }
}

public sealed class SendEmailVerificationCommandHandler(
    ICurrentUser current,
    IUserRepository users,
    IEmailVerificationTokenRepository tokens
)
{
    public async Task<Result<string>> HandleAsync(CancellationToken cancellationToken = default)
    {
        if (current.Id is not Guid id)
            return Result<string>.Failure(Error.Unauthorized("Autenticação necessária."));
        var user = await users.GetByIdAsync(id);
        if (user is null)
            return Result<string>.Failure(
                Error.NotFound("Usuário não encontrado.", "USER_NOT_FOUND")
            );
        if (user.IsAccountVerified)
            return Result<string>.Failure(
                Error.Conflict("E-mail já confirmado.", "EMAIL_ALREADY_VERIFIED")
            );
        var raw = SecureToken.Generate();
        await tokens.AddAsync(
            new EmailVerificationToken
            {
                UserId = id,
                TokenHash = SecureToken.Hash(raw),
                ExpiresAt = DateTime.UtcNow.AddHours(24),
            }
        );
        return Result<string>.Success(raw);
    }
}

public sealed class SimulateEmailVerificationCommandHandler(
    ICurrentUser current,
    IUserRepository users
)
{
    public async Task<Result<ProfileDto>> HandleAsync()
    {
        if (current.Id is not Guid id)
            return Result<ProfileDto>.Failure(Error.Unauthorized("Autenticação necessária."));

        var user = await users.GetByIdAsync(id);
        if (user is null)
            return Result<ProfileDto>.Failure(
                Error.NotFound("Usuário não encontrado.", "USER_NOT_FOUND")
            );

        if (!user.IsAccountVerified)
        {
            user.IsAccountVerified = true;
            user.AccountVerifiedAt = DateTime.UtcNow;
            await users.UpdateAsync(user);
        }

        return Result<ProfileDto>.Success(
            new ProfileDto(user.Id, user.Email, user.IsAccountVerified, user.CreatedAt)
        );
    }
}

public sealed class VerifyEmailCommandHandler(
    IEmailVerificationTokenRepository tokens,
    IUserRepository users,
    IUnitOfWork unitOfWork
)
{
    private static readonly VerifyEmailCommandValidator Validator = new();

    public async Task<Result<bool>> HandleAsync(
        VerifyEmailCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (!(await Validator.ValidateAsync(command, cancellationToken)).IsValid)
            return Result<bool>.Failure(
                Error.Validation("Token obrigatório.", "VERIFICATION_TOKEN_INVALID")
            );
        var token = await tokens.GetActiveByHashAsync(SecureToken.Hash(command.Token));
        if (token is null)
            return Result<bool>.Failure(
                Error.Validation("Token inválido ou expirado.", "VERIFICATION_TOKEN_INVALID")
            );
        var user = await users.GetByIdAsync(token.UserId);
        if (user is null)
            return Result<bool>.Failure(
                Error.NotFound("Usuário não encontrado.", "USER_NOT_FOUND")
            );
        if (user.IsAccountVerified)
            return Result<bool>.Failure(
                Error.Conflict("E-mail já confirmado.", "EMAIL_ALREADY_VERIFIED")
            );
        return await unitOfWork.ExecuteAsync(
            async _ =>
            {
                user.IsAccountVerified = true;
                user.AccountVerifiedAt = DateTime.UtcNow;
                token.UsedAt = DateTime.UtcNow;
                await users.UpdateAsync(user);
                await tokens.UpdateAsync(token);
                return Result<bool>.Success(true);
            },
            cancellationToken
        );
    }
}
