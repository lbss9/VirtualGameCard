using FluentValidation;

namespace VirtualGameCard.Application.Auth.Commands;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("E-mail é obrigatório")
            .EmailAddress()
            .WithMessage("E-mail inválido")
            .MaximumLength(255)
            .WithMessage("E-mail muito longo");

        RuleFor(x => x.Password)
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
