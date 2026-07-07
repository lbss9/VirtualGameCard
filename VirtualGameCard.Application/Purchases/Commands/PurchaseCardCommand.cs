using FluentValidation;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Application.Purchases.DTOs;
using VirtualGameCard.Application.Purchases.Messages;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Purchases.Commands;

public record PurchaseCardCommand(
    long AmountInCents,
    PaymentMethod PaymentMethod,
    GiftCardPlatform Platform,
    string IdempotencyKey
);

public sealed class PurchaseCardCommandValidator : AbstractValidator<PurchaseCardCommand>
{
    public PurchaseCardCommandValidator()
    {
        RuleFor(x => x.AmountInCents)
            .InclusiveBetween(500, 25_000)
            .Must(x => x % 500 == 0)
            .WithMessage("O valor deve estar entre R$ 5 e R$ 250, em intervalos de R$ 5.")
            .WithErrorCode("INVALID_AMOUNT");
        RuleFor(x => x.PaymentMethod)
            .IsInEnum()
            .WithMessage("Método de pagamento inválido.")
            .WithErrorCode("INVALID_PAYMENT_METHOD");
        RuleFor(x => x.Platform)
            .IsInEnum()
            .WithMessage("Plataforma inválida.")
            .WithErrorCode("INVALID_PLATFORM");
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithErrorCode("IDEMPOTENCY_KEY_INVALID")
            .MaximumLength(100)
            .WithErrorCode("IDEMPOTENCY_KEY_INVALID")
            .WithMessage("Chave de idempotência inválida.");
    }
}

public class PurchaseCardCommandHandler(
    IGiftCardPurchaseRepository purchaseRepository,
    IUserRepository userRepository,
    ICurrentUser currentUser,
    IPaymentMessagePublisher paymentMessagePublisher
)
{
    private static readonly PurchaseCardCommandValidator Validator = new();

    public async Task<Result<PurchaseDetail>> HandleAsync(
        PurchaseCardCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (!currentUser.IsAuthenticated || currentUser.Id is null)
        {
            return Result<PurchaseDetail>.Failure(
                Error.Unauthorized("Usuário não autenticado.", "UNAUTHORIZED")
            );
        }

        var validation = await Validator.ValidateAsync(command);
        if (!validation.IsValid)
            return Result<PurchaseDetail>.Failure(
                Error.Validation(validation.Errors[0].ErrorMessage, validation.Errors[0].ErrorCode)
            );

        var isAccountVerified = await userRepository.GetByIdAsync(
            currentUser.Id.Value,
            user => (bool?)user.IsAccountVerified
        );

        if (isAccountVerified is null)
        {
            return Result<PurchaseDetail>.Failure(
                Error.NotFound("Usuário não encontrado.", "USER_NOT_FOUND")
            );
        }

        if (!isAccountVerified.Value)
        {
            return Result<PurchaseDetail>.Failure(
                Error.Validation(
                    "Confirme seu e-mail antes de realizar uma compra.",
                    "ACCOUNT_NOT_VERIFIED"
                )
            );
        }

        var existing = await purchaseRepository.GetByIdempotencyKeyAsync(
            currentUser.Id.Value,
            command.IdempotencyKey
        );
        if (existing is not null)
        {
            if (
                existing.AmountInCents != command.AmountInCents
                || existing.PaymentMethod != command.PaymentMethod
                || existing.Platform != command.Platform
            )
                return Result<PurchaseDetail>.Failure(
                    Error.Conflict(
                        "A chave de idempotência já foi usada com outra compra.",
                        "IDEMPOTENCY_KEY_REUSED"
                    )
                );
            return Result<PurchaseDetail>.Success(ToDetail(existing));
        }

        var purchase = new GiftCardPurchase
        {
            UserId = currentUser.Id.Value,
            AmountInCents = command.AmountInCents,
            PaymentMethod = command.PaymentMethod,
            Platform = command.Platform,
            Status = GiftCardStatus.Pending,
            IdempotencyKey = command.IdempotencyKey,
            PaymentReference = $"pay_{SecureToken.Generate()[..24]}",
        };

        var persisted = await purchaseRepository.AddIdempotentAsync(purchase);
        if (
            !persisted.Created
            && (
                persisted.Purchase.AmountInCents != command.AmountInCents
                || persisted.Purchase.PaymentMethod != command.PaymentMethod
                || persisted.Purchase.Platform != command.Platform
            )
        )
            return Result<PurchaseDetail>.Failure(
                Error.Conflict(
                    "A chave de idempotência já foi usada com outra compra.",
                    "IDEMPOTENCY_KEY_REUSED"
                )
            );

        if (persisted.Created)
        {
            await paymentMessagePublisher.PublishPaymentRequestedAsync(
                new PaymentRequestedMessage(
                    persisted.Purchase.Id,
                    persisted.Purchase.UserId,
                    persisted.Purchase.AmountInCents,
                    PlatformName(persisted.Purchase.Platform),
                    MethodName(persisted.Purchase.PaymentMethod),
                    persisted.Purchase.IdempotencyKey,
                    DateTime.UtcNow
                ),
                cancellationToken
            );
        }

        return Result<PurchaseDetail>.Success(ToDetail(persisted.Purchase));
    }

    private static string MethodName(PaymentMethod method) =>
        method == PaymentMethod.Pix ? "pix" : "card";

    private static string PlatformName(GiftCardPlatform platform) =>
        platform switch
        {
            GiftCardPlatform.Playstation => "playstation",
            GiftCardPlatform.GooglePlay => "google-play",
            _ => platform.ToString().ToLowerInvariant(),
        };

    private static PurchaseDetail ToDetail(GiftCardPurchase purchase) =>
        new(
            purchase.Id,
            purchase.AmountInCents,
            purchase.PaymentMethod,
            purchase.Platform,
            purchase.Status,
            purchase.Code,
            purchase.Status.ToString().ToLowerInvariant(),
            purchase.PaymentReference,
            purchase.CreatedAt
        );
}
