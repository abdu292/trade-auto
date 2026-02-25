using Brain.Application.Features.Strategies.Commands;
using FluentValidation;

namespace Brain.Application.Features.Strategies.Validators;

public sealed class SetStrategyProfileCommandValidator : AbstractValidator<SetStrategyProfileCommand>
{
    public SetStrategyProfileCommandValidator()
    {
        RuleFor(x => x.StrategyProfileId).NotEmpty();
    }
}
