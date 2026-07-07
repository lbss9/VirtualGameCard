using FluentValidation;

namespace VirtualGameCard.Application.Auth.Commands;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Token é obrigatório");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("Senha é obrigatória")
            .MaximumLength(128)
            .WithMessage("Senha deve ter no máximo 128 caracteres")
            .MinimumLength(8)
            .WithMessage("Senha deve ter no mínimo 8 caracteres")
            .Matches("[A-Z]")
            .WithMessage("Senha deve ter pelo menos uma letra maiúscula")
            .Matches("[a-z]")
            .WithMessage("Senha deve ter pelo menos uma letra minúscula")
            .Matches("[0-9]")
            .WithMessage("Senha deve ter pelo menos um número")
            .Matches("[^a-zA-Z0-9]")
            .WithMessage("Senha deve ter pelo menos um caractere especial");
    }
}
