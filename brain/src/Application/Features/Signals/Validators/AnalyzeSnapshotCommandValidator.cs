using Brain.Application.Features.Signals.Commands;
using FluentValidation;

namespace Brain.Application.Features.Signals.Validators;

public sealed class AnalyzeSnapshotCommandValidator : AbstractValidator<AnalyzeSnapshotCommand>
{
    public AnalyzeSnapshotCommandValidator()
    {
        RuleFor(x => x.Snapshot).NotNull();
        RuleFor(x => x.Snapshot.Symbol).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Snapshot.Atr).GreaterThan(0);
        RuleFor(x => x.Snapshot.TimeframeData).NotEmpty();
    }
}
