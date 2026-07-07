using FluentValidation;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Purchases.DTOs;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Purchases.Queries;

public record GetPurchasesQuery(Guid UserId, int Page, int PageSize);

public sealed class GetPurchasesQueryValidator : AbstractValidator<GetPurchasesQuery>
{
    private static readonly int[] PageSizes = [20, 50, 100];

    public GetPurchasesQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithErrorCode("INVALID_PAGE")
            .WithMessage("A página deve ser maior que zero.");
        RuleFor(x => x.PageSize)
            .Must(PageSizes.Contains)
            .WithErrorCode("INVALID_PAGE_SIZE")
            .WithMessage("Use 20, 50 ou 100 itens por página.");
    }
}

public class GetPurchasesQueryHandler(IGiftCardPurchaseRepository purchaseRepository)
{
    private static readonly GetPurchasesQueryValidator Validator = new();

    public async Task<Result<PagedResult<PurchaseSummary>>> HandleAsync(GetPurchasesQuery query)
    {
        var validation = await Validator.ValidateAsync(query);
        if (!validation.IsValid)
            return Result<PagedResult<PurchaseSummary>>.Failure(
                Error.Validation(validation.Errors[0].ErrorMessage, validation.Errors[0].ErrorCode)
            );
        var page = query.Page;
        var pageSize = query.PageSize;

        var total = await purchaseRepository.CountByUserAsync(query.UserId);
        var items = await purchaseRepository.GetPageByUserAsync(query.UserId, page, pageSize);

        var summaries = items
            .Select(p => new PurchaseSummary(
                p.Id,
                p.AmountInCents,
                p.PaymentMethod,
                p.Platform,
                p.Status,
                p.CreatedAt
            ))
            .ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return Result<PagedResult<PurchaseSummary>>.Success(
            new PagedResult<PurchaseSummary>(summaries, total, page, pageSize, totalPages)
        );
    }
}
