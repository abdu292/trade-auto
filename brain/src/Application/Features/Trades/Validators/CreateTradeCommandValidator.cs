using Brain.Application.Features.Trades.Commands;
using FluentValidation;

namespace Brain.Application.Features.Trades.Validators;

public sealed class CreateTradeCommandValidator : AbstractValidator<CreateTradeCommand>
{
    public CreateTradeCommandValidator()
    {
        RuleFor(x => x.Symbol).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Rail).NotEmpty();
        RuleFor(x => x.Entry).GreaterThan(0);
        RuleFor(x => x.Tp).GreaterThan(0);
        RuleFor(x => x.MaxLifeSeconds).GreaterThan(0);
    }
}
