using Brain.Application.Features.Risk.Commands;
using FluentValidation;

namespace Brain.Application.Features.Risk.Validators;

public sealed class SetRiskProfileCommandValidator : AbstractValidator<SetRiskProfileCommand>
{
    public SetRiskProfileCommandValidator()
    {
        RuleFor(x => x.RiskProfileId).NotEmpty();
    }
}
