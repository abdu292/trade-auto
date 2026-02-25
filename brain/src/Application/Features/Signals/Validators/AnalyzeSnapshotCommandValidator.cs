using Brain.Application.Features.Signals.Commands;
using FluentValidation;

namespace Brain.Application.Features.Signals.Validators;

public sealed class AnalyzeSnapshotCommandValidator : AbstractValidator<AnalyzeSnapshotCommand>
{
    public AnalyzeSnapshotCommandValidator()
    {
        RuleFor(x => x.Symbol).NotEmpty().MaximumLength(20);
    }
}
