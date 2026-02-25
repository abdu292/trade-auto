using Brain.Application.Features.Sessions.Commands;
using FluentValidation;

namespace Brain.Application.Features.Sessions.Validators;

public sealed class ToggleSessionCommandValidator : AbstractValidator<ToggleSessionCommand>
{
    public ToggleSessionCommandValidator()
    {
        RuleFor(x => x.Session).NotEmpty();
    }
}
