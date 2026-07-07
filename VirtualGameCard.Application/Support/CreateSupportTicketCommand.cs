using FluentValidation;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Support;

public sealed record CreateSupportTicketCommand(string Subject, string Category, string Message);

public sealed record SupportTicketResult(Guid Id, string Status, DateTime CreatedAt);

public sealed class CreateSupportTicketCommandValidator
    : AbstractValidator<CreateSupportTicketCommand>
{
    private static readonly string[] Categories = ["code", "payment", "account", "other"];

    public CreateSupportTicketCommandValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MinimumLength(4).MaximumLength(120);
        RuleFor(x => x.Message).NotEmpty().MinimumLength(10).MaximumLength(4000);
        RuleFor(x => x.Category).Must(x => Categories.Contains(x?.ToLowerInvariant()));
    }
}

public sealed class CreateSupportTicketCommandHandler(
    ICurrentUser current,
    ISupportTicketRepository tickets,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork
)
{
    private static readonly CreateSupportTicketCommandValidator Validator = new();

    public async Task<Result<SupportTicketResult>> HandleAsync(
        CreateSupportTicketCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await Validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<SupportTicketResult>.Failure(
                Error.Validation("Dados do chamado inválidos.")
            );
        if (current.Id is not Guid userId)
            return Result<SupportTicketResult>.Failure(
                Error.Unauthorized("Autenticação necessária.")
            );

        var ticket = new SupportTicket
        {
            UserId = userId,
            Subject = command.Subject.Trim(),
            Category = Enum.Parse<SupportCategory>(command.Category, true),
            Message = command.Message.Trim(),
        };
        return await unitOfWork.ExecuteAsync(
            async _ =>
            {
                await tickets.AddAsync(ticket);
                await notifications.AddAsync(
                    new Notification
                    {
                        UserId = userId,
                        Title = "Chamado recebido",
                        Message =
                            $"O chamado #{ticket.Id.ToString()[..8].ToUpperInvariant()} foi aberto.",
                        Kind = NotificationKind.Support,
                    }
                );
                return Result<SupportTicketResult>.Success(
                    new SupportTicketResult(ticket.Id, "open", ticket.CreatedAt)
                );
            },
            cancellationToken
        );
    }
}
